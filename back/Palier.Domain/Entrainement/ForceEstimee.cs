using Palier.Domain.Grandeurs;

namespace Palier.Domain.Entrainement;

/// <summary>
/// La confiance qu'on peut accorder à une force estimée.
/// </summary>
/// <remarks>
/// Les paliers suivent la littérature : l'erreur de prédiction reste sous 0,03
/// entre 3 et 8 répétitions et l'exactitude est maximale au 5RM ; elle se
/// dégrade au-delà de 10 ; et « beyond 12 reps, prediction error increases
/// significantly ».
/// </remarks>
public enum Fiabilite
{
    /// <summary>Jusqu'à 8 répétitions effectives.</summary>
    Bonne,

    /// <summary>De 9 à 12 répétitions effectives.</summary>
    Moyenne,

    /// <summary>De 13 à 15 répétitions effectives. À signaler à l'affichage.</summary>
    Faible,
}

/// <summary>
/// Une force maximale estimée, indissociable de la confiance qu'on peut lui
/// accorder.
/// </summary>
public sealed record EstimationForce(Charge UnRepetitionMaximum, Fiabilite Fiabilite);

/// <summary>
/// La force maximale estimée par Epley, corrigée du RIR.
/// </summary>
/// <remarks>
/// <c>docs/05-entrainement.md</c> § 3 : « aucun test de charge maximale n'est
/// jamais proposé par le produit. Les tissus conjonctifs s'adaptent en
/// semaines, le système nerveux en séances : c'est précisément l'écart qui
/// blesse les pratiquants en reprise. » L'estimation existe pour éviter le
/// test, pas pour le préparer.
/// </remarks>
public static class ForceEstimee
{
    /// <summary>Au-delà, la fiabilité passe de bonne à moyenne.</summary>
    public static int RepetitionsEffectivesFiables => 8;

    /// <summary>Au-delà, la fiabilité passe de moyenne à faible.</summary>
    public static int RepetitionsEffectivesMoyennes => 12;

    /// <summary>Au-delà, plus rien n'est rendu.</summary>
    public static int RepetitionsEffectivesMaximum => 15;

    /// <summary>
    /// Le 1RM estimé, ou <c>null</c> quand la série est trop longue pour qu'une
    /// estimation ait un sens.
    /// </summary>
    /// <remarks>
    /// Rendre une charge malgré tout aurait été indiscernable d'une estimation
    /// valide une fois sortie d'ici. Le <c>null</c> est le seul retour honnête.
    ///
    /// Le RIR absent vaut 2 — « hypothèse prudente » du document, devenue la
    /// recommandation de référence du Position Stand 2026 de l'ACSM.
    /// </remarks>
    public static EstimationForce? Epley(Charge charge, Repetitions repetitions, Rir? rir)
    {
        if (charge.Kilogrammes <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(charge), charge.Kilogrammes, null);
        }

        var effectives = repetitions.Nombre + (rir ?? Rir.ParDefaut).Nombre;

        if (effectives > RepetitionsEffectivesMaximum)
        {
            return null;
        }

        var unRepetitionMaximum = charge.Kilogrammes * (1m + (effectives / 30m));

        var fiabilite = effectives switch
        {
            var e when e <= RepetitionsEffectivesFiables => Fiabilite.Bonne,
            var e when e <= RepetitionsEffectivesMoyennes => Fiabilite.Moyenne,
            _ => Fiabilite.Faible,
        };

        return new EstimationForce(Charge.DepuisKilogrammes(unRepetitionMaximum), fiabilite);
    }
}
