namespace Palier.Domain.Grandeurs;

/// <summary>
/// Un pourcentage de masse grasse, indissociable de la façon dont il a été
/// obtenu.
/// </summary>
/// <remarks>
/// La provenance voyage avec la valeur plutôt qu'à côté d'elle : un appelant
/// qui doit penser à transporter les deux finit par n'en transporter qu'une.
/// </remarks>
public readonly record struct PourcentageMasseGrasse
{
    private PourcentageMasseGrasse(decimal pourcentage, ProvenanceMesure provenance)
    {
        Pourcentage = pourcentage;
        Provenance = provenance;
    }

    public decimal Pourcentage { get; }

    public ProvenanceMesure Provenance { get; }

    public static PourcentageMasseGrasse De(decimal pourcentage, ProvenanceMesure provenance)
    {
        if (pourcentage is <= 0m or >= 100m)
        {
            throw new ArgumentOutOfRangeException(nameof(pourcentage), pourcentage, null);
        }

        if (!Enum.IsDefined(provenance))
        {
            throw new ArgumentOutOfRangeException(nameof(provenance), provenance, null);
        }

        return new PourcentageMasseGrasse(pourcentage, provenance);
    }

    /// <summary>La masse maigre correspondante, seule entrée de Katch-McArdle.</summary>
    public Masse MasseMaigreDe(Masse masseTotale) =>
        Masse.DepuisKilogrammes(masseTotale.Kilogrammes * (100m - Pourcentage) / 100m);
}
