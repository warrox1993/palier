using Microsoft.EntityFrameworkCore;
using Npgsql;
using Palier.Application.Pipeline;
using Palier.Infrastructure.Pipeline;

namespace Palier.Database.Tests;

/// <summary>
/// La garde applicative, éprouvée SUR TABLE VIDE — et le nom de ce fichier le
/// dit.
///
/// C'est la moitié que le moteur ne peut pas tenir. `ddl-rowsecurity.html` :
/// l'expression d'une politique « will be evaluated FOR EACH ROW ». Zéro ligne
/// parcourue, zéro évaluation, AUCUNE exception. Sur un compte neuf, une table
/// fraîchement créée, les premiers jours de production, « identité absente » et
/// « cet utilisateur n'a pas de données » redeviennent indiscernables.
///
/// Pire, la loudness dépendrait du PLAN : sur un parcours d'index, le
/// planificateur peut hisser la fonction `stable` en clé de parcours et lever
/// même sur table vide ; en parcours séquentiel, non. Une propriété de sécurité
/// qui dépend du plan n'est pas une propriété.
/// </summary>
[Collection(BaseFixture.Collection)]
public sealed class GardeApplicativeTests(BaseFixture baseDeDonnees)
{
    [Fact]
    public async Task Sur_body_weight_VIDE_et_sans_identite_le_refus_est_APPLICATIF_et_nomme_le_cas_d_usage()
    {
        // `body_weight` est choisie parce qu'aucune autre épreuve du projet n'y
        // écrit : elle est réellement vide, et l'assertion ci-dessous le
        // VÉRIFIE au lieu de l'espérer.
        var lignes = await Compter(baseDeDonnees.ChaineMigrations);
        Assert.True(
            lignes == 0,
            $"`body_weight` porte {lignes} ligne(s) : l'épreuve mesurerait le cas peuplé, "
                + "c'est-à-dire exactement celui que la garde applicative ne sert pas à couvrir."
        );

        // CE QUE LE MOTEUR FAIT ICI, MESURÉ ET NON SUPPOSÉ — et le résultat
        // contredit la formulation du plan, qui annonçait un moteur MUET sur
        // table vide.
        //
        // Mesuré le 20/08/2026 sur PostgreSQL 18.6, sous `palier_app`, sur
        // `body_weight` VIDE et sans identité : les trois formes de requête —
        // `select count(*)`, `select *`, et un `select` avec un `where` sur une
        // autre colonne — lèvent TOUTES `28000`. Motif : l'enveloppe
        // `(select app.utilisateur())` produit un InitPlan, évalué UNE FOIS par
        // instruction avant tout parcours, et non une fois par ligne.
        //
        // C'est une bonne nouvelle, et elle ne change RIEN au besoin de cette
        // garde : rien dans la documentation ne promet que le planificateur
        // évaluera toujours cet InitPlan, ni qu'il ne l'élaguera pas sur une
        // relation prouvée vide, ni qu'une politique future écrite autrement se
        // comportera pareil. Une propriété de sécurité qui dépend du plan
        // d'exécution n'est pas une propriété — on ne la revendique donc pas,
        // et on n'écrit pas ici d'assertion qui la figerait.
        //
        // Ce qui est éprouvé ci-dessous est indépendant du plan : le refus
        // tombe AVANT que le moteur ait la parole.

        // Et voici ce que la garde applicative en fait.
        await using var contexte = BaseFixture.Contexte(baseDeDonnees.ChaineApp);
        var executeur = new ExecuteurDeCasDUsage(contexte, new DemandeurSansIdentite());

        var refus = await Assert.ThrowsAsync<IdentiteAbsenteException>(() =>
            executeur.ExecuterAsync(
                "ListerLesPesees",
                async jeton => await contexte.BodyWeights.CountAsync(jeton)
            )
        );

        Assert.True(
            refus.NomDuCasDUsage == "ListerLesPesees",
            $"Le refus nomme « {refus.NomDuCasDUsage} » au lieu du cas d'usage appelé. Un code "
                + "SQL nu ne dit pas quel appel a échoué."
        );
        Assert.Contains("ListerLesPesees", refus.Message, StringComparison.Ordinal);

        // Et ce n'est PAS une exception du moteur : `ThrowsAsync` ci-dessus est
        // strict sur le type. Un `PostgresException` prouverait que la garde
        // n'a pas parlé la première, et le message porterait un code SQL nu.
        Assert.IsNotType<PostgresException>(refus);

        // Et il ne porte AUCUNE donnée : ni identifiant, ni valeur.
        // `01-conformite.md` § 4.
        Assert.DoesNotContain("body_weight", refus.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Le_refus_tombe_AVANT_toute_ouverture_de_transaction()
    {
        // « Refuser avant d'ouvrir la transaction » n'est pas une élégance :
        // une transaction ouverte puis abandonnée consomme une connexion du
        // pool et un instantané. On le MESURE en comptant les transactions du
        // moteur avant et après — relire le code ne prouverait rien.
        await using var contexte = BaseFixture.Contexte(baseDeDonnees.ChaineApp);
        var executeur = new ExecuteurDeCasDUsage(contexte, new DemandeurSansIdentite());

        var avant = await Transactions();
        await Assert.ThrowsAsync<IdentiteAbsenteException>(() =>
            executeur.ExecuterAsync("ListerLesPesees", _ => Task.FromResult(0))
        );
        var apres = await Transactions();

        Assert.True(
            apres == avant,
            $"Le compteur de transactions validées est passé de {avant} à {apres} : une "
                + "transaction a été ouverte alors que l'identité manquait."
        );
    }

    [Fact]
    public async Task Avec_une_identite_le_meme_cas_d_usage_passe_et_voit_ses_propres_lignes()
    {
        // La branche opposée. Sans elle, un exécuteur qui refuserait TOUT
        // passerait les deux épreuves ci-dessus sans rien démontrer.
        var identifiant = await Utilisateur();
        await using var contexte = BaseFixture.Contexte(baseDeDonnees.ChaineApp);
        var executeur = new ExecuteurDeCasDUsage(contexte, new Demandeur(identifiant));

        var compte = await executeur.ExecuterAsync(
            "ListerLesPesees",
            async jeton => await contexte.BodyWeights.CountAsync(jeton)
        );

        Assert.True(compte == 0, $"Attendu zéro pesée pour un utilisateur neuf, obtenu {compte}.");
    }

    [Fact]
    public async Task ÉPREUVE_INVERSÉE_avec_EnableRetryOnFailure_tout_reste_VERT()
    {
        // ⚠ ÉPREUVE INVERSÉE — ELLE DOIT RESTER VERTE. Ne pas la « corriger »
        // en la rendant rouge.
        //
        // Le vert PROUVE que le passage par `CreateExecutionStrategy` est réel.
        // Microsoft Learn, _Connection Resiliency_ : « if your code initiates a
        // transaction using BeginTransactionAsync() … You will receive an
        // exception … does not support user-initiated transactions. » Sans
        // l'enveloppe, activer la résilience ferait lever TOUTES les requêtes
        // d'un coup — le soir où quelqu'un l'activera sur une base managée qui
        // clignote.
        var identifiant = await Utilisateur();
        await using var contexte = BaseFixture.Contexte(
            baseDeDonnees.ChaineApp,
            npgsql => npgsql.EnableRetryOnFailure(3)
        );

        // ET IL FAUT VÉRIFIER QUE L'ÉTAPE A PRIS EFFET. Une expérience dont une
        // étape n'a pas eu lieu n'est pas une expérience : si
        // `EnableRetryOnFailure` n'était pas actif, cette épreuve serait verte
        // pour rien. La stratégie non résiliente d'EF s'appelle
        // `NonRetryingExecutionStrategy` ; on refuse de continuer si c'est elle
        // qu'on a.
        var strategie = contexte.Database.CreateExecutionStrategy();
        Assert.True(
            strategie.RetriesOnFailure,
            $"La stratégie d'exécution est « {strategie.GetType().Name} », qui ne réessaie "
                + "pas : `EnableRetryOnFailure` a été écrit mais n'a pas pris effet, et cette "
                + "épreuve inversée ne prouverait rien."
        );

        var executeur = new ExecuteurDeCasDUsage(contexte, new Demandeur(identifiant));
        var compte = await executeur.ExecuterAsync(
            "ListerLesPesees",
            async jeton => await contexte.BodyWeights.CountAsync(jeton)
        );
        Assert.True(compte == 0, $"Attendu zéro pesée, obtenu {compte}.");
    }

    private sealed class Demandeur(Guid identifiant) : IIdentiteDemandeur
    {
        public Guid? Identifiant => identifiant;
    }

    private static async Task<long> Compter(string chaine)
    {
        await using var connexion = new NpgsqlConnection(chaine);
        await connexion.OpenAsync();
        await using var commande = new NpgsqlCommand(
            "select count(*) from public.body_weight",
            connexion
        );
        return (long)(await commande.ExecuteScalarAsync())!;
    }

    private async Task<long> Transactions()
    {
        await using var connexion = new NpgsqlConnection(baseDeDonnees.ChaineAdministrateur);
        await connexion.OpenAsync();
        await using var commande = new NpgsqlCommand(
            "select xact_commit + xact_rollback from pg_stat_database where datname = current_database()",
            connexion
        );
        return (long)(await commande.ExecuteScalarAsync())!;
    }

    private async Task<Guid> Utilisateur()
    {
        var identifiant = Guid.NewGuid();
        await using var connexion = new NpgsqlConnection(baseDeDonnees.ChaineAdministrateur);
        await connexion.OpenAsync();
        await using var commande = new NpgsqlCommand(
            """
            insert into public."AspNetUsers" ("Id", "EmailConfirmed", "PhoneNumberConfirmed",
                   "TwoFactorEnabled", "LockoutEnabled", "AccessFailedCount")
            values ($1, false, false, false, false, 0)
            """,
            connexion
        );
        commande.Parameters.AddWithValue(identifiant);
        await commande.ExecuteNonQueryAsync();
        return identifiant;
    }
}
