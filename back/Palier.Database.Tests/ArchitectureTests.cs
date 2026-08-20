using System.Reflection;
using Palier.Application.Pipeline;
using Palier.Infrastructure;

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
                + "Si ce type EST un cas d'usage, qu'il porte `[GestionnaireDeCasDUsage]`."
        );
    }

    /// <summary>
    /// Les trois voies par lesquelles un type peut tenir le contexte :
    /// paramètre de constructeur, propriété, champ. En couvrir deux sur trois
    /// laisserait la troisième ouverte, et c'est celle-là qu'on utiliserait.
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
    }

    /// <summary>
    /// `IsAssignableFrom` et non l'égalité : un type dérivé de
    /// <c>PalierDbContext</c> donnerait exactement les mêmes pouvoirs.
    /// </summary>
    private static bool EstLeContexte(Type type) => typeof(PalierDbContext).IsAssignableFrom(type);
}
