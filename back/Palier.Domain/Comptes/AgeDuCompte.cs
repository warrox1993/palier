namespace Palier.Domain.Comptes;

/// <summary>
/// Ce que l'âge déclaré ouvre, et ce qu'il ferme.
/// </summary>
/// <remarks>
/// <para>
/// <b>La règle vient de <c>docs/13-juridique.md</c> § 1, et elle n'est pas
/// négociable</b> : « âge minimum 16 ans, partie nutrition verrouillée en
/// dessous de 18 ans ».
/// </para>
///
/// <para>
/// Son motif est écrit dans le même document : « Une application de comptage
/// calorique accessible à un adolescent est le pire scénario possible,
/// éthiquement et juridiquement. » En Belgique, l'âge du consentement numérique
/// est de 13 ans, mais un service traitant des données de santé avec suivi du
/// poids relève d'un autre régime.
/// </para>
///
/// <para>
/// <b>Pourquoi la règle vit dans le DOMAINE et non dans une route.</b> Elle
/// gouverne au moins quatre familles de points d'entrée — poids, nutrition,
/// progression corporelle, inscription. Écrite quatre fois, elle divergerait au
/// premier changement, et c'est la branche oubliée qui laisserait passer un
/// mineur. Ici elle est écrite une fois et éprouvée une fois.
/// </para>
/// </remarks>
public static class AgeDuCompte
{
    /// <summary>L'âge en dessous duquel l'inscription est refusée.</summary>
    public const int AgeMinimal = 16;

    /// <summary>L'âge à partir duquel la nutrition et le poids s'ouvrent.</summary>
    public const int AgeDeLaMajorite = 18;

    /// <summary>
    /// L'âge révolu à une date donnée.
    /// </summary>
    /// <remarks>
    /// <b>L'anniversaire compte, pas la différence d'années.</b> Quelqu'un né le
    /// 31 décembre 2010 n'a pas seize ans le 1er janvier 2026 : il les aura le
    /// 31 décembre. Soustraire les années donnerait seize, et ouvrirait un
    /// compte à quelqu'un de quinze ans — la faute exacte que cette règle
    /// existe pour empêcher.
    /// </remarks>
    public static int AgeRevolu(DateOnly naissance, DateOnly aujourdHui)
    {
        var age = aujourdHui.Year - naissance.Year;

        // L'anniversaire n'est pas encore passé cette année.
        if (aujourdHui < naissance.AddYears(age))
        {
            age--;
        }

        return age;
    }

    /// <summary>Ce que le compte a le droit de faire.</summary>
    public static AccesDuCompte Accorder(DateOnly naissance, DateOnly aujourdHui)
    {
        // UNE DATE DANS LE FUTUR N'EST PAS UN ÂGE. Elle produirait un âge
        // négatif, donc un refus — le bon résultat, mais pour la mauvaise
        // raison, et le message dirait « trop jeune » à quelqu'un qui s'est
        // trompé d'année. Le refus est nommé.
        if (naissance > aujourdHui)
        {
            return AccesDuCompte.DateInvalide;
        }

        var age = AgeRevolu(naissance, aujourdHui);

        // Une date absurdement ancienne est une faute de saisie, pas une
        // longévité. Cent trente ans dépasse le record humain documenté.
        if (age > 130)
        {
            return AccesDuCompte.DateInvalide;
        }

        if (age < AgeMinimal)
        {
            return AccesDuCompte.Refuse;
        }

        return age < AgeDeLaMajorite ? AccesDuCompte.EntrainementSeul : AccesDuCompte.Complet;
    }
}

/// <summary>Les trois paliers de <c>docs/13-juridique.md</c> § 1.</summary>
public enum AccesDuCompte
{
    /// <summary>La date déclarée n'est pas une date de naissance plausible.</summary>
    DateInvalide,

    /// <summary>Moins de seize ans : refus d'inscription.</summary>
    Refuse,

    /// <summary>
    /// Seize ou dix-sept ans : entraînement uniquement. Nutrition, poids et
    /// progression corporelle désactivés.
    /// </summary>
    EntrainementSeul,

    /// <summary>Dix-huit ans ou plus.</summary>
    Complet,
}
