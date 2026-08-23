namespace Palier.Application.Entrainement;

/// <summary>Ce qu'on donne pour ouvrir une séance. Tout est facultatif.</summary>
/// <param name="Debut">
/// Absent, c'est maintenant. On le laisse fournir pour qu'une séance saisie
/// après coup — le soir, de mémoire — porte l'heure où elle a EU LIEU, et non
/// celle où on l'a tapée : le volume hebdomadaire compte par semaine, et une
/// séance du dimanche saisie le lundi changerait de semaine.
/// </param>
/// <param name="HeuresDeSommeil">Ce que la nuit précédente a donné, si l'utilisateur le note.</param>
/// <param name="Note">Un texte libre. Il n'atteint AUCUN calcul et AUCUN modèle.</param>
public sealed record OuvertureDeSeance(
    DateTimeOffset? Debut,
    decimal? HeuresDeSommeil,
    string? Note
);

/// <summary>Ce qu'on donne pour clore une séance.</summary>
public sealed record ClotureDeSeance(DateTimeOffset? Fin, int? Energie)
{
    /// <summary>Les bornes du <c>CHECK</c> écrit dans la migration du socle.</summary>
    /// <remarks>
    /// Elles sont ici, en <c>const int</c>, et non recopiées dans le point
    /// d'entrée : le jour où la migration change l'échelle, un seul endroit
    /// bouge. <c>const</c> et non <c>static readonly</c> — un
    /// <c>const decimal</c> engendrerait un constructeur statique que rien
    /// n'exécute, et le seuil de couverture le refuserait ; sur <c>int</c>, la
    /// valeur est inscrite à la compilation.
    /// </remarks>
    public const int EnergieMinimale = 1;

    /// <inheritdoc cref="EnergieMinimale" />
    public const int EnergieMaximale = 5;

    /// <summary>
    /// Vrai si l'énergie est absente ou dans les bornes. ABSENTE EST VALIDE :
    /// clore une séance sans noter son énergie doit rester possible, sans quoi
    /// l'utilisateur pressé laisse ses séances ouvertes.
    /// </summary>
    /// <remarks>
    /// Le contrôle vit ICI plutôt que de laisser le <c>CHECK</c> parler : une
    /// violation de contrainte remonterait en 500, c'est-à-dire en « le serveur
    /// a un défaut » là où l'utilisateur a simplement tapé 6.
    /// </remarks>
    public bool EnergieValide => Energie is null or (>= EnergieMinimale and <= EnergieMaximale);
}

/// <summary>Une séance, telle qu'elle sort de l'API.</summary>
/// <remarks>
/// <b>Aucun <c>OwnerId</c>.</b> Il est vrai que l'appelant ne peut voir que les
/// siennes — mais le renvoyer apprendrait au front qu'il existe un identifiant
/// de propriétaire, et le premier écran qui s'en sert le lira depuis la réponse
/// plutôt que depuis le jeton. D36 tient parce que l'identité n'a qu'une seule
/// source.
/// </remarks>
public sealed record SeanceRendue(
    Guid Id,
    DateTimeOffset Debut,
    DateTimeOffset? Fin,
    decimal? HeuresDeSommeil,
    int? Energie,
    string? Note
);

/// <summary>Une page de séances, et de quoi demander la suivante.</summary>
/// <param name="Elements">Les séances, de la plus récente à la plus ancienne.</param>
/// <param name="SuivantAvant">
/// L'instant du DERNIER élément rendu, ou <c>null</c> s'il n'y a plus rien
/// après. Le client le repasse tel quel.
/// </param>
/// <param name="SuivantAvantId">
/// L'identifiant du dernier élément rendu. Il accompagne l'instant, et ce
/// n'est pas une précaution théorique : deux séances peuvent porter le MÊME
/// <c>started_at</c> — un import, une saisie en lot, deux entraînements le
/// même matin notés à la minute près. Un curseur qui ne porterait que
/// l'instant les traiterait comme une seule, et un <c>&lt;</c> strict en
/// SAUTERAIT une définitivement : elle deviendrait invisible, sans erreur et
/// sans trou apparent.
/// </param>
public sealed record PageDeSeances(
    IReadOnlyList<SeanceRendue> Elements,
    DateTimeOffset? SuivantAvant,
    Guid? SuivantAvantId
)
{
    /// <summary>La taille de page par défaut, quand le client ne demande rien.</summary>
    public const int TailleParDefaut = 20;

    /// <summary>
    /// Le plafond. Il n'est PAS décoratif : sans lui, <c>?limite=1000000</c>
    /// fait matérialiser toute la table en mémoire — un déni de service que
    /// n'importe quel compte authentifié déclenche en une requête, et que ni
    /// RLS ni la limitation par adresse n'empêchent.
    /// </summary>
    public const int TailleMaximale = 100;

    /// <summary>La taille effective, bornée des deux côtés.</summary>
    public static int Borner(int? demandee) =>
        Math.Clamp(demandee ?? TailleParDefaut, 1, TailleMaximale);
}

/// <summary>Ce qu'on donne pour ajouter une série à une séance.</summary>
/// <param name="ExerciceId">L'exercice travaillé. Il doit exister et être visible.</param>
/// <param name="Index">Le rang de la série dans la séance, à partir de 1.</param>
/// <param name="ChargeKg">Nul est légitime : une série au poids de corps n'a pas de charge.</param>
/// <param name="Repetitions">Nul est légitime : un gainage se compte en secondes, pas en répétitions.</param>
/// <param name="Rir">Répétitions en réserve. Nul signifie « non renseigné », que le domaine suppose à 2.</param>
/// <param name="Echauffement">Une série d'échauffement ne compte dans AUCUN volume ni aucune estimation.</param>
public sealed record AjoutDeSerie(
    Guid ExerciceId,
    int Index,
    decimal? ChargeKg,
    int? Repetitions,
    int? Rir,
    bool Echauffement
)
{
    /// <summary>
    /// Le code du premier refus rencontré, ou <c>null</c> si tout passe.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Les bornes viennent du DOMAINE, elles ne sont pas recopiées ici.</b>
    /// <c>Charge.EstValide</c>, <c>Repetitions.EstValide</c> et
    /// <c>Rir.EstValide</c> sont les mêmes prédicats dont les fabriques se
    /// servent : il ne peut donc pas exister de valeur que ce contrôle accepte
    /// et que la construction refuse, ni l'inverse. C'est le critère du destin
    /// commun de <c>CLAUDE.md</c> § 4 — si une borne change, un seul endroit
    /// bouge.
    /// </para>
    ///
    /// <para>
    /// <b>Rendre le CODE plutôt qu'un booléen</b> évite au point d'entrée de
    /// redécouvrir laquelle des quatre règles a mordu — ce qu'il ferait en
    /// réécrivant les mêmes conditions, donc en dupliquant exactement ce que
    /// ce membre existe pour centraliser.
    /// </para>
    /// </remarks>
    public string? Faute =>
        Index < 1 ? "IndexInvalide"
        : ChargeKg is { } charge && !Domain.Grandeurs.Charge.EstValide(charge) ? "ChargeInvalide"
        : Repetitions is { } reps && !Domain.Grandeurs.Repetitions.EstValide(reps)
            ? "RepetitionsInvalides"
        : Rir is { } reserve && !Domain.Grandeurs.Rir.EstValide(reserve) ? "RirInvalide"
        : null;
}

/// <summary>Une série, telle qu'elle sort de l'API.</summary>
public sealed record SerieRendue(
    Guid Id,
    Guid ExerciceId,
    int Index,
    decimal? ChargeKg,
    int? Repetitions,
    int? Rir,
    bool Echauffement,
    DateTimeOffset Instant
);

/// <summary>Une séance avec ses séries.</summary>
/// <remarks>
/// La lecture d'une séance rend ses séries dans la MÊME réponse, et la liste
/// paginée ne les rend pas. Ce n'est pas une incohérence : l'écran de séance
/// affiche les deux ensemble, et les faire chercher en N+1 requêtes coûterait
/// un aller-retour par séance. La liste, elle, n'affiche que des en-têtes —
/// y joindre les séries multiplierait par vingt le volume transféré pour rien.
/// </remarks>
public sealed record SeanceDetaillee(SeanceRendue Seance, IReadOnlyList<SerieRendue> Series);

/// <summary>Ce qu'on donne pour enregistrer une pesée.</summary>
/// <param name="Jour">Absent, c'est aujourd'hui.</param>
/// <param name="PoidsKg">Le poids corporel.</param>
public sealed record MesureDePoids(DateOnly? Jour, decimal PoidsKg)
{
    /// <summary>
    /// Le code du premier refus, ou <c>null</c>.
    /// </summary>
    /// <param name="aujourdHui">
    /// La date du jour, DONNÉE plutôt que lue. Appeler l'horloge ici rendrait
    /// ce membre impur, donc son épreuve dépendante de la date d'exécution —
    /// et une épreuve qui rougit un jour sur trois cesse d'être lue.
    /// </param>
    /// <remarks>
    /// <b>Une pesée DANS LE FUTUR est refusée.</b> Ce n'est pas du zèle : la
    /// détection de perte rapide compare des valeurs consécutives dans le
    /// temps, et une date future les réordonne. Une faute de frappe sur
    /// l'année suffirait à faire passer la dernière pesée en tête de série et
    /// à inverser le sens de la variation.
    /// </remarks>
    public string? Faute(DateOnly aujourdHui) =>
        !Domain.Grandeurs.Masse.EstValide(PoidsKg) ? "PoidsInvalide"
        : Jour is { } jour && jour > aujourdHui ? "JourDansLeFutur"
        : null;
}

/// <summary>Une pesée, telle qu'elle sort de l'API.</summary>
public sealed record PoidsRendu(DateOnly Jour, decimal PoidsKg);

/// <summary>La série de pesées, et le constat de sécurité s'il y en a un.</summary>
/// <param name="Mesures">De la plus ancienne à la plus récente.</param>
/// <param name="Constat">
/// Un CODE — <c>PerteRapide</c> — ou <c>null</c>. JAMAIS une phrase.
/// </param>
/// <remarks>
/// <para>
/// <b>Le constat n'empêche RIEN.</b> `docs/01-conformite.md` § 5 impose « un
/// message d'orientation vers un professionnel, jamais de renforcement de la
/// restriction ». Un produit qui REFUSERAIT la saisie se ferait contourner en
/// cessant de saisir — ce qui supprime justement le signal qu'on cherchait à
/// lire.
/// </para>
///
/// <para>
/// <b>Et l'API ne rédige pas le message.</b> Le libellé vit en base, versionné
/// et validé — <c>09-comptes.md</c>. Une phrase écrite ici échapperait à cette
/// validation ET à i18next, sur le sujet exact où <c>01-conformite.md</c>
/// sépare informer de prescrire.
/// </para>
/// </remarks>
public sealed record SerieDePoids(IReadOnlyList<PoidsRendu> Mesures, string? Constat)
{
    /// <summary>Le code du constat de perte rapide. Le SEUL de ce lot.</summary>
    public const string PerteRapide = "PerteRapide";

    /// <summary>La fenêtre de lecture par défaut, en jours.</summary>
    /// <remarks>
    /// Quatre-vingt-dix jours couvrent largement les quatre semaines dont la
    /// détection a besoin, et donnent une courbe lisible sans rapatrier des
    /// années de mesures à chaque ouverture d'écran.
    /// </remarks>
    public const int FenetreParDefaut = 90;

    /// <summary>Le plafond de la fenêtre, en jours — cinq ans.</summary>
    public const int FenetreMaximale = 1825;

    /// <summary>La fenêtre effective, bornée des deux côtés.</summary>
    public static int BornerLaFenetre(int? demandee) =>
        Math.Clamp(demandee ?? FenetreParDefaut, 1, FenetreMaximale);
}
