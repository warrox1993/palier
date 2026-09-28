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
/// Combien de séries dures ont été VUES — pas combien ont produit une
/// estimation.
///
/// La nuance porte l'information : zéro dit « je n'ai pas encore de donnée »,
/// l'état vide que `11-qualite.md` exige de traiter ; un nombre non nul avec
/// <paramref name="UnRepetitionMaximumKg" /> absent dit « vos séries sont trop
/// longues pour qu'une estimation ait un sens » — au-delà de quinze
/// répétitions effectives, <c>ForceEstimee</c> refuse de rendre une charge
/// plutôt que d'en rendre une fausse.
///
/// Compter les séries RETENUES par Epley confondrait les deux cas, et l'écran
/// ne saurait plus s'il doit inviter à saisir une première série ou expliquer
/// pourquoi l'estimation manque.
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
/// <param name="Nom">Obligatoire, et borné. Il devient le nom FRANÇAIS : un exercice personnalisé n'a pas de traduction.</param>
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

    /// <summary>La longueur maximale du matériel.</summary>
    /// <remarks>
    /// <b>Soixante et non quarante</b>, contrairement à un nom de muscle : le
    /// matériel se décrit par une composition — « poids de corps + élastique
    /// lourd », « barre olympique + colliers de serrage » — là où un muscle
    /// porte un nom. Le bornage lui-même n'était pas là, et son absence
    /// contredisait la doctrine écrite trois lignes plus haut : la colonne est
    /// <c>text</c>, donc PostgreSQL ne borne rien, et un mégaoctet de matériel
    /// serait renvoyé à chaque lecture du catalogue.
    /// </remarks>
    public const int LongueurMaximaleDuMateriel = 60;

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
        : Materiel is { Length: > LongueurMaximaleDuMateriel } ? "MaterielTropLong"
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
/// <param name="Slug">La clé naturelle du catalogue, ou <c>null</c> pour un exercice personnalisé.</param>
/// <param name="NomFr">Le nom français.</param>
/// <param name="NomEn">Le nom anglais, ou <c>null</c> pour un exercice personnalisé.</param>
/// <param name="ConsignesFr">Les consignes d'exécution en français, ou <c>null</c>.</param>
/// <param name="ConsignesEn">Les consignes d'exécution en anglais, ou <c>null</c>.</param>
/// <param name="ErreursFr">Les erreurs fréquentes en français, ou <c>null</c>.</param>
/// <param name="ErreursEn">Les erreurs fréquentes en anglais, ou <c>null</c>.</param>
/// <param name="RoleDuMouvement"><c>tirage</c>, <c>poussee</c> ou <c>aucun</c>.</param>
/// <param name="Materiel">Le matériel, ou <c>null</c>.</param>
/// <param name="MusclesPrimaires">Comptent pour une série pleine dans le volume.</param>
/// <param name="MusclesSecondaires">Comptent pour une demi-série.</param>
/// <param name="Unilateral">Vrai quand le mouvement se fait un côté à la fois.</param>
/// <param name="IncrementParDefaut">Le pas de progression proposé, en kilogrammes.</param>
/// <param name="ContreIndicationsPour">Les régions sur lesquelles il est contre-indiqué.</param>
/// <param name="Marquages">Le recoupement avec les contraintes DÉCLARÉES par l appelant. Vide s il n en a pas.</param>
/// <param name="EstPersonnalise">
/// Vrai pour les siens, faux pour le catalogue public. C'est ce drapeau qui dit
/// au front s'il peut proposer la suppression — et l'API le refuse de toute
/// façon, parce que cacher un bouton ne rend pas une action indisponible.
/// </param>
/// <remarks>
/// <b>LES DEUX LANGUES VOYAGENT ENSEMBLE, et l'API n'en choisit aucune.</b>
/// Le catalogue est une RÉFÉRENCE : le front le charge, le met en cache et
/// affiche la langue que l'utilisateur a réglée. Rendre une seule langue
/// obligerait à propager un paramètre de langue dans chaque appel, à faire
/// varier le cache avec, et à décider d'un repli côté serveur — trois
/// complications pour une information que le client possède déjà.
///
/// Les champs de langue sont NULS sur un exercice personnalisé : l'utilisateur
/// donne un nom, pas une traduction ni des consignes. La contrainte <c>CHECK</c>
/// du schéma les exige en revanche du catalogue public — D70.
/// </remarks>
public sealed record ExerciceRendu(
    Guid Id,
    string? Slug,
    string NomFr,
    string? NomEn,
    string? ConsignesFr,
    string? ConsignesEn,
    string? ErreursFr,
    string? ErreursEn,
    string RoleDuMouvement,
    string? Materiel,
    IReadOnlyList<string> MusclesPrimaires,
    IReadOnlyList<string> MusclesSecondaires,
    bool Unilateral,
    decimal IncrementParDefaut,
    IReadOnlyList<string> ContreIndicationsPour,
    bool EstPersonnalise,
    IReadOnlyList<MarquageDeContrainte> Marquages
);

/// <summary>
/// Le recoupement entre une contre-indication de l'exercice et une contrainte
/// que l'appelant a DÉCLARÉE.
/// </summary>
/// <remarks>
/// <para>
/// <b>C'est ce que « filtrer automatiquement le catalogue » veut dire ici, et
/// c'est un choix de conformité, pas de goût.</b>
/// <c>docs/05-entrainement.md</c> § 4 emploie le mot « filtrent » ;
/// <c>docs/00-produit.md</c> tranche ce qu'il peut vouloir dire : « voici ta
/// valeur, voici la référence, voici l'écart » est une information, tandis que
/// décider à la place de l'utilisateur « place l'éditeur en conseiller, ce qui
/// est réglementé ». Et <c>docs/01-conformite.md</c> § 2 pose la formule
/// canonique : « un chiffre, une référence, un écart. <b>Jamais une
/// action.</b> »
/// </para>
///
/// <para>
/// <b>Retirer un exercice du catalogue EST une action.</b> Elle ment sur le
/// contenu du catalogue, elle n'apprend rien, et elle prend une décision
/// médicale en silence pour quelqu'un qui a peut-être un avis contraire de son
/// kinésithérapeute. Le marquage dit la même chose sans décider : voici
/// l'exercice, voici votre contrainte, voici le recoupement.
/// </para>
///
/// <para>
/// <b>L'API ne filtre donc JAMAIS.</b> La présentation — replier, signaler,
/// laisser passer — se déduit de <paramref name="Severite" /> et appartient à
/// l'écran. Une épreuve garde ce partage dans les deux sens : le catalogue rend
/// tout, et le marquage est présent.
/// </para>
/// </remarks>
/// <param name="Region">La région contre-indiquée, déclarée par l'appelant.</param>
/// <param name="Severite">La sévérité qu'il lui a donnée — son réglage.</param>
public sealed record MarquageDeContrainte(string Region, string Severite);

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
/// <param name="Instant">
/// Le DÉBUT DE LA SÉANCE, et non le moment de la saisie. Le § 5 raisonne sur
/// des séances consécutives : noter après coup le ressenti d'une séance
/// ancienne ne doit pas la faire passer en tête.
/// </param>
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

/// <summary>Une contrainte, telle qu'on la déclare.</summary>
/// <param name="Region">Une des quatre régions de la liste fermée.</param>
/// <param name="Severite">
/// <c>leger</c>, <c>modere</c> ou <c>strict</c>. ABSENTE vaut <c>modere</c> —
/// le formulaire peut ne pas poser la question, et ne rien dire est un choix
/// légitime.
/// </param>
/// <param name="Note">Un aide-mémoire libre, facultatif. Il n'atteint aucun calcul.</param>
public sealed record DeclarationDUneContrainte(string? Region, string? Severite, string? Note)
{
    /// <summary>La longueur maximale de la note.</summary>
    /// <remarks>
    /// La colonne est <c>text</c>, donc PostgreSQL ne borne rien. Deux cents
    /// signes tiennent « douleur à la flexion complète, opérée en 2019 » avec
    /// de la marge, et refusent le mégaoctet.
    /// </remarks>
    public const int LongueurMaximaleDeLaNote = 200;

    /// <summary>La note, vide valant absente.</summary>
    public string? NoteNettoyee =>
        string.IsNullOrWhiteSpace(Note) ? null : Note.Trim();

    /// <summary>Le code du refus, ou <c>null</c>.</summary>
    /// <remarks>
    /// Une PROJECTION de <see cref="ContrainteValidee.Lire" />, qui porte la
    /// seule expression des règles. Les tester une seconde fois ici les ferait
    /// diverger au premier changement — et c'est déjà arrivé : la première
    /// version de ce fichier avait les deux, et la garde en double masquait une
    /// branche que rien ne pouvait franchir.
    /// </remarks>
    public string? Faute => ContrainteValidee.Lire(this).Faute;
}

/// <summary>La liste COMPLÈTE des contraintes qu'on déclare.</summary>
/// <param name="Regions">
/// Zéro à quatre contraintes. Une liste vide est LÉGITIME : c'est ainsi qu'on
/// déclare n'avoir aucune contrainte, ou qu'on retire la dernière.
/// </param>
public sealed record DeclarationDeContraintes(IReadOnlyList<DeclarationDUneContrainte>? Regions)
{
    /// <summary>La liste, une absence valant une liste vide.</summary>
    /// <remarks>
    /// Le type dit nullable ICI, contrairement à <c>CreationDExercice</c> où
    /// il mentait. C'est la même réalité — un corps JSON qui omet le champ
    /// donne <c>null</c> — écrite honnêtement.
    /// </remarks>
    public IReadOnlyList<DeclarationDUneContrainte> RegionsOuVide => Regions ?? [];

    /// <summary>Le code du premier refus, ou <c>null</c>.</summary>
    /// <remarks>
    /// <b>Les doublons sont TOLÉRÉS et dédupliqués</b>, pas refusés : déclarer
    /// deux fois « genou » dit la même chose, et la dernière sévérité donnée
    /// l'emporte. Un refus obligerait le client à dédupliquer avant d'envoyer.
    /// </remarks>
    public string? Faute =>
        RegionsOuVide.Count > Contraintes.Toutes.Count ? "TropDeContraintes"
        : RegionsOuVide.Select(c => ContrainteValidee.Lire(c).Faute).FirstOrDefault(f => f is not null);

    /// <summary>
    /// Les contraintes sous une forme que le gestionnaire ne peut pas mal lire.
    /// </summary>
    /// <remarks>
    /// N'appeler qu'après avoir vérifié <see cref="Faute" /> : une déclaration
    /// fautive est simplement écartée ici, sans lever. C'est le point d'entrée
    /// qui refuse, avec le code qui dit lequel des contrôles a mordu.
    /// </remarks>
    public IReadOnlyList<ContrainteValidee> Valider() =>
        [.. RegionsOuVide.Select(c => ContrainteValidee.Lire(c).Valide).OfType<ContrainteValidee>()];
}

/// <summary>
/// Une contrainte dont la validité est acquise À LA CONSTRUCTION.
/// </summary>
/// <remarks>
/// <para>
/// <b>Ce type existe pour supprimer un couplage implicite.</b> Les
/// gestionnaires recevaient <see cref="DeclarationDUneContrainte" /> — des
/// chaînes brutes — et relisaient leur région avec un <c>!</c>, en comptant sur
/// le fait que le point d'entrée avait appelé <c>Faute</c> avant. Rien ne le
/// garantissait structurellement : ces gestionnaires sont publics et
/// enregistrés au conteneur, et un futur cas d'usage, une commande
/// d'exploitation ou une épreuve pouvait les appeler directement et obtenir une
/// <c>NullReferenceException</c>.
/// </para>
///
/// <para>
/// C'est la règle de <c>CLAUDE.md</c> § 4 appliquée : « valider à la frontière,
/// une seule fois, puis faire confiance au type. L'entrée devient un type du
/// domaine qui <b>ne peut pas être construit invalide</b>. Le reste du code n'a
/// plus à se défendre. » Le constructeur est privé ; on n'y entre que par
/// <see cref="DeclarationDeContraintes.Valider" />, qui a déjà refusé.
/// </para>
///
/// <para>
/// <b>Aucune revalidation dans le gestionnaire</b>, et c'est délibéré : elle
/// créerait une branche que le chemin HTTP ne peut jamais franchir, donc du
/// code mort que le seuil de couverture refuserait à juste titre.
/// </para>
/// </remarks>
public sealed record ContrainteValidee
{
    private ContrainteValidee(Contrainte region, Severite severite, string? note)
    {
        Region = region;
        Severite = severite;
        Note = note;
    }

    public Contrainte Region { get; }

    public Severite Severite { get; }

    public string? Note { get; }

    /// <summary>
    /// Lit une déclaration : rend SOIT le type validé, SOIT le code du refus.
    /// Jamais les deux, jamais aucun.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>UNE SEULE expression de la validation.</b> La première version
    /// dupliquait le travail : <c>Faute</c> testait les trois règles pour
    /// produire un code, et cette méthode les retestait pour produire un type.
    /// Deux conséquences, toutes deux mesurées — un déréférencement de
    /// <c>null</c> sur la première mouture, puis une branche morte à 50 % de
    /// couverture, parce que la garde <c>Faute</c> masquait les deux lectures
    /// qui la suivaient.
    /// </para>
    ///
    /// <para>
    /// Ici, chaque règle est écrite une fois, et chaque branche est
    /// franchissable par une épreuve. <c>Faute</c> n'est plus qu'une projection
    /// de ce résultat.
    /// </para>
    /// </remarks>
    internal static (ContrainteValidee? Valide, string? Faute) Lire(
        DeclarationDUneContrainte? declaree
    )
    {
        if (declaree is null)
        {
            return (null, "ContrainteInvalide");
        }

        if (!Contraintes.Lire(declaree.Region, out var region))
        {
            return (null, "ContrainteInvalide");
        }

        if (!Severites.Lire(declaree.Severite, out var severite))
        {
            return (null, "SeveriteInvalide");
        }

        var note = declaree.NoteNettoyee;

        if (note is { Length: > DeclarationDUneContrainte.LongueurMaximaleDeLaNote })
        {
            return (null, "NoteTropLongue");
        }

        return (new ContrainteValidee(region.Value, severite.Value, note), null);
    }
}

/// <summary>Une contrainte déclarée, telle qu'elle sort de l'API.</summary>
/// <param name="Region">La forme stockée : <c>cervicale</c>, <c>lombaire</c>, <c>epaule</c> ou <c>genou</c>.</param>
/// <param name="Severite">La forme stockée : <c>leger</c>, <c>modere</c> ou <c>strict</c>.</param>
/// <param name="Note">L'aide-mémoire, ou <c>null</c>.</param>
/// <param name="DeclareeLe">
/// Depuis quand elle est déclarée. Elle NE CHANGE PAS quand on renvoie une
/// liste qui la contient déjà, ni quand sa sévérité change — une contrainte qui
/// passe de <c>modere</c> à <c>strict</c> reste la même contrainte.
/// </param>
public sealed record ContrainteRendue(
    string Region,
    string Severite,
    string? Note,
    DateTimeOffset DeclareeLe
);
