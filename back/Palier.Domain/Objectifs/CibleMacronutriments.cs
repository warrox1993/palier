using Palier.Domain.Grandeurs;

namespace Palier.Domain.Objectifs;

/// <summary>
/// Les cibles quotidiennes de macronutriments, en grammes.
/// </summary>
/// <remarks>
/// <b>L'hydratation n'y figure plus</b> — elle a rejoint
/// <see cref="CibleDHydratation"/> le 25/08/2026. Sa cible ne dépend pas de la
/// masse corporelle mais du sexe, et la loger ici obligeait à un coefficient
/// par kilogramme qu'aucun référentiel européen ne publie.
/// </remarks>
public sealed record CibleMacronutriments(
    decimal ProteinesG,
    decimal LipidesG,
    decimal GlucidesG,
    decimal FibresG
);

/// <summary>
/// Les références de macronutriments de <c>docs/04-nutrition.md</c> § 2 et
/// § 2 ter : pré-remplies, toujours modifiables, et affichées avec leur
/// fourchette.
/// </summary>
/// <remarks>
/// <para>
/// <b>DEUX NIVEAUX DE SOURCE, et ils ne se contredisent pas.</b> Le socle
/// réglementaire européen — l'ANSES 2016 — répond à « quelle répartition pour
/// une population ». La littérature d'entraînement — Morton 2018 — répond à
/// « combien de protéines pour construire du muscle ». Ce sont deux questions
/// distinctes, et les mélanger est ce qui produit des chiffres ininterprétables.
/// </para>
///
/// <para>
/// Le socle gouverne la <b>contrainte</b> : <see cref="PartGlucidiqueMinimale"/>
/// vient de l'ANSES. La littérature gouverne la <b>demande</b> : les fourchettes
/// protéiques viennent de Morton. La contrainte peut réduire la demande, jamais
/// l'inverse.
/// </para>
/// </remarks>
public static class Macronutriments
{
    /// <summary>
    /// Borne basse des protéines, en g/kg.
    /// </summary>
    /// <remarks>
    /// La méta-analyse de Morton 2018 — 49 études, 1 863 participants — place
    /// le point d'inflexion à 1,62 g/kg : au-delà, la supplémentation ne
    /// produit plus de gain de masse maigre. La borne haute de 2,2 est le
    /// sommet de l'intervalle de confiance à 95 % autour de ce point, et non
    /// un plafond physiologique.
    ///
    /// <para>
    /// <b>Ces bornes viennent d'une MÉTA-ANALYSE, pas d'un référentiel.</b>
    /// L'EFSA ne fixe aucune limite haute protéique et considère « twice the
    /// PRI » — 1,66 g/kg — comme sûr. 2,2 g/kg n'est donc pas un dépassement
    /// de limite, mais aucun référentiel européen ne l'endosse non plus.
    /// L'écran doit porter cette distinction.
    /// </para>
    /// </remarks>
    public static decimal ProteinesParKgMin => 1.6m;

    /// <summary>Le défaut, au-dessus du point d'inflexion de Morton.</summary>
    public static decimal ProteinesParKgDefaut => 1.8m;

    /// <summary>Le sommet de l'intervalle de confiance de Morton.</summary>
    public static decimal ProteinesParKgMax => 2.2m;

    /// <summary>
    /// Borne basse des lipides, en g/kg.
    /// </summary>
    /// <remarks>
    /// <b>AUCUNE SOURCE À CE JOUR.</b> L'audit du 25/08/2026 l'a relevé : ces
    /// bornes ne sont adossées à aucun référentiel, et tous les référentiels
    /// expriment les lipides en pourcentage d'énergie, jamais en g/kg.
    ///
    /// <para>
    /// La forme par kilogramme fait dériver silencieusement la part
    /// d'énergie : 1,0 g/kg vaut 33,7 % de l'énergie à 1 949 kcal et 24 % à
    /// 3 000 kcal <b>pour la même personne</b>. C'est le mécanisme même du
    /// défaut que <see cref="Repartir"/> corrige. Les réexprimer en pourcentage
    /// d'énergie est une décision ouverte — § 7 de <c>04-nutrition.md</c>.
    /// </para>
    /// </remarks>
    public static decimal LipidesParKgMin => 0.8m;

    public static decimal LipidesParKgDefaut => 1.0m;

    public static decimal LipidesParKgMax => 1.2m;

    /// <summary>Borne basse des fibres, en grammes.</summary>
    /// <remarks>
    /// 25 g est l'apport adéquat de l'EFSA, fondé sur le seul critère du
    /// transit intestinal normal. L'EFSA reconnaît par ailleurs un bénéfice
    /// au-delà, et l'IOM pose 38 g pour l'homme. 25-35 g est donc une
    /// <b>cible de planification</b> adossée à ces trois éléments, et non un
    /// intervalle EFSA. Aucune limite haute n'existe pour les fibres.
    /// </remarks>
    public static decimal FibresGMin => 25m;

    public static decimal FibresGDefaut => 30m;

    public static decimal FibresGMax => 35m;

    /// <summary>
    /// La part minimale de l'énergie que les glucides conservent.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>ANSES 2016 : 40 à 55 % de l'énergie.</b> C'est la borne basse de
    /// l'intervalle de référence français, retenu comme socle. L'EFSA pose
    /// 45-60 % ; les deux sont légitimes pour un public belge, et le choix est
    /// consigné plutôt que moyenné.
    /// </para>
    ///
    /// <para>
    /// <b>C'est une CIBLE DE PLANIFICATION, jamais un seuil de sécurité.</b>
    /// L'EFSA a explicitement échoué à en fixer un : « data are not sufficient
    /// to define a Lower Threshold of Intake for carbohydrates », et « only a
    /// Reference Intake range can be given ». En sortir produit un état
    /// affiché, et ne fait jamais refuser un plan — refuser de servir est un
    /// acte de niveau sécurité, et l'adosser à une valeur dont la source dit
    /// qu'elle n'en est pas une serait exactement l'inversion de grandeur que
    /// tout ce document combat.
    /// </para>
    ///
    /// <para>
    /// <b>Ce n'est PAS 130 g/jour</b> — la RDA américaine, que l'EFSA cite puis
    /// disqualifie dans la phrase suivante : « these levels of intake are not
    /// sufficient to meet energy needs ».
    /// </para>
    /// </remarks>
    public static decimal PartGlucidiqueMinimale => 0.40m;

    private static decimal KcalParGrammeDeProteine => 4m;

    private static decimal KcalParGrammeDeLipide => 9m;

    private static decimal KcalParGrammeDeGlucide => 4m;

    /// <summary>
    /// Répartit l'énergie entre les trois macronutriments, sous contrainte.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>AUCUN POSTE N'EST LE RESTE.</b> Jusqu'au 25/08/2026, les glucides
    /// étaient ce qui restait après les protéines et les lipides — et rien ne
    /// contraignait ces deux-là en part d'énergie. Le reste devenait nul, puis
    /// négatif, sur des profils ordinaires.
    /// </para>
    ///
    /// <para>
    /// Cette méthode n'est pas seulement fautive : elle est <b>supersédée</b>.
    /// L'ANSES décrit ainsi ses propres références antérieures — « la
    /// contribution des glucides à l'AET a été définie pour compléter les
    /// apports énergétiques au-delà des apports en lipides et protéines » — et
    /// les a remplacées en 2016 par un intervalle. L'EFSA écrit que les valeurs
    /// glucidiques « cannot be made without considering other energy delivering
    /// macronutrients ».
    /// </para>
    ///
    /// <para>
    /// <b>La non-négativité devient un théorème, pas une garde.</b> Comme la
    /// part non glucidique ne peut pas dépasser
    /// <c>1 − PartGlucidiqueMinimale</c> par construction, les glucides valent
    /// toujours au moins cette part. Deux interdits en découlent : jamais de
    /// <c>Math.Max(0, reste)</c>, qui masquerait la brèche en livrant un plan
    /// dont les macros ne somment plus aux calories ; et jamais de levée sur un
    /// profil d'utilisateur banal.
    /// </para>
    ///
    /// <para>
    /// <b>L'ordre de cession n'est pas arbitraire.</b> Les lipides cèdent
    /// d'abord, les protéines ensuite : sous restriction énergétique, la
    /// littérature indique de préserver les protéines pour protéger la masse
    /// maigre. Et l'EFSA assouplit elle-même sa borne lipidique haute selon le
    /// niveau d'activité physique — « intakes > 35 E% may be compatible with
    /// both good health and normal body weight depending on […] the level of
    /// physical activity ».
    /// </para>
    /// </remarks>
    public static RepartitionDesMacronutriments Repartir(
        Masse masse,
        Energie apportCible,
        decimal? proteinesParKg = null,
        decimal? lipidesParKg = null,
        decimal? fibresG = null
    )
    {
        var proteinesDemandees = DansLaFourchette(
            proteinesParKg ?? ProteinesParKgDefaut,
            ProteinesParKgMin,
            ProteinesParKgMax,
            nameof(proteinesParKg)
        );

        var lipidesDemandes = DansLaFourchette(
            lipidesParKg ?? LipidesParKgDefaut,
            LipidesParKgMin,
            LipidesParKgMax,
            nameof(lipidesParKg)
        );

        var fibres = DansLaFourchette(
            fibresG ?? FibresGDefaut,
            FibresGMin,
            FibresGMax,
            nameof(fibresG)
        );

        var kg = masse.Kilogrammes;
        var energie = apportCible.Kilocalories;

        // Le budget que les protéines et les lipides se partagent. Ce qui
        // reste au-delà appartient aux glucides, et cette part ne se négocie
        // pas — c'est elle qui rend la non-négativité démontrable.
        var budget = (1m - PartGlucidiqueMinimale) * energie;

        var proteinesG = proteinesDemandees * kg;
        var lipidesG = lipidesDemandes * kg;

        var issue = IssueDeRepartition.Honoree;

        if ((proteinesG * KcalParGrammeDeProteine) + (lipidesG * KcalParGrammeDeLipide) > budget)
        {
            issue = IssueDeRepartition.DemandeReduite;

            // Les lipides cèdent en premier, jusqu'à leur plancher.
            lipidesG = Math.Max(
                LipidesParKgMin * kg,
                (budget - (proteinesG * KcalParGrammeDeProteine)) / KcalParGrammeDeLipide
            );

            // Les protéines ensuite, si le plancher lipidique n'a pas suffi.
            if ((proteinesG * KcalParGrammeDeProteine) + (lipidesG * KcalParGrammeDeLipide) > budget)
            {
                proteinesG = Math.Max(
                    ProteinesParKgMin * kg,
                    (budget - (lipidesG * KcalParGrammeDeLipide)) / KcalParGrammeDeProteine
                );
            }

            // Aux deux planchers, la répartition n'existe toujours pas. Ce
            // n'est pas un refus de sécurité : c'est une impossibilité
            // arithmétique, et la question porte sur l'ÉNERGIE, pas sur les
            // macronutriments.
            if ((proteinesG * KcalParGrammeDeProteine) + (lipidesG * KcalParGrammeDeLipide) > budget)
            {
                return new RepartitionDesMacronutriments(
                    IssueDeRepartition.Impossible,
                    null,
                    proteinesDemandees,
                    lipidesDemandes
                );
            }
        }

        var glucidesG =
            (energie - (proteinesG * KcalParGrammeDeProteine) - (lipidesG * KcalParGrammeDeLipide))
            / KcalParGrammeDeGlucide;

        return new RepartitionDesMacronutriments(
            issue,
            new CibleMacronutriments(proteinesG, lipidesG, glucidesG, fibres),
            proteinesDemandees,
            lipidesDemandes
        );
    }

    private static decimal DansLaFourchette(decimal valeur, decimal min, decimal max, string nom) =>
        valeur >= min && valeur <= max
            ? valeur
            : throw new ArgumentOutOfRangeException(nom, valeur, null);
}
