namespace Palier.Domain.Entrainement;

/// <summary>
/// Le rôle d'un mouvement dans l'équilibre tirage / poussée.
/// </summary>
public enum RoleMouvement
{
    Tirage,
    Poussee,

    /// <summary>
    /// Ni l'un ni l'autre. <c>docs/05-entrainement.md</c> § 4 exclut
    /// explicitement le deltoïde latéral du ratio : « il ne tire ni ne pousse ».
    /// </summary>
    NiTirageNiPoussee,
}

/// <summary>Une série effectuée, réduite à ce que le calcul de volume consomme.</summary>
public sealed record SerieEffectuee(string GroupeMusculaire, RoleMouvement Role, DateOnly Jour);

/// <summary>
/// Le bilan de volume d'une semaine glissante.
/// </summary>
/// <remarks>
/// <see cref="RatioTiragePoussee" /> vaut <c>null</c> quand aucune poussée n'a
/// été enregistrée : une division par zéro n'est pas un ratio infini, c'est une
/// absence de mesure, et le produit ne peut rien dire d'un ratio qu'il n'a pas.
/// </remarks>
public sealed record BilanVolume(
    IReadOnlyDictionary<string, int> SeriesParGroupe,
    decimal? RatioTiragePoussee,
    bool? RatioSousLaCible,
    IReadOnlyCollection<string> GroupesSousLaCible);

/// <summary>
/// Le volume d'entraînement par groupe musculaire, sur sept jours glissants.
/// </summary>
public static class VolumeParGroupe
{
    /// <summary>
    /// Compte les séries, calcule le ratio et nomme les groupes sous la cible.
    /// </summary>
    /// <remarks>
    /// Les deux références arrivent en paramètre, et c'est délibéré. La cible de
    /// séries vient du Position Stand 2026 de l'ACSM — bâti sur 137 revues
    /// systématiques et plus de 30 000 participants — qui établit qu'environ dix
    /// séries hebdomadaires par groupe sont nécessaires à l'hypertrophie, avec
    /// une relation dose-réponse au-delà.
    ///
    /// Le ratio tirage/poussée, lui, est un repère d'équilibre et <b>non un
    /// seuil de santé</b> : les recommandations publiées vont de 1:1 à 3:1 et la
    /// pertinence même d'un ratio fixe est débattue. Le figer en constante
    /// laisserait croire qu'il en existe une valeur juste.
    /// </remarks>
    public static BilanVolume SurSeptJours(
        IReadOnlyCollection<SerieEffectuee> series,
        DateOnly finDeFenetre,
        int cibleSeriesParGroupe,
        decimal ratioTiragePousseeCible)
    {
        ArgumentNullException.ThrowIfNull(series);

        if (cibleSeriesParGroupe <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(cibleSeriesParGroupe),
                cibleSeriesParGroupe,
                null);
        }

        if (ratioTiragePousseeCible <= 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(ratioTiragePousseeCible),
                ratioTiragePousseeCible,
                null);
        }

        var debut = finDeFenetre.AddDays(-6);
        var retenues = new List<SerieEffectuee>();

        foreach (var serie in series)
        {
            if (string.IsNullOrWhiteSpace(serie.GroupeMusculaire))
            {
                throw new ArgumentException(
                    "Une série sans groupe musculaire ne peut pas être comptée.",
                    nameof(series));
            }

            if (!Enum.IsDefined(serie.Role))
            {
                throw new ArgumentOutOfRangeException(nameof(series), serie.Role, null);
            }

            if (serie.Jour >= debut && serie.Jour <= finDeFenetre)
            {
                retenues.Add(serie);
            }
        }

        var parGroupe = retenues
            .GroupBy(s => s.GroupeMusculaire, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);

        var tirages = retenues.Count(s => s.Role == RoleMouvement.Tirage);
        var poussees = retenues.Count(s => s.Role == RoleMouvement.Poussee);

        decimal? ratio = poussees == 0 ? null : (decimal)tirages / poussees;

        return new BilanVolume(
            parGroupe,
            ratio,
            ratio is null ? null : ratio < ratioTiragePousseeCible,
            parGroupe
                .Where(kv => kv.Value < cibleSeriesParGroupe)
                .Select(kv => kv.Key)
                .ToArray());
    }
}
