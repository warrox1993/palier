using Microsoft.EntityFrameworkCore;
using System.Reflection;
using Palier.Application.Pipeline;
using Palier.Infrastructure;
using Palier.Infrastructure.Identite;

namespace Palier.Database.Tests;

/// <summary>
/// « Rien d'autre ne peut le faire. »
///
/// Le pipeline pose l'identité — mais un <c>IHostedService</c>, une tâche de
/// fond, un contrôle de santé ou une file de rejeu n'ont PAS de transaction,
/// donc pas d'identité : bruyants sur table peuplée, SILENCIEUX sur table vide.
/// Cette épreuve ferme la classe entière plutôt que ses instances.
///
/// Trente lignes de <c>System.Reflection</c> et AUCUNE dépendance nouvelle.
/// NetArchTest ferait la même chose au prix d'un paquet, d'une licence à
/// vérifier et d'un Dependabot de plus — `CLAUDE.md` § 1 déconseille d'inventer
/// un outillage quand un mécanisme simple suffit, et « chercher avant d'écrire »
/// vaut aussi pour les paquets.
/// </summary>
public sealed class ArchitectureTests
{
    private static readonly Assembly[] _assemblages =
    [
        typeof(IExecuteurDeCasDUsage).Assembly, // Palier.Application
        typeof(PalierDbContext).Assembly, // Palier.Infrastructure
        typeof(Program).Assembly, // Palier.Api
    ];

    /// <summary>
    /// LES EXEMPTIONS NOMMÉES, ET LEUR MOTIF — D21 : le motif vit dans
    /// l'épreuve, jamais dans un fichier de configuration d'outil.
    ///
    /// Une exemption non nommée est une porte laissée ouverte. Celles-ci sont
    /// écrites ici, en toutes lettres, et
    /// <see cref="Chaque_exemption_nommee_designe_un_type_qui_EXISTE_ENCORE" />
    /// refuse le dépôt le jour où le type exempté disparaît ou est renommé —
    /// sans quoi l'exemption survivrait à sa cible et couvrirait, en silence,
    /// tout ce qui reprendrait ce nom.
    ///
    /// <c>Palier.Api.Socle.LecteurDeSocle</c> — l'assertion de démarrage de D37
    /// et la route <c>GET /api/v1/sante</c>. C'est le SEUL chemin légitime hors
    /// pipeline de ce lot, pour une raison qui se vérifie ligne à ligne : ce
    /// type n'interroge AUCUNE table portant une donnée personnelle. Il lit les
    /// catalogues du moteur (<c>pg_roles</c>, <c>pg_class</c>), l'historique des
    /// migrations, et le compte de <c>nutrient_refs</c> — table de référence
    /// publique, politique <c>using (true)</c> en lecture seule. Il n'a donc
    /// aucune identité à poser : l'assertion s'exécute AVANT que le serveur
    /// accepte une requête, et la route répond sans authentification jusqu'au
    /// lot 4 (D41).
    /// </summary>
    /// <c>Palier.Infrastructure.Identite.MagasinDeSessions</c> — le SEUL type
    /// autorisé à prendre <c>PalierAuthDbContext</c>. Motif, vérifiable ligne à
    /// ligne : il porte le chemin que D38 laissait à concevoir, celui qui lit
    /// `AspNetUsers` par email AVANT qu'aucune identité n'existe, et il n'atteint
    /// aucune table de donnée de santé — `palier_auth` n'y a aucun privilège, ce
    /// que trois épreuves de `RolesTests` exigent en code 42501.
    /// <c>Palier.Infrastructure.Coffre.AmorcageDuTrousseau</c> — D59. Il lit
    /// <c>cles_de_donnees</c> AVANT que le serveur accepte une requête, donc
    /// sans identité à poser, exactement comme <c>LecteurDeSocle</c>. Motif
    /// vérifiable ligne à ligne : il ne touche que cette table, qui ne porte
    /// AUCUNE donnée personnelle — des enveloppes chiffrées, illisibles sans le
    /// coffre. Il LIT et n'écrit jamais : `palier_app` n'a que <c>select</c> sur
    /// cette table, aucune politique d'écriture n'existe, et deux épreuves de
    /// <see cref="CleDeDonneesTests" /> l'exigent en code 42501.
    private static readonly string[] _exemptionsNommees =
    [
        "Palier.Api.Socle.LecteurDeSocle",
        "Palier.Infrastructure.Identite.MagasinDeSessions",
        "Palier.Infrastructure.Coffre.AmorcageDuTrousseau",
    ];

    [Fact]
    public void Une_FABRIQUE_de_contexte_est_une_voie_vers_le_contexte()
    {
        // La quatrième question du franchissement, appliquée à la branche que
        // D59 a ajoutée. Sans elle, un type prenant
        // `IDbContextFactory<PalierDbContext>` atteindrait les mêmes tables
        // sans identité et passerait le contrôle sans être vu — mesuré : c'est
        // exactement ce qui se produisait avant qu'on la ferme.
        //
        // Cette épreuve rougit le jour où la branche disparaît, et c'est sa
        // seule raison d'être : une exemption qui ne protège plus rien est pire
        // qu'une absence d'exemption, parce qu'elle rassure.
        var voies = Voies(typeof(Palier.Infrastructure.Coffre.AmorcageDuTrousseau)).ToArray();

        Assert.NotEmpty(voies);
        Assert.Contains(voies, v => v.Contains("fabrique", StringComparison.Ordinal));
    }

    [Fact]
    public void Les_trois_assemblages_du_produit_sont_bien_charges()
    {
        // Quatrième question du franchissement : sans cette assertion, une
        // référence de projet retirée ferait passer l'épreuve suivante au vert
        // en n'inspectant plus rien — ruling P12.
        var noms = _assemblages.Select(a => a.GetName().Name).ToArray();
        Assert.Contains("Palier.Application", noms);
        Assert.Contains("Palier.Infrastructure", noms);
        Assert.Contains("Palier.Api", noms);

        var types = _assemblages.Sum(a => a.GetTypes().Length);
        Assert.True(types > 20, $"Le parcours n'a vu que {types} types dans les trois assemblages.");
    }

    [Fact]
    public void Aucun_type_hors_des_gestionnaires_ne_prend_PalierDbContext_en_dependance()
    {
        var fautifs = new List<string>();

        foreach (var assemblage in _assemblages)
        {
            foreach (var type in assemblage.GetTypes())
            {
                if (type.GetCustomAttribute<GestionnaireDeCasDUsageAttribute>() is not null)
                {
                    continue;
                }

                if (
                    type.FullName is not null
                    && _exemptionsNommees.Contains(type.FullName, StringComparer.Ordinal)
                )
                {
                    continue;
                }

                foreach (var voie in Voies(type))
                {
                    fautifs.Add($"{type.FullName} ({voie})");
                }
            }
        }

        Assert.True(
            fautifs.Count == 0,
            "Ces types prennent `PalierDbContext` sans être des gestionnaires de cas d'usage :\n  "
                + string.Join("\n  ", fautifs)
                + "\n\nSeul le pipeline ouvre une transaction et y pose l'identité. Un type qui "
                + "tient le contexte hors de ce chemin interroge la base SANS identité : le "
                + "moteur le signale sur une table peuplée, et se tait sur une table vide.\n"
                + "Si ce type EST un cas d'usage, qu'il porte `[GestionnaireDeCasDUsage]`.\n"
                + "S'il est le chemin hors pipeline d'un contrôle de santé, il s'ajoute à "
                + "`_exemptionsNommees` AVEC son motif — jamais autrement."
        );
    }

    [Fact]
    public void Chaque_exemption_nommee_designe_un_type_qui_EXISTE_ENCORE()
    {
        // Quatrième question du franchissement, appliquée aux exemptions
        // elles-mêmes. Une exemption dont la cible a été renommée ne signale
        // rien : elle reste dans la liste, ne couvre plus personne, et couvrira
        // en silence le prochain type qui reprendra ce nom. Une porte laissée
        // ouverte sur un couloir vide reste une porte ouverte.
        var connus = _assemblages
            .SelectMany(a => a.GetTypes())
            .Select(t => t.FullName)
            .Where(n => n is not null)
            .ToHashSet(StringComparer.Ordinal);

        var mortes = _exemptionsNommees.Where(e => !connus.Contains(e)).ToArray();

        Assert.True(
            mortes.Length == 0,
            "Ces exemptions ne désignent plus aucun type des trois assemblages :\n  "
                + string.Join("\n  ", mortes)
                + "\n\nSoit le type a été renommé — corriger l'exemption — soit il a disparu, "
                + "et l'exemption doit disparaître avec lui."
        );
    }

    /// <summary>
    /// Les QUATRE voies par lesquelles un type peut tenir le contexte :
    /// paramètre de constructeur, propriété, champ, et **paramètre de
    /// méthode**. En couvrir trois sur quatre laisserait la quatrième ouverte,
    /// et c'est celle-là qu'on utiliserait.
    ///
    /// La quatrième a été AJOUTÉE À LA TÂCHE 9, sur un trou signalé à la tâche
    /// 8 : <c>void Poser(PalierDbContext contexte)</c> passait intégralement.
    /// C'est la forme la plus naturelle d'un contournement — on ne l'injecte
    /// pas, on se le fait passer.
    /// </summary>
    [Fact]
    public void AUCUN_type_d_entrainement_ne_prend_le_SEXE_en_dependance()
    {
        // LE GARDE-FOU LE PLUS CONTRE-INTUITIF DU DEPOT : il protege une
        // ABSENCE.
        //
        // L'etude du 24/08/2026 — quatorze axes, trente-deux agents, chaque axe
        // conteste par un sceptique — conclut sans nuance : aucune difference
        // liee au sexe ne justifie deux programmes. Ni le choix des exercices,
        // ni la charge relative, ni la plage de repetitions, ni le nombre de
        // series, ni la frequence, ni la progression, ni les temps de repos.
        //
        // Les nuls reposent sur des effectifs tres superieurs a ceux des
        // differences alleguees : 7 289 personnes pour la relation
        // charge-repetitions, 78 etudes pour le cycle menstruel, contre n = 42
        // pour l'ecart de plus grande ampleur du dossier.
        //
        // POURQUOI UNE EPREUVE PLUTOT QU'UN COMMENTAIRE. Brancher le sexe sur
        // une decision d'entrainement ne casserait rien, ne leverait rien, et
        // passerait toutes les autres epreuves. Le defaut serait invisible au
        // compilateur et visible seulement a l'ecran, sous la forme d'un
        // stereotype que le produit aurait fabrique lui-meme.
        //
        // Ce que l'epreuve N'INTERDIT PAS : le sexe reste legitime cote
        // NUTRITION — Mifflin-St Jeor porte un terme de sexe de 166 kcal, et
        // `Palier.Domain.Depense` comme `Palier.Domain.Objectifs` le lisent a
        // bon droit. La frontiere est l'entrainement.
        var fautifs = new List<string>();

        foreach (var assemblage in _assemblages)
        {
            foreach (var type in assemblage.GetTypes())
            {
                if (
                    type.Namespace is null
                    || !type.Namespace.Contains("Entrainement", StringComparison.Ordinal)
                )
                {
                    continue;
                }

                foreach (var voie in VoiesVersLeSexe(type))
                {
                    fautifs.Add($"{type.FullName} ({voie})");
                }
            }
        }

        Assert.True(
            fautifs.Count == 0,
            "Ces types d'entraînement prennent le SEXE en dépendance :\n  "
                + string.Join("\n  ", fautifs)
                + "\n\nAucune différence liée au sexe ne justifie d'adapter un programme — "
                + "c'est la conclusion de l'étude du 24/08/2026, et elle est mieux étayée "
                + "que n'importe quelle différence alléguée.\n"
                + "Faire dépendre une décision d'entraînement du sexe FABRIQUERAIT une "
                + "différence que la littérature ne soutient pas.\n"
                + "Le sexe reste légitime côté NUTRITION : Mifflin-St Jeor en dépend."
        );
    }

    [Fact]
    public void Le_garde_fou_du_sexe_REGARDE_bien_quelque_chose()
    {
        // Quatrieme question du franchissement — ruling P12. Sans cette
        // assertion, l'epreuve ci-dessus passerait au vert le jour ou le filtre
        // sur « Entrainement » cesserait de trouver le moindre type : elle
        // n'inspecterait plus rien, et le dirait en silence.
        var inspectes = _assemblages
            .SelectMany(a => a.GetTypes())
            .Count(t =>
                t.Namespace is not null
                && t.Namespace.Contains("Entrainement", StringComparison.Ordinal)
            );

        Assert.True(
            inspectes > 10,
            $"Le garde-fou du sexe n'a inspecté que {inspectes} type(s) d'entraînement."
        );

        // Et la CIBLE doit exister. Si `Sexe` etait renomme ou deplace,
        // `EstLeSexe` ne reconnaitrait plus rien et l'epreuve deviendrait un
        // decor — le piege que ce depot ferme partout ailleurs.
        Assert.True(
            EstLeSexe(typeof(Palier.Domain.Grandeurs.Sexe)),
            "`EstLeSexe` ne reconnaît plus le type `Sexe` : le garde-fou ne garde plus rien."
        );
    }

    /// <summary>Le type que l'entraînement ne doit jamais atteindre.</summary>
    private static bool EstLeSexe(Type type)
    {
        var nu = Nullable.GetUnderlyingType(type) ?? type;
        return nu == typeof(Palier.Domain.Grandeurs.Sexe);
    }

    /// <summary>
    /// Les voies par lesquelles un type atteint le sexe.
    /// </summary>
    /// <remarks>
    /// Même parcours que <see cref="Voies"/> — constructeurs, propriétés,
    /// champs, méthodes — parce qu'une dépendance se prend par n'importe
    /// laquelle, et qu'en oublier une suffit à rendre le contrôle décoratif.
    /// </remarks>
    private static IEnumerable<string> VoiesVersLeSexe(Type type)
    {
        const BindingFlags tous =
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

        foreach (var constructeur in type.GetConstructors(tous))
        {
            foreach (var parametre in constructeur.GetParameters())
            {
                if (EstLeSexe(parametre.ParameterType))
                {
                    yield return $"constructeur, paramètre « {parametre.Name} »";
                }
            }
        }

        foreach (var propriete in type.GetProperties(tous))
        {
            if (EstLeSexe(propriete.PropertyType))
            {
                yield return $"propriété « {propriete.Name} »";
            }
        }

        foreach (var champ in type.GetFields(tous))
        {
            if (EstLeSexe(champ.FieldType) && !champ.Name.Contains('<', StringComparison.Ordinal))
            {
                yield return $"champ « {champ.Name} »";
            }
        }

        foreach (var methode in type.GetMethods(tous))
        {
            if (methode.IsSpecialName || methode.DeclaringType != type)
            {
                continue;
            }

            if (EstLeSexe(methode.ReturnType))
            {
                yield return $"méthode « {methode.Name} », type de retour";
            }

            foreach (var parametre in methode.GetParameters())
            {
                if (EstLeSexe(parametre.ParameterType))
                {
                    yield return $"méthode « {methode.Name} », paramètre « {parametre.Name} »";
                }
            }
        }
    }

    private static IEnumerable<string> Voies(Type type)
    {
        const BindingFlags tous =
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

        foreach (var constructeur in type.GetConstructors(tous))
        {
            foreach (var parametre in constructeur.GetParameters())
            {
                if (EstLeContexte(parametre.ParameterType))
                {
                    yield return $"constructeur, paramètre « {parametre.Name} »";
                }
            }
        }

        foreach (var propriete in type.GetProperties(tous))
        {
            if (EstLeContexte(propriete.PropertyType))
            {
                yield return $"propriété « {propriete.Name} »";
            }
        }

        foreach (var champ in type.GetFields(tous))
        {
            // Les champs de sauvegarde d'un paramètre primaire portent un nom
            // que le compilateur choisit ; le constructeur les a déjà signalés,
            // et les compter deux fois brouillerait le message.
            if (EstLeContexte(champ.FieldType) && !champ.Name.Contains('<', StringComparison.Ordinal))
            {
                yield return $"champ « {champ.Name} »";
            }
        }

        foreach (var methode in type.GetMethods(tous))
        {
            // `IsSpecialName` écarte les accesseurs de propriété : `set_X` prend
            // bien un paramètre du type, mais la propriété a déjà été signalée
            // juste au-dessus, et la compter deux fois brouillerait le message.
            if (methode.IsSpecialName || methode.DeclaringType != type)
            {
                continue;
            }

            foreach (var parametre in methode.GetParameters())
            {
                if (EstLeContexte(parametre.ParameterType))
                {
                    yield return $"méthode « {methode.Name} », paramètre « {parametre.Name} »";
                }
            }
        }
    }

    /// <summary>
    /// `IsAssignableFrom` et non l'égalité : un type dérivé de
    /// <c>PalierDbContext</c> donnerait exactement les mêmes pouvoirs.
    /// </summary>
    private static bool EstLeContexte(Type type) =>
        typeof(PalierDbContext).IsAssignableFrom(type)
        // Lot 4 : `PalierAuthDbContext` n'hérite PAS de `PalierDbContext` — il
        // n'expose délibérément aucune table de donnée de santé. Sans cette
        // seconde branche, un contexte neuf échapperait au contrôle par
        // construction : on aurait ouvert une seconde porte en croyant n'en
        // surveiller qu'une.
        || typeof(PalierAuthDbContext).IsAssignableFrom(type)
        // D59 : une FABRIQUE de contexte est une voie vers le contexte, et
        // c'était un trou. Un type qui prend `IDbContextFactory<PalierDbContext>`
        // atteint exactement les mêmes tables, sans identité, et passait ce
        // contrôle sans être vu. Le trou n'était pas théorique : le chargement
        // du trousseau en a besoin, et une fabrique est justement ce qu'un
        // service singleton emploie pour tenir un contexte à durée de requête.
        || (
            type.IsGenericType
            && type.GetGenericTypeDefinition() == typeof(IDbContextFactory<>)
            && EstLeContexte(type.GetGenericArguments()[0])
        );
}
