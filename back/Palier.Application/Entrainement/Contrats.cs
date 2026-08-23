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

/// <summary>Ce que le produit sait dire d'un exercice.</summary>
/// <param name="ExerciceId">L'exercice interrogé.</param>
/// <param name="UnRepetitionMaximumKg">
/// Le 1RM estimé, ou <c>null</c>. Absent signifie « pas encore de série
/// exploitable » ou « série trop longue pour qu'une estimation ait un sens » —
/// et ces deux cas se lisent au champ <paramref name="SeriesRetenues" />.
/// </param>
/// <param name="Fiabilite">
/// <c>Bonne</c>, <c>Moyenne</c>, <c>Faible</c>, ou <c>null</c> quand il n'y a
/// pas d'estimation. Elle voyage AVEC la valeur, jamais séparément : une force
/// estimée sans sa fiabilité invite à la lire comme une mesure.
/// </param>
/// <param name="EnPlateau">
/// Un constat, pas une consigne. `docs/05-entrainement.md` § 2 : le système
/// « signale, SANS prescrire de solution ».
/// </param>
/// <param name="SeriesRetenues">
/// Combien de séries dures ont servi. Zéro dit « je n'ai pas encore de
/// donnée » — l'état vide que `11-qualite.md` exige de traiter, et qui n'est
/// pas une erreur.
/// </param>
public sealed record ProgressionRendue(
    Guid ExerciceId,
    decimal? UnRepetitionMaximumKg,
    string? Fiabilite,
    bool EnPlateau,
    int SeriesRetenues
);

/// <summary>Le volume d'un muscle sur une semaine.</summary>
/// <param name="Muscle">Le groupe musculaire, tel qu'il est nommé au catalogue.</param>
/// <param name="SeriesDures">
/// Les séries d'échauffement sont exclues, et un muscle secondaire compte pour
/// une demi-série — d'où le décimal. C'est la vue <c>weekly_volume</c> qui
/// applique ces deux règles, posée au lot 2.
/// </param>
public sealed record VolumeDUnMuscle(string Muscle, decimal SeriesDures);

/// <summary>Une semaine de volume.</summary>
/// <param name="Semaine">Le LUNDI de la semaine, tel que la vue le calcule.</param>
/// <param name="Muscles">Un élément par muscle travaillé, du plus au moins travaillé.</param>
public sealed record SemaineDeVolume(DateOnly Semaine, IReadOnlyList<VolumeDUnMuscle> Muscles);

/// <summary>Le volume hebdomadaire, de la semaine la plus récente à la plus ancienne.</summary>
/// <remarks>
/// <para>
/// <b>Le ratio tirage/poussée n'est PAS ici, et c'est délibéré.</b>
/// <c>docs/05-entrainement.md</c> § 4 le définit — « dos et trapèzes contre
/// pectoraux et triceps, sur 7 jours, cible minimale 1,3 » — et
/// <c>VolumeParGroupe.SurSeptJours</c> sait le calculer depuis le lot 3.
/// </para>
///
/// <para>
/// Ce qui manque est en base : <c>exercises</c> porte <c>primary_muscles</c> et
/// <c>secondary_muscles</c>, mais AUCUNE colonne ne dit si un mouvement tire ou
/// pousse. Le déduire des muscles serait faux — un pull-over travaille les
/// pectoraux ET le grand dorsal, un rowing inversé et un développé partagent le
/// deltoïde — et un ratio faux vaut moins que pas de ratio : il ferait modifier
/// un programme sur une mesure inventée.
/// </para>
///
/// <para>
/// La colonne se pose au lot qui remplit le catalogue — étape 1 bis, 250 à 400
/// exercices — où le rôle se renseigne exercice par exercice, avec le reste.
/// L'ajouter maintenant créerait une colonne vide sur un catalogue vide.
/// </para>
/// </remarks>
public sealed record BilanDeVolume(IReadOnlyList<SemaineDeVolume> Semaines)
{
    /// <summary>Le nombre de semaines rendues par défaut.</summary>
    public const int SemainesParDefaut = 8;

    /// <summary>Le plafond, en semaines — deux ans.</summary>
    public const int SemainesMaximales = 104;

    /// <summary>Le nombre de semaines effectif, borné des deux côtés.</summary>
    public static int BornerLesSemaines(int? demandees) =>
        Math.Clamp(demandees ?? SemainesParDefaut, 1, SemainesMaximales);
}

/// <summary>Ce qu'on donne pour créer un exercice personnalisé.</summary>
/// <param name="Nom">Obligatoire, et borné : c'est du texte libre affiché.</param>
/// <param name="Materiel">Facultatif — « barre », « haltères », « poids de corps ».</param>
/// <param name="MusclesPrimaires">Au moins un. Sans lui, l'exercice ne compte dans aucun volume.</param>
/// <param name="MusclesSecondaires">Comptent pour une demi-série dans le volume.</param>
/// <param name="Unilateral">Vrai quand le mouvement se fait un côté à la fois.</param>
/// <param name="IncrementParDefaut">Le pas de progression proposé, en kilogrammes.</param>
/// <param name="ContreIndicationsPour">Zéro à quatre régions, prises dans la liste FERMÉE.</param>
public sealed record CreationDExercice(
    string Nom,
    string? Materiel,
    IReadOnlyList<string> MusclesPrimaires,
    IReadOnlyList<string> MusclesSecondaires,
    bool Unilateral,
    decimal IncrementParDefaut,
    IReadOnlyList<string> ContreIndicationsPour
)
{
    /// <summary>La longueur maximale du nom.</summary>
    /// <remarks>
    /// La colonne est <c>text</c>, donc PostgreSQL ne borne rien : sans ce
    /// contrôle, un nom d'un mégaoctet entrerait en base et casserait chaque
    /// écran qui l'affiche. Cent vingt signes tiennent le plus long nom
    /// d'exercice réel avec de la marge.
    /// </remarks>
    public const int LongueurMaximaleDuNom = 120;

    /// <summary>Le pas de progression le plus grand qu'on accepte, en kilogrammes.</summary>
    /// <remarks>
    /// UNE PROPRIÉTÉ, ET NON UNE <c>const decimal</c>. Un <c>decimal</c> n'est
    /// pas une constante de compilation en IL : le compilateur engendre un
    /// constructeur statique pour l'initialiser, et ce constructeur n'est
    /// JAMAIS exécuté — lire la constante depuis une épreuve n'y change rien,
    /// puisque la valeur est inlinée à l'appel. La ligne reste donc
    /// éternellement non couverte, et le seuil de 100 % échoue sans qu'aucune
    /// épreuve ne manque. Les <c>const int</c> voisines n'ont pas ce défaut.
    /// </remarks>
    public static decimal IncrementMaximal => 50m;

    /// <summary>Le nombre maximal de muscles nommés, primaires et secondaires confondus.</summary>
    public const int MusclesMaximum = 12;

    /// <summary>La longueur maximale d'un nom de muscle.</summary>
    public const int LongueurMaximaleDUnMuscle = 40;

    /// <summary>Le code du premier refus, ou <c>null</c>.</summary>
    /// <remarks>
    /// <b>Tout est borné, y compris ce qui semble anodin.</b> Les tableaux
    /// <c>text[]</c> n'ont pas de taille maximale en PostgreSQL : un tableau
    /// d'un million d'entrées passerait, entrerait dans la vue
    /// <c>weekly_volume</c> — qui le DÉPLIE par <c>unnest</c> — et y
    /// multiplierait les lignes autant de fois. C'est un déni de service qu'un
    /// compte authentifié déclenche en une requête, et le nombre de muscles
    /// d'un exercice réel dépasse rarement six.
    /// </remarks>
    /// <summary>
    /// Les muscles secondaires, une liste ABSENTE valant une liste vide.
    /// </summary>
    /// <remarks>
    /// <b>Le type dit non-nullable, et il ment.</b> Ces listes viennent de la
    /// désérialisation d'un corps JSON : un client qui omet
    /// <c>musclesSecondaires</c> — ce qui est parfaitement légitime — produit
    /// <c>null</c>, que l'annotation de nullabilité n'empêche EN RIEN. Lire
    /// <c>.Count</c> dessus lèverait une <c>NullReferenceException</c>, donc un
    /// 500 sur une requête valide.
    ///
    /// C'est exactement ce que <c>CLAUDE.md</c> § 4 vise : « toute donnée qui
    /// vient de l'extérieur est hostile jusqu'à preuve du contraire », et une
    /// annotation de compilation n'est pas une preuve — elle ne survit pas au
    /// passage par le réseau.
    /// </remarks>
    public IReadOnlyList<string> SecondairesOuVide => MusclesSecondaires ?? [];

    /// <inheritdoc cref="SecondairesOuVide" />
    public IReadOnlyList<string> ContraintesOuVide => ContreIndicationsPour ?? [];

    /// <inheritdoc cref="SecondairesOuVide" />
    public IReadOnlyList<string> PrimairesOuVide => MusclesPrimaires ?? [];

    public string? Faute =>
        string.IsNullOrWhiteSpace(Nom) ? "NomRequis"
        : Nom.Length > LongueurMaximaleDuNom ? "NomTropLong"
        : PrimairesOuVide.Count == 0 ? "MusclePrimaireRequis"
        : PrimairesOuVide.Count + SecondairesOuVide.Count > MusclesMaximum ? "TropDeMuscles"
        : PrimairesOuVide.Concat(SecondairesOuVide).Any(m => !MuscleValide(m)) ? "MuscleInvalide"
        // Une comparaison ordinaire, et non un motif `is <= 0m or > ...` : un
        // motif exige une CONSTANTE, et `IncrementMaximal` est devenue une
        // propriété pour échapper au constructeur statique jamais exécuté
        // qu'un `const decimal` engendre.
        : IncrementParDefaut <= 0m || IncrementParDefaut > IncrementMaximal ? "IncrementInvalide"
        : ContraintesOuVide.Any(c => !Contraintes.Lire(c, out _)) ? "ContrainteInvalide"
        : null;

    private static bool MuscleValide(string? muscle) =>
        !string.IsNullOrWhiteSpace(muscle) && muscle.Length <= LongueurMaximaleDUnMuscle;
}

/// <summary>Un exercice, tel qu'il sort de l'API.</summary>
/// <param name="Id">L'identifiant, celui que les séries référencent.</param>
/// <param name="Nom">Le libellé affiché.</param>
/// <param name="Materiel">Le matériel, ou <c>null</c>.</param>
/// <param name="MusclesPrimaires">Comptent pour une série pleine dans le volume.</param>
/// <param name="MusclesSecondaires">Comptent pour une demi-série.</param>
/// <param name="Unilateral">Vrai quand le mouvement se fait un côté à la fois.</param>
/// <param name="IncrementParDefaut">Le pas de progression proposé, en kilogrammes.</param>
/// <param name="ContreIndicationsPour">Les régions sur lesquelles il est contre-indiqué.</param>
/// <param name="EstPersonnalise">
/// Vrai pour les siens, faux pour le catalogue public. C'est ce drapeau qui dit
/// au front s'il peut proposer la suppression — et l'API le refuse de toute
/// façon, parce que cacher un bouton ne rend pas une action indisponible.
/// </param>
public sealed record ExerciceRendu(
    Guid Id,
    string Nom,
    string? Materiel,
    IReadOnlyList<string> MusclesPrimaires,
    IReadOnlyList<string> MusclesSecondaires,
    bool Unilateral,
    decimal IncrementParDefaut,
    IReadOnlyList<string> ContreIndicationsPour,
    bool EstPersonnalise
);

/// <summary>Ce qu'on donne pour noter le ressenti d'un exercice.</summary>
/// <param name="ExerciceId">L'exercice noté. Il doit avoir été travaillé dans la séance.</param>
/// <param name="Ressenti">
/// <c>good</c>, <c>meh</c> ou <c>pain</c>. La liste est fermée par
/// <c>docs/05-entrainement.md</c> § 5.
/// </param>
public sealed record NoteDeRessenti(Guid ExerciceId, string? Ressenti)
{
    /// <summary>Le code du refus, ou <c>null</c>.</summary>
    public string? Faute => Ressentis.Lire(Ressenti, out _) ? null : "RessentiInvalide";
}

/// <summary>Un ressenti, tel qu'il sort de l'API.</summary>
/// <param name="SeanceId">La séance où il a été noté — c'est elle qui l'ordonne dans le temps.</param>
/// <param name="ExerciceId">L'exercice concerné.</param>
/// <param name="Ressenti">La forme stockée : <c>good</c>, <c>meh</c> ou <c>pain</c>.</param>
/// <param name="Instant">Quand il a été noté.</param>
public sealed record RessentiRendu(
    Guid SeanceId,
    Guid ExerciceId,
    string Ressenti,
    DateTimeOffset Instant
)
{
    /// <summary>Le nombre de ressentis rendus par défaut pour un exercice.</summary>
    /// <remarks>
    /// Dix couvre largement les fenêtres du § 5 — « les 3 dernières séances »,
    /// « 2 consécutifs », « 3 consécutifs » — et tient dans un écran.
    /// </remarks>
    public const int HistoriqueParDefaut = 10;

    /// <summary>Le plafond.</summary>
    public const int HistoriqueMaximal = 200;

    /// <summary>La taille effective de l'historique, bornée des deux côtés.</summary>
    public static int BornerLHistorique(int? demande) =>
        Math.Clamp(demande ?? HistoriqueParDefaut, 1, HistoriqueMaximal);
}

/// <summary>La liste COMPLÈTE des contraintes qu'on déclare.</summary>
/// <param name="Regions">
/// Zéro à quatre régions, prises dans la liste fermée. Une liste vide est
/// LÉGITIME : c'est ainsi qu'on déclare n'avoir aucune contrainte, ou qu'on
/// retire la dernière.
/// </param>
public sealed record DeclarationDeContraintes(IReadOnlyList<string>? Regions)
{
    /// <summary>La liste, une absence valant une liste vide.</summary>
    /// <remarks>
    /// Le type dit nullable ICI, contrairement à <c>CreationDExercice</c> où
    /// il mentait. C'est la même réalité — un corps JSON qui omet le champ
    /// donne <c>null</c> — écrite honnêtement.
    /// </remarks>
    public IReadOnlyList<string> RegionsOuVide => Regions ?? [];

    /// <summary>Le code du refus, ou <c>null</c>.</summary>
    /// <remarks>
    /// <b>Les doublons sont TOLÉRÉS et dédupliqués</b>, pas refusés : déclarer
    /// deux fois « genou » dit la même chose que le déclarer une fois, et un
    /// refus obligerait le client à dédupliquer avant d'envoyer. La contrainte
    /// <c>unique (owner_id, region)</c> reste le dernier mot.
    /// </remarks>
    public string? Faute =>
        RegionsOuVide.Count > Contraintes.Toutes.Count ? "TropDeContraintes"
        : RegionsOuVide.Any(r => !Contraintes.Lire(r, out _)) ? "ContrainteInvalide"
        : null;
}

/// <summary>Une contrainte déclarée, telle qu'elle sort de l'API.</summary>
/// <param name="Region">La forme stockée : <c>cervicale</c>, <c>lombaire</c>, <c>epaule</c> ou <c>genou</c>.</param>
/// <param name="DeclareeLe">
/// Depuis quand elle est déclarée. Elle NE CHANGE PAS quand on renvoie une
/// liste qui la contient déjà — sans quoi la date dirait « depuis le dernier
/// enregistrement » au lieu de « depuis quand ».
/// </param>
public sealed record ContrainteRendue(string Region, DateTimeOffset DeclareeLe);
