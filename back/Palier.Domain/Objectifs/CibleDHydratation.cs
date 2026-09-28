using Palier.Domain.Grandeurs;

namespace Palier.Domain.Objectifs;

/// <summary>
/// La cible d'hydratation — <b>en boissons</b>, et elle le dit.
/// </summary>
/// <remarks>
/// <para>
/// <c>Basse</c> et <c>Haute</c> bornent une fourchette, jamais un chiffre
/// unique : la conversion de l'eau totale vers les boissons vaut 70 à 80 %
/// selon la source, et la dispersion internationale des recommandations va de
/// 1,0 à 3,0 L/j chez l'homme. Il n'existe aucune valeur unique défendable.
/// </para>
///
/// <para>
/// <c>EauDesAlimentsMl</c> est renvoyée pour être <b>affichée</b>, jamais
/// ajoutée : un utilisateur qui a lu « 2,5 litres par jour » ailleurs se
/// croirait en déficit permanent si l'écran ne disait pas ce que le compteur
/// ne compte pas.
/// </para>
/// </remarks>
public sealed record CibleDHydratation(
    VolumeEau Basse,
    VolumeEau Haute,
    VolumeEau EauDesAlimentsMl
);

/// <summary>
/// L'hydratation de <c>docs/04-nutrition.md</c> § 2 bis.
/// </summary>
/// <remarks>
/// <para>
/// <b>LE COEFFICIENT PAR KILOGRAMME A ÉTÉ RETIRÉ le 25/08/2026.</b> Le domaine
/// posait <c>35 ml × poids</c> en se réclamant des apports adéquats de l'EFSA
/// — 2,0 L pour la femme, 2,5 L pour l'homme. Le raisonnement était faux à sa
/// racine : <b>ces valeurs portent sur l'eau TOTALE</b>, boissons et aliments
/// confondus, alors que le produit ne compte que les boissons.
/// </para>
///
/// <para>
/// La seule table qui publie un coefficient de 35 ml/kg est la table D-A-CH,
/// et l'intitulé de sa colonne le dit mot pour mot : <i>« Wasserzufuhr durch
/// Getränke <b>und feste Nahrung</b> »</i> — boissons ET aliments solides. Le
/// coefficient y est de surcroît dérivé de la <b>dépense énergétique</b>
/// (« für einen Energieumsatz von 11,1 MJ »), non de la masse corporelle.
/// </para>
///
/// <para>
/// Conséquence chiffrée : pour 73 kg, l'ancien calcul demandait 2 555 ml de
/// boissons quand l'apport adéquat masculin est de 2 500 ml <b>tout
/// compris</b>. La cible dépassait de plus d'un tiers ce que l'EFSA pose.
/// </para>
///
/// <para>
/// <b>Et une cible gonflée n'est pas une prudence du côté de la sécurité.</b>
/// Le consensus international sur l'hyponatrémie d'effort nomme les
/// « inappropriate hydration recommendations » parmi les causes de l'apport
/// excessif, et désigne comme les plus exposés le sportif <b>récréatif</b> et
/// la <b>femme</b> — soit exactement le public de cette application.
/// </para>
/// </remarks>
public static class Hydratation
{
    /// <summary>
    /// La part de l'eau totale qui vient des boissons, borne basse.
    /// </summary>
    /// <remarks>
    /// L'ESPEN écrit que « drinks or beverages account for 70 to 80 % of fluid
    /// consumed ». <b>La fourchette est reportée ici plutôt que refermée sur
    /// son sommet</b> : 70 % donne la borne basse de la cible, 80 % la borne
    /// haute, qui coïncide avec le plancher de la recommandation R61.
    /// </remarks>
    public static decimal PartDesBoissonsBasse => 0.70m;

    /// <summary>La part haute — celle que l'ESPEN retient pour son plancher.</summary>
    public static decimal PartDesBoissonsHaute => 0.80m;

    /// <summary>
    /// L'apport adéquat d'eau TOTALE de la femme adulte, en millilitres.
    /// </summary>
    /// <remarks>
    /// EFSA 2010, climat tempéré, activité modérée. L'ANSES reprend ces
    /// valeurs telles quelles : « Cet apport satisfaisant concerne toutes les
    /// sources d'eau, c'est-à-dire l'eau de boisson, l'eau présente dans les
    /// autres boissons et l'eau contenue dans les aliments. »
    ///
    /// <para>
    /// <b>Le sexe est la SEULE variable</b> que l'EFSA et l'ANSES retiennent
    /// chez l'adulte. Ni le poids, ni la taille, ni l'âge.
    /// </para>
    /// </remarks>
    public static decimal EauTotaleFemmeMl => 2000m;

    /// <summary>L'apport adéquat d'eau totale de l'homme adulte, en millilitres.</summary>
    public static decimal EauTotaleHommeMl => 2500m;

    /// <summary>
    /// Ce que les aliments solides apportent, en millilitres par jour.
    /// </summary>
    /// <remarks>
    /// Bilan hydrique D-A-CH de l'adulte : boissons 1 440 ml, aliments 875 ml,
    /// eau d'oxydation 335 ml. Cette valeur s'AFFICHE pour dire ce que le
    /// compteur ne compte pas ; elle ne s'ajoute jamais à la cible.
    /// </remarks>
    public static decimal EauDesAlimentsMl => 875m;

    /// <summary>
    /// La majoration d'effort, en millilitres par heure de séance.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>UNE ESTIMATION, PLAFONNÉE, ET QUI NE S'EMPILE PAS.</b> La sudation
    /// va de 0,3 à 2,4 L/h selon l'intensité, la durée, l'acclimatation et
    /// l'environnement. Pour une séance de musculation en salle, le bas de
    /// fourchette est l'ordre de grandeur plausible.
    /// </para>
    ///
    /// <para>
    /// <b>La consigne de référence reste « boire à la soif ».</b> Le consensus
    /// international est explicite : « recommending fixed ranges of fluid
    /// intake is not appropriate […] The most individualized hydration
    /// strategy […] is to drink fluids when thirsty. »
    /// </para>
    /// </remarks>
    public static decimal MajorationParHeureDEffortMl => 500m;

    /// <summary>
    /// Le débit maximal d'excrétion rénale, en millilitres par heure.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>IL N'EXISTE AUCUNE LIMITE HAUTE JOURNALIÈRE POUR L'EAU</b>, et les
    /// deux référentiels le disent : « No maximum daily amount of water that
    /// can be tolerated by a population group can be defined » (EFSA) ; « a
    /// Tolerable Upper Intake Level was not set for water » (IOM, 2005).
    /// </para>
    ///
    /// <para>
    /// Le garde-fou existe, mais c'est un <b>débit</b>, pas un total. Le rein
    /// excrète au plus 0,7 à 1,0 L/h ; la borne basse est retenue.
    /// </para>
    ///
    /// <para>
    /// <b>Ce plafond ne vaut pas tel quel pendant une séance.</b> La sécrétion
    /// non osmotique d'AVP à l'effort bloque l'élimination de l'eau libre, et
    /// l'hyponatrémie d'effort survient chez des sportifs qui n'ont jamais
    /// approché ce débit. Afficher « sous 1 L/h tout va bien » serait faux dans
    /// le contexte même où l'application ajoute ses majorations.
    /// </para>
    /// </remarks>
    public static decimal DebitRenalMaximalMlParHeure => 700m;

    /// <summary>La cible de boissons, à partir du sexe.</summary>
    /// <param name="sexe">
    /// La seule variable que les référentiels retiennent chez l'adulte.
    /// </param>
    public static CibleDHydratation Calculer(Sexe sexe)
    {
        var eauTotale = sexe == Sexe.Homme ? EauTotaleHommeMl : EauTotaleFemmeMl;

        return new CibleDHydratation(
            VolumeEau.DepuisMillilitres(decimal.Round(eauTotale * PartDesBoissonsBasse)),
            VolumeEau.DepuisMillilitres(decimal.Round(eauTotale * PartDesBoissonsHaute)),
            VolumeEau.DepuisMillilitres(EauDesAlimentsMl)
        );
    }
}
