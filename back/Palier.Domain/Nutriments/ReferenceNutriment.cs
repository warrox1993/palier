using Palier.Domain.Grandeurs;

namespace Palier.Domain.Nutriments;

/// <summary>
/// Une référence sanitaire pour un nutriment, telle qu'elle arrive de la base.
/// </summary>
/// <remarks>
/// Le domaine ne connaît <b>aucune</b> valeur de limite haute : il reçoit cette
/// référence et compare. Le motif est mesuré : la limite de la vitamine B6 est
/// passée de 25 à 12 mg en 2023, celle du sélénium de 300 à 255 µg, et le fer a
/// perdu la sienne. Un produit qui aurait figé la B6 à 25 mg laisserait passer
/// sans rien dire un apport de 20 mg, soit 167 % de la limite en vigueur.
///
/// La valeur seule ne suffit pas à comparer : la <b>forme chimique</b> change
/// la limite — la niacine vaut 10 mg en acide nicotinique et 900 mg en
/// nicotinamide, un facteur 90 sur la même ligne d'étiquette — et la
/// <b>source</b> change le périmètre, l'UL du magnésium ne valant que pour les
/// sels solubles des compléments. Ces deux colonnes n'existent pas encore dans
/// le schéma ; leur ajout est une décision qui appartient au porteur du projet.
/// </remarks>
public sealed record ReferenceNutriment
{
    public ReferenceNutriment(
        string cle,
        StatutReference statut,
        MasseNutriment? valeur,
        string source,
        int? annee)
    {
        if (string.IsNullOrWhiteSpace(cle))
        {
            throw new ArgumentException("La clé du nutriment est obligatoire.", nameof(cle));
        }

        // Un statut qui annonce une valeur sans en porter une ferait comparer
        // contre rien tout en croyant comparer. L'incohérence se refuse à la
        // construction, pas au moment de s'en servir.
        var statutPorteUneValeur =
            statut is StatutReference.LimiteHauteEtablie or StatutReference.NiveauSurDApport;

        if (statutPorteUneValeur && valeur is null)
        {
            throw new ArgumentException(
                "Ce statut annonce une valeur, et aucune n'est fournie.",
                nameof(valeur));
        }

        Cle = cle;
        Statut = statut;
        Valeur = valeur;
        Source = source;
        Annee = annee;
    }

    public string Cle { get; }

    public StatutReference Statut { get; }

    public MasseNutriment? Valeur { get; }

    public string Source { get; }

    public int? Annee { get; }
}
