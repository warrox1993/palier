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
    private static readonly string[] _exemptionsNommees =
    [
        "Palier.Api.Socle.LecteurDeSocle",
        "Palier.Infrastructure.Identite.MagasinDeSessions",
    ];

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
        || typeof(PalierAuthDbContext).IsAssignableFrom(type);
}
