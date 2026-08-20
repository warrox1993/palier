namespace Palier.Application.Pipeline;

// Le contrat du pipeline. Son IMPLÉMENTATION vit dans `Palier.Infrastructure`,
// et c'est un écart assumé au brief de la tâche 8, qui la plaçait ici.
//
// Motif : le sens des dépendances. `Palier.Infrastructure` référence
// `Palier.Application`, jamais l'inverse ; or l'exécuteur ouvre une transaction
// sur `PalierDbContext`, qui vit en infrastructure. Le placer ici obligerait
// soit à inverser la dépendance, soit à faire entrer EF Core dans la couche
// applicative. Le contrat — ce que le reste du code voit — reste ici ; le
// mécanisme est là où vit la base.
//
// Les interfaces portent le préfixe `I`, que `docs/16-projet.md` § 2 interdit.
// La contradiction est CONNUE et portée au plan (question 12c) : les analyseurs
// Roslyn du dépôt, avec `AnalysisLevel: latest-all` et `TreatWarningsAsErrors`,
// EXIGENT ce préfixe (CA1715) et rendent le projet incompilable sans lui. La
// règle `type-prefixe-i` de `scripts/regles-projet.mjs` ne porte que sur `.ts`
// et `.tsx` : rien ne casse aujourd'hui. Ce n'est pas tranché ici.

/// <summary>
/// Qui demande. Rendre <c>null</c> signifie « aucune identité », et c'est un
/// état parfaitement légitime — un appel anonyme, un contrôle de santé, une
/// tâche de fond. Ce qui ne l'est pas, c'est d'exécuter un cas d'usage dans cet
/// état : l'exécuteur refuse.
/// </summary>
public interface IIdentiteDemandeur
{
    /// <summary>L'identifiant de l'utilisateur, ou <c>null</c> si personne n'est identifié.</summary>
    public Guid? Identifiant { get; }
}

/// <summary>
/// Déclare qu'un type a le droit de prendre <c>PalierDbContext</c> en
/// dépendance. Sa seule fonction est d'être VISIBLE par le test d'architecture,
/// qui refuse tout autre type tenant le contexte.
///
/// C'est un attribut et non une interface marqueur : CA1040 refuse les
/// interfaces vides, et une déclaration explicite se lit mieux qu'un héritage
/// dont personne n'appelle jamais les membres.
///
/// C'est la différence entre « le pipeline le fait » et « rien d'autre ne peut
/// le faire ». Un <c>IHostedService</c>, une tâche de fond, un contrôle de santé
/// ou une file de rejeu n'ont pas de transaction, donc pas d'identité : bruyants
/// sur table peuplée, SILENCIEUX sur table vide.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class GestionnaireDeCasDUsageAttribute : Attribute;

/// <summary>
/// Le seul chemin par lequel un cas d'usage touche la base. Il ouvre la
/// transaction, y pose l'identité, et n'en sort qu'au <c>COMMIT</c>.
/// </summary>
public interface IExecuteurDeCasDUsage
{
    /// <typeparam name="T">Ce que le cas d'usage rend.</typeparam>
    /// <param name="nomDuCasDUsage">
    /// Nommé dans le message de refus. Un code SQL nu ne dit pas quel appel a
    /// échoué, et c'est ce message-là qu'on lit à trois heures du matin.
    /// </param>
    /// <param name="corps">Le cas d'usage, exécuté dans la transaction.</param>
    /// <param name="jeton">Jeton d'annulation.</param>
    /// <returns>Ce que <paramref name="corps" /> a rendu, une fois le COMMIT passé.</returns>
    public Task<T> ExecuterAsync<T>(
        string nomDuCasDUsage,
        Func<CancellationToken, Task<T>> corps,
        CancellationToken jeton = default
    );
}

/// <summary>
/// Le refus applicatif, levé AVANT toute ouverture de transaction.
///
/// Il existe parce que le moteur ne peut pas crier sur une table vide : RLS
/// évalue ses politiques « for each row », donc zéro ligne parcourue rend zéro
/// évaluation et aucune exception. Pire, la levée dépendrait du plan choisi par
/// le planificateur — et une propriété de sécurité qui dépend du plan n'est pas
/// une propriété.
///
/// Le message NE PORTE AUCUNE DONNÉE : le nom du cas d'usage, rien d'autre.
/// `01-conformite.md` § 4 interdit toute donnée de santé au journal, et un
/// identifiant d'utilisateur n'y a pas sa place non plus.
/// </summary>
public sealed class IdentiteAbsenteException : InvalidOperationException
{
    public IdentiteAbsenteException(string nomDuCasDUsage)
        : base(
            $"Identité absente : le cas d'usage « {nomDuCasDUsage} » a été appelé sans "
                + "utilisateur. Aucune transaction n'a été ouverte et rien n'a été lu. "
                + "Le moteur ne peut pas signaler cette faute lui-même sur une table vide."
        ) => NomDuCasDUsage = nomDuCasDUsage;

    public IdentiteAbsenteException()
        : base("Identité absente.") => NomDuCasDUsage = "(inconnu)";

    public IdentiteAbsenteException(string message, Exception innerException)
        : base(message, innerException) => NomDuCasDUsage = "(inconnu)";

    /// <summary>Le cas d'usage fautif, pour qu'un appelant puisse l'assertionner sans lire le message.</summary>
    public string NomDuCasDUsage { get; }
}
