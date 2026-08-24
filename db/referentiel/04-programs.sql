-- =====================================================================
-- Programmes modèles — les neuf de `docs/14-contenu.md` § 2
-- =====================================================================
--
-- CE FICHIER N'EST PAS RELU PAR UN KINÉSITHÉRAPEUTE.
--
-- `docs/14-contenu.md` § 2 l'exige — « relus par un kinésithérapeute pour la
-- partie contraintes […] tout aussi nécessaire » que la relecture du
-- diététicien pour la nutrition — et cette relecture N'A PAS EU LIEU.
--
-- La réserve pèse plus lourd sur CINQ d'entre eux : Reprise, Reprise
-- cervicale, Reprise lombaire, Épaule ménagée, Genou ménagé. Ce sont ceux
-- qu'on propose à quelqu'un qui revient de blessure — exactement la population
-- que `docs/00-produit.md` place au cœur de la cible.
--
-- ---------------------------------------------------------------------
-- CE QUI EST ÉCARTÉ, ET D'OÙ CELA VIENT
--
-- Les exclusions des quatre programmes de contrainte sont DÉRIVÉES du tableau
-- de `docs/05-entrainement.md` § 4, mécaniquement :
--
--   Cervicale | charge axiale, développés au-dessus de la tête, shrugs
--   Lombaire  | soulevé de terre lourd, squat barre, good morning chargé
--   Épaule    | développé militaire, dips lestés, écarté en amplitude maximale
--   Genou     | fentes profondes, extensions lourdes en fin d'amplitude
--
-- Le fichier est ENGENDRÉ, et son générateur REFUSE de produire un programme
-- qui ménage une contrainte tout en contenant un mouvement contre-indiqué pour
-- elle. Cette vérification est mécanique ; elle ne remplace pas un avis.
--
-- ---------------------------------------------------------------------
-- LES CIBLES NE SONT PAS INVENTÉES. `docs/05-entrainement.md` § 1 :
--   Intensité    | 0 à 3 RIR en régime normal, 3 à 4 EN REPRISE
--   Force        | 3 à 6 répétitions, 3 à 5 min de repos
--   Hypertrophie | 6 à 15 répétitions, 90 à 180 s de repos
--   Endurance    | 15 à 30 répétitions, repos court, en fin de séance
--
-- LES STRUCTURES NON PLUS, même document, même section :
--   3 séances | Full body                        | 3× par muscle
--   4 séances | Upper / Lower                    | 2×
--   5 séances | Upper / Lower / Push / Pull / Legs | 1,7×
--   6 séances | Push / Pull / Legs ×2            | 2×
--   6-7       | Split spécialisé                 | 1-2×
--
-- ---------------------------------------------------------------------
-- IDEMPOTENT. Rejouable après un `db:reset` ou une correction : les
-- programmes s'upsertent par leur slug, et leurs séances sont purgées puis
-- réécrites — la cascade emporte les exercices qui y pendent.
-- =====================================================================

begin;

-- ---------------------------------------------------------------------
-- 1. Les nine programmes
-- ---------------------------------------------------------------------
insert into public.programs
  (slug, is_template, owner_id, name_fr, name_en, description_fr, description_en,
   notes_fr, notes_en, frequency_min, frequency_max, targets_constraint)
values
('reprise', true, null, 'Reprise', 'Getting back',
 'Pour revenir après un arrêt, quelle qu''en soit la durée. Trois séances complètes par semaine, sur machines et charges légères.',
 'For coming back after a break, however long. Three full-body sessions a week, on machines and light loads.',
 'Aucune barre libre, et c''est le choix central : reprendre demande de retrouver un geste, pas de gérer un équilibre. Les machines guident la trajectoire pendant que la coordination revient.

Le RIR visé est de 3 à 4, soit trois ou quatre répétitions gardées en réserve à chaque série. C''est volontairement loin de l''échec : la courbature d''une reprise trop ambitieuse coûte la deuxième séance, et c''est la deuxième séance qui compte.

Chaque muscle est travaillé trois fois par semaine, ce que la structure full body permet à cette fréquence.',
 'No free barbell, and that is the central choice: coming back is about finding a movement again, not managing balance. Machines guide the path while coordination returns.

The target RIR is 3 to 4 — three or four reps left in reserve on every set. That is deliberately far from failure: the soreness of an over-ambitious restart costs you the second session, and the second session is the one that counts.

Each muscle is trained three times a week, which the full-body structure allows at this frequency.',
 3, 3, null),

('reprise-cervicale', true, null, 'Reprise cervicale', 'Neck-friendly restart',
 'Trois séances par semaine, sans charge axiale ni poussée au-dessus de la tête.',
 'Three sessions a week, with no axial loading and no overhead pressing.',
 'Trois familles de mouvements sont absentes : la charge axiale, les développés au-dessus de la tête et les shrugs. Le squat barre et le développé militaire n''y sont donc pas, et leur remplacement par la presse et le développé machine n''enlève rien au travail — seulement la compression qui passe par la nuque.

La rétraction cervicale et la rétraction scapulaire ouvrent les séances. Ce sont des mouvements de contrôle, pas de charge : leur intérêt est de replacer les appuis avant que le reste ne commence.

Ce programme ne traite rien et ne remplace aucun avis. Il propose des mouvements en évitant une famille de sollicitations.',
 'Three families of movements are absent: axial loading, overhead pressing, and shrugs. Barbell squats and overhead press are therefore out, and replacing them with the leg press and machine press takes nothing away from the work — only the compression that travels through the neck.

Chin tucks and scapular retraction open each session. These are control movements, not loaded ones: their point is to reset the anchors before anything else begins.

This program treats nothing and replaces no medical advice. It offers movements while avoiding one family of demands.',
 3, 3, 'cervicale'),

('reprise-lombaire', true, null, 'Reprise lombaire', 'Lower-back-friendly restart',
 'Trois séances par semaine, sans soulevé de terre ni squat barre, avec un travail de contrôle du bassin.',
 'Three sessions a week, with no deadlift and no barbell squat, built around pelvic control.',
 'Le soulevé de terre, le squat barre et le good morning chargé sont absents — ce sont les trois mouvements qui chargent le plus la colonne en position debout. La presse et le hip thrust machine les remplacent : la charge y passe par les jambes sans traverser la colonne debout.

Le dead bug et le bird dog ouvrent deux séances sur trois, et ce n''est pas du remplissage. Ils demandent de tenir le bassin immobile pendant que les membres bougent — c''est-à-dire exactement ce qu''un dos demande dans la vie courante.

Le gainage latéral commence à genoux. La version complète se prend quand la première est facile, pas selon un calendrier.',
 'Deadlifts, barbell squats and loaded good mornings are absent — the three movements that load a standing spine the most. The leg press and machine hip thrust replace them: the load travels through the legs without passing down a standing spine.

Dead bugs and bird dogs open two sessions out of three, and that is not filler. They ask you to hold the pelvis still while the limbs move — which is exactly what a back is asked to do in daily life.

Side planks start from the knees. Move to the full version when the first one is easy, not on a schedule.',
 3, 3, 'lombaire'),

('epaule-menagee', true, null, 'Épaule ménagée', 'Shoulder-friendly',
 'Trois à quatre séances par semaine, sans développé militaire ni dips lestés, avec un travail des rotateurs à chaque séance.',
 'Three to four sessions a week, no overhead press and no weighted dips, with rotator work in every session.',
 'Le développé militaire, les dips lestés et l''écarté en amplitude maximale sont écartés. Ce qui reste couvre le haut du corps sans passer par la position où l''épaule est la plus vulnérable.

Une rotation externe ouvre chaque séance. Les rotateurs sont petits, ils fatiguent vite, et les placer en fin de séance revient à ne pas les travailler.

La charge y est volontairement faible : ces mouvements se font au contrôle, et une charge qui oblige à s''aider du tronc ne travaille plus ce qu''on visait.',
 'Overhead press, weighted dips and full-range flyes are out. What remains covers the upper body without passing through the position where the shoulder is most vulnerable.

An external rotation opens every session. The rotators are small, they fatigue quickly, and putting them last amounts to not training them at all.

The load there is deliberately light: these are control movements, and a load heavy enough to recruit the trunk no longer trains what you were aiming at.',
 3, 4, 'epaule'),

('genou-menage', true, null, 'Genou ménagé', 'Knee-friendly',
 'Trois à quatre séances par semaine, sans fente profonde ni extension lourde en fin d''amplitude.',
 'Three to four sessions a week, no deep lunges and no heavy end-range extensions.',
 'Les fentes profondes et les extensions lourdes en fin d''amplitude sont absentes. Le travail des jambes passe par la chaîne postérieure : pont fessier, hip thrust, leg curl. Ce sont des mouvements où la charge s''applique sans que le genou ait à absorber l''angle fermé.

La dorsiflexion de cheville figure dans une séance sur deux. La cheville et la hanche encadrent le genou ; quand l''une des deux manque de mobilité, c''est le genou qui compense.

Le haut du corps est complet, sans restriction : cette contrainte ne le concerne pas.',
 'Deep lunges and heavy end-range extensions are absent. Leg work goes through the posterior chain: glute bridge, hip thrust, leg curl. These load the movement without asking the knee to absorb a closed angle.

Ankle dorsiflexion appears in every other session. The ankle and the hip frame the knee; when either lacks mobility, the knee is what compensates.

The upper body is trained in full, with no restriction: this constraint does not concern it.',
 3, 4, 'genou'),

('full-body', true, null, 'Full body', 'Full body',
 'Trois séances complètes par semaine, chacune travaillant tout le corps. La structure qui convient à cette fréquence.',
 'Three full-body sessions a week, each working the whole body. The structure that suits this frequency.',
 'À trois séances par semaine, chaque muscle est stimulé trois fois — le maximum que cette fréquence permet, et bien au-dessus du minimum de deux passages hebdomadaires.

Chaque séance tient sur quatre mouvements, tous polyarticulaires. C''est délibéré : à trois séances, le temps disponible ne suffit pas à isoler, et ce qui isole prend la place de ce qui construit.

Les trois séances ne se répètent pas à l''identique. Elles alternent les angles — barre puis haltères, vertical puis horizontal — pour que trois passages par semaine sur les mêmes articulations ne deviennent pas trois fois la même contrainte.',
 'At three sessions a week, each muscle is stimulated three times — the most this frequency allows, and well above the minimum of two weekly passes.

Each session holds to four movements, all compound. That is deliberate: at three sessions the available time does not stretch to isolation work, and isolation takes the place of what actually builds.

The three sessions are not identical. They rotate angles — barbell then dumbbell, vertical then horizontal — so that three weekly passes over the same joints do not become the same demand three times.',
 3, 3, null),

('upper-lower', true, null, 'Upper / Lower', 'Upper / Lower',
 'Quatre séances par semaine, haut et bas du corps en alternance. Chaque muscle est vu deux fois.',
 'Four sessions a week, alternating upper and lower body. Each muscle is trained twice.',
 'Quatre séances donnent une fréquence de deux par muscle — le minimum utile, et la raison pour laquelle cette structure existe à cette fréquence plutôt qu''un split.

Les deux séances hautes ne sont pas jumelles : la première pousse à l''horizontale et tire à l''horizontale, la seconde pousse à l''inclinaison et tire à la verticale. Le volume par angle reste modéré alors que le volume par muscle monte.

Le travail d''isolation arrive en fin de séance, après les mouvements qui demandent de la coordination. L''ordre inverse est une des façons les plus efficaces de rater une série lourde.',
 'Four sessions give a frequency of two per muscle — the useful minimum, and the reason this structure exists at this frequency rather than a split.

The two upper sessions are not twins: the first presses and pulls horizontally, the second presses on an incline and pulls vertically. Volume per angle stays moderate while volume per muscle rises.

Isolation work comes at the end, after the movements that demand coordination. The reverse order is one of the most reliable ways to miss a heavy set.',
 4, 4, null),

('ppl', true, null, 'Push / Pull / Legs', 'Push / Pull / Legs',
 'Cinq à six séances par semaine, réparties en poussée, tirage et jambes.',
 'Five to six sessions a week, split into push, pull and legs.',
 'La fréquence par muscle dépend de ce que vous tenez réellement. À six séances, le cycle tourne deux fois : chaque muscle est vu deux fois par semaine. À cinq, il tourne une fois et demie — la fréquence tombe à 1,7, et c''est une information, pas un reproche.

Si cinq séances sont votre plafond, une autre répartition existe à cette fréquence — haut, bas, poussée, tirage, jambes — et elle maintient deux passages par muscle.

Les six séances ne se répètent pas : chaque famille a une version A et une version B, aux angles et aux charges différents. Six séances identiques sur la même semaine seraient six fois la même contrainte, avec la fatigue en plus.',
 'Frequency per muscle depends on what you actually sustain. At six sessions the cycle runs twice: each muscle is trained twice a week. At five it runs one and a half times — frequency drops to 1.7, and that is information, not a reproach.

If five sessions is your ceiling, a different split exists at that frequency — upper, lower, push, pull, legs — and it keeps two passes per muscle.

The six sessions do not repeat: each family has an A and a B version, at different angles and loads. Six identical sessions in one week would be the same demand six times, with the fatigue on top.',
 5, 6, null),

('split', true, null, 'Split spécialisé', 'Body-part split',
 'Six séances par semaine, un groupe musculaire dominant par séance.',
 'Six sessions a week, one dominant muscle group per session.',
 'Chaque muscle est travaillé une à deux fois par semaine selon la séance. C''est **en dessous** du minimum de deux passages hebdomadaires pour plusieurs groupes, et il faut le savoir avant de choisir : ce programme échange de la fréquence contre du volume par séance.

Ce choix se défend quand un groupe est en retard et qu''on veut lui consacrer une séance entière. Il se défend moins comme structure permanente — et à volume hebdomadaire égal, une fréquence de deux fait mieux.

La règle est simple : le split par groupe musculaire devient légitime à partir de cinq séances hebdomadaires. En dessous, il ne l''est pas.',
 'Each muscle is trained once or twice a week depending on the session. That is **below** the minimum of two weekly passes for several groups, and you should know it before choosing: this program trades frequency for volume per session.

The trade defends itself when one group lags and you want to give it a whole session. It defends itself less as a permanent structure — and at equal weekly volume, a frequency of two does better.

The rule is simple: a body-part split becomes legitimate from five weekly sessions upward. Below that, it is not.',
 6, 6, null)

on conflict (slug) where is_template
do update set
  name_fr            = excluded.name_fr,
  name_en            = excluded.name_en,
  description_fr     = excluded.description_fr,
  description_en     = excluded.description_en,
  notes_fr           = excluded.notes_fr,
  notes_en           = excluded.notes_en,
  frequency_min      = excluded.frequency_min,
  frequency_max      = excluded.frequency_max,
  targets_constraint = excluded.targets_constraint;


-- ---------------------------------------------------------------------
-- 2. Purge des séances des MODÈLES — jamais celles des utilisateurs.
--    `on delete cascade` emporte les exercices qui y pendent.
-- ---------------------------------------------------------------------
delete from public.program_days
 where program_id in (select id from public.programs where is_template = true);


-- ---------------------------------------------------------------------
-- 3. Les séances
-- ---------------------------------------------------------------------
insert into public.program_days (program_id, label_fr, label_en, position)
select p.id, v.label_fr, v.label_en, v.position
  from (values
    ('reprise', 'Séance A', 'Session A', 1),
    ('reprise', 'Séance B', 'Session B', 2),
    ('reprise', 'Séance C', 'Session C', 3),
    ('reprise-cervicale', 'Séance A', 'Session A', 1),
    ('reprise-cervicale', 'Séance B', 'Session B', 2),
    ('reprise-cervicale', 'Séance C', 'Session C', 3),
    ('reprise-lombaire', 'Séance A', 'Session A', 1),
    ('reprise-lombaire', 'Séance B', 'Session B', 2),
    ('reprise-lombaire', 'Séance C', 'Session C', 3),
    ('epaule-menagee', 'Séance A — haut', 'Session A — upper', 1),
    ('epaule-menagee', 'Séance B — bas', 'Session B — lower', 2),
    ('epaule-menagee', 'Séance C — haut', 'Session C — upper', 3),
    ('epaule-menagee', 'Séance D — bas', 'Session D — lower', 4),
    ('genou-menage', 'Séance A — bas', 'Session A — lower', 1),
    ('genou-menage', 'Séance B — haut', 'Session B — upper', 2),
    ('genou-menage', 'Séance C — bas', 'Session C — lower', 3),
    ('genou-menage', 'Séance D — haut', 'Session D — upper', 4),
    ('full-body', 'Séance A', 'Session A', 1),
    ('full-body', 'Séance B', 'Session B', 2),
    ('full-body', 'Séance C', 'Session C', 3),
    ('upper-lower', 'Haut A', 'Upper A', 1),
    ('upper-lower', 'Bas A', 'Lower A', 2),
    ('upper-lower', 'Haut B', 'Upper B', 3),
    ('upper-lower', 'Bas B', 'Lower B', 4),
    ('ppl', 'Poussée A', 'Push A', 1),
    ('ppl', 'Tirage A', 'Pull A', 2),
    ('ppl', 'Jambes A', 'Legs A', 3),
    ('ppl', 'Poussée B', 'Push B', 4),
    ('ppl', 'Tirage B', 'Pull B', 5),
    ('ppl', 'Jambes B', 'Legs B', 6),
    ('split', 'Pectoraux', 'Chest', 1),
    ('split', 'Dos', 'Back', 2),
    ('split', 'Jambes', 'Legs', 3),
    ('split', 'Épaules', 'Shoulders', 4),
    ('split', 'Bras', 'Arms', 5),
    ('split', 'Ischios et fessiers', 'Hamstrings and glutes', 6)
       ) as v(program_slug, label_fr, label_en, position)
  join public.programs p
    on p.slug = v.program_slug and p.is_template = true;


-- ---------------------------------------------------------------------
-- 4. Les exercices de chaque séance
--
--    La jointure sur `exercises.slug` saute EN SILENCE ce qu'elle ne trouve
--    pas. C'est pourquoi le bloc final compte les lignes posées et LÈVE si le
--    compte ne tombe pas juste — sans quoi une faute de frappe produirait une
--    séance amputée que personne ne verrait.
-- ---------------------------------------------------------------------
insert into public.program_exercises
  (program_day_id, exercise_id, position, target_sets,
   target_reps_min, target_reps_max, target_rir, rest_seconds, note)
select d.id, e.id, v.position, v.target_sets,
       v.target_reps_min, v.target_reps_max, v.target_rir, v.rest_seconds, v.note
  from (values
    ('reprise', 1, 'goblet-squat-kettlebell', 1, 3, 10, 12, 4, 90, null),
    ('reprise', 1, 'pompes-inclinees', 2, 3, 8, 12, 4, 90, 'Plus le support est haut, plus le mouvement est facile.'),
    ('reprise', 1, 'tirage-horizontal-machine', 3, 3, 10, 12, 3, 90, null),
    ('reprise', 1, 'pont-fessier', 4, 3, 12, 15, 3, 60, null),
    ('reprise', 1, 'gainage-ventral', 5, 3, 20, 30, 3, 60, 'Les répétitions comptent des secondes de maintien.'),
    ('reprise', 2, 'presse-a-cuisses', 1, 3, 10, 12, 4, 120, null),
    ('reprise', 2, 'developpe-machine-pectoraux', 2, 3, 10, 12, 4, 90, null),
    ('reprise', 2, 'tirage-vertical', 3, 3, 10, 12, 3, 90, null),
    ('reprise', 2, 'leg-curl-assis', 4, 3, 12, 15, 3, 90, null),
    ('reprise', 2, 'dead-bug', 5, 3, 8, 10, 3, 60, 'Répétitions par côté.'),
    ('reprise', 3, 'fente-statique', 1, 3, 8, 10, 4, 90, 'Répétitions par jambe.'),
    ('reprise', 3, 'developpe-epaules-machine', 2, 3, 10, 12, 4, 90, null),
    ('reprise', 3, 'rowing-machine-buste-soutenu', 3, 3, 10, 12, 3, 90, null),
    ('reprise', 3, 'mollets-assis', 4, 3, 12, 15, 3, 60, null),
    ('reprise', 3, 'bird-dog', 5, 3, 8, 10, 3, 60, 'Répétitions par côté.'),
    ('reprise-cervicale', 1, 'retraction-cervicale', 1, 3, 8, 10, 4, 45, 'Sans forcer. L''amplitude utile est petite.'),
    ('reprise-cervicale', 1, 'retraction-scapulaire', 2, 3, 10, 12, 3, 45, null),
    ('reprise-cervicale', 1, 'presse-a-cuisses', 3, 3, 10, 12, 3, 120, null),
    ('reprise-cervicale', 1, 'developpe-machine-pectoraux', 4, 3, 10, 12, 3, 90, null),
    ('reprise-cervicale', 1, 'gainage-ventral', 5, 3, 20, 30, 3, 60, 'Les répétitions comptent des secondes de maintien.'),
    ('reprise-cervicale', 2, 'face-pull', 1, 3, 12, 15, 3, 60, null),
    ('reprise-cervicale', 2, 'rowing-machine-buste-soutenu', 2, 3, 10, 12, 3, 90, null),
    ('reprise-cervicale', 2, 'leg-curl-assis', 3, 3, 12, 15, 3, 90, null),
    ('reprise-cervicale', 2, 'pont-fessier', 4, 3, 12, 15, 3, 60, null),
    ('reprise-cervicale', 2, 'dead-bug', 5, 3, 8, 10, 3, 60, 'Répétitions par côté.'),
    ('reprise-cervicale', 3, 'rotation-externe-elastique', 1, 3, 12, 15, 3, 45, 'Coude au corps, mouvement lent.'),
    ('reprise-cervicale', 3, 'tirage-horizontal-machine', 2, 3, 10, 12, 3, 90, null),
    ('reprise-cervicale', 3, 'fente-statique', 3, 3, 8, 10, 3, 90, 'Répétitions par jambe.'),
    ('reprise-cervicale', 3, 'mollets-assis', 4, 3, 12, 15, 3, 60, null),
    ('reprise-cervicale', 3, 'bird-dog', 5, 3, 8, 10, 3, 60, 'Répétitions par côté.'),
    ('reprise-lombaire', 1, 'dead-bug', 1, 3, 8, 10, 3, 60, 'Répétitions par côté. Le bas du dos reste au sol.'),
    ('reprise-lombaire', 1, 'pont-fessier', 2, 3, 12, 15, 3, 60, null),
    ('reprise-lombaire', 1, 'presse-a-cuisses', 3, 3, 10, 12, 3, 120, null),
    ('reprise-lombaire', 1, 'developpe-machine-pectoraux', 4, 3, 10, 12, 3, 90, null),
    ('reprise-lombaire', 1, 'gainage-lateral-genoux', 5, 3, 15, 25, 3, 60, 'Les répétitions comptent des secondes, par côté.'),
    ('reprise-lombaire', 2, 'bird-dog', 1, 3, 8, 10, 3, 60, 'Répétitions par côté.'),
    ('reprise-lombaire', 2, 'leg-curl-assis', 2, 3, 12, 15, 3, 90, null),
    ('reprise-lombaire', 2, 'tirage-horizontal-machine', 3, 3, 10, 12, 3, 90, null),
    ('reprise-lombaire', 2, 'developpe-epaules-machine', 4, 3, 10, 12, 3, 90, null),
    ('reprise-lombaire', 2, 'gainage-ventral', 5, 3, 20, 30, 3, 60, 'Les répétitions comptent des secondes de maintien.'),
    ('reprise-lombaire', 3, 'clamshell', 1, 3, 12, 15, 3, 45, 'Répétitions par côté.'),
    ('reprise-lombaire', 3, 'hip-thrust-machine', 2, 3, 10, 12, 3, 90, null),
    ('reprise-lombaire', 3, 'extension-jambes', 3, 3, 12, 15, 3, 90, null),
    ('reprise-lombaire', 3, 'rowing-machine-buste-soutenu', 4, 3, 10, 12, 3, 90, null),
    ('reprise-lombaire', 3, 'mollets-assis', 5, 3, 12, 15, 3, 60, null),
    ('epaule-menagee', 1, 'rotation-externe-elastique', 1, 3, 12, 15, 3, 45, 'Coude au corps. Lent, sans à-coup.'),
    ('epaule-menagee', 1, 'retraction-scapulaire', 2, 3, 10, 12, 3, 45, null),
    ('epaule-menagee', 1, 'rowing-machine-buste-soutenu', 3, 3, 10, 12, 2, 90, null),
    ('epaule-menagee', 1, 'developpe-machine-pectoraux', 4, 3, 10, 12, 2, 90, 'Amplitude arrêtée avant que l''épaule ne parte en arrière.'),
    ('epaule-menagee', 2, 'presse-a-cuisses', 1, 4, 10, 12, 2, 120, null),
    ('epaule-menagee', 2, 'leg-curl-assis', 2, 3, 12, 15, 2, 90, null),
    ('epaule-menagee', 2, 'pont-fessier', 3, 3, 12, 15, 2, 60, null),
    ('epaule-menagee', 2, 'mollets-assis', 4, 3, 12, 15, 2, 60, null),
    ('epaule-menagee', 2, 'gainage-ventral', 5, 3, 20, 30, 3, 60, 'Les répétitions comptent des secondes de maintien.'),
    ('epaule-menagee', 3, 'rotation-externe-couche', 1, 3, 12, 15, 3, 45, 'Répétitions par bras.'),
    ('epaule-menagee', 3, 'face-pull-elastique', 2, 3, 12, 15, 3, 60, null),
    ('epaule-menagee', 3, 'tirage-horizontal-machine', 3, 3, 10, 12, 2, 90, null),
    ('epaule-menagee', 3, 'curl-halteres', 4, 3, 10, 12, 2, 60, null),
    ('epaule-menagee', 3, 'extension-triceps-poulie', 5, 3, 12, 15, 2, 60, null),
    ('epaule-menagee', 4, 'fente-statique', 1, 3, 8, 10, 2, 90, 'Répétitions par jambe.'),
    ('epaule-menagee', 4, 'extension-jambes', 2, 3, 12, 15, 2, 90, null),
    ('epaule-menagee', 4, 'hip-thrust-machine', 3, 3, 10, 12, 2, 90, null),
    ('epaule-menagee', 4, 'dead-bug', 4, 3, 8, 10, 3, 60, 'Répétitions par côté.'),
    ('genou-menage', 1, 'pont-fessier', 1, 3, 12, 15, 2, 60, null),
    ('genou-menage', 1, 'leg-curl-assis', 2, 4, 10, 12, 2, 90, null),
    ('genou-menage', 1, 'dorsiflexion-cheville', 3, 3, 12, 15, 3, 45, 'Répétitions par cheville.'),
    ('genou-menage', 1, 'mollets-assis', 4, 3, 12, 15, 2, 60, null),
    ('genou-menage', 2, 'developpe-couche-halteres', 1, 4, 8, 12, 2, 120, null),
    ('genou-menage', 2, 'rowing-haltere', 2, 4, 8, 12, 2, 120, 'Répétitions par bras.'),
    ('genou-menage', 2, 'developpe-epaules-halteres', 3, 3, 10, 12, 2, 90, null),
    ('genou-menage', 2, 'face-pull', 4, 3, 12, 15, 2, 60, null),
    ('genou-menage', 3, 'hip-thrust-machine', 1, 4, 10, 12, 2, 90, null),
    ('genou-menage', 3, 'romanian-une-jambe', 2, 3, 8, 10, 3, 90, 'Répétitions par jambe. La charge reste légère.'),
    ('genou-menage', 3, 'clamshell', 3, 3, 12, 15, 3, 45, 'Répétitions par côté.'),
    ('genou-menage', 3, 'gainage-lateral', 4, 3, 20, 30, 3, 60, 'Les répétitions comptent des secondes, par côté.'),
    ('genou-menage', 4, 'tirage-vertical', 1, 4, 8, 12, 2, 120, null),
    ('genou-menage', 4, 'developpe-incline-halteres', 2, 3, 10, 12, 2, 90, null),
    ('genou-menage', 4, 'curl-marteau', 3, 3, 10, 12, 2, 60, null),
    ('genou-menage', 4, 'extension-triceps-poulie', 4, 3, 12, 15, 2, 60, null),
    ('full-body', 1, 'squat-barre-dos', 1, 4, 6, 8, 2, 180, null),
    ('full-body', 1, 'developpe-couche-barre', 2, 4, 6, 8, 2, 180, null),
    ('full-body', 1, 'rowing-barre', 3, 4, 8, 10, 2, 120, null),
    ('full-body', 1, 'gainage-ventral', 4, 3, 30, 45, 2, 60, 'Les répétitions comptent des secondes de maintien.'),
    ('full-body', 2, 'souleve-de-terre-roumain', 1, 4, 8, 10, 2, 150, null),
    ('full-body', 2, 'developpe-militaire', 2, 4, 6, 8, 2, 150, null),
    ('full-body', 2, 'traction-assistee', 3, 4, 6, 10, 2, 120, 'L''assistance diminue quand dix répétitions sont franchies.'),
    ('full-body', 2, 'mollets-debout', 4, 3, 12, 15, 1, 60, null),
    ('full-body', 3, 'presse-a-cuisses', 1, 4, 10, 12, 2, 120, null),
    ('full-body', 3, 'developpe-incline-halteres', 2, 4, 8, 12, 2, 120, null),
    ('full-body', 3, 'tirage-vertical', 3, 4, 8, 12, 2, 120, null),
    ('full-body', 3, 'curl-halteres', 4, 3, 10, 12, 1, 60, null),
    ('upper-lower', 1, 'developpe-couche-barre', 1, 4, 6, 8, 2, 180, null),
    ('upper-lower', 1, 'rowing-barre', 2, 4, 8, 10, 2, 120, null),
    ('upper-lower', 1, 'developpe-epaules-halteres', 3, 3, 8, 12, 2, 90, null),
    ('upper-lower', 1, 'curl-halteres', 4, 3, 10, 12, 1, 60, null),
    ('upper-lower', 1, 'extension-triceps-poulie', 5, 3, 12, 15, 1, 60, null),
    ('upper-lower', 2, 'squat-barre-dos', 1, 4, 6, 8, 2, 180, null),
    ('upper-lower', 2, 'leg-curl-allonge', 2, 4, 10, 12, 2, 90, null),
    ('upper-lower', 2, 'mollets-debout', 3, 4, 12, 15, 1, 60, null),
    ('upper-lower', 2, 'gainage-ventral', 4, 3, 30, 45, 2, 60, 'Les répétitions comptent des secondes de maintien.'),
    ('upper-lower', 3, 'traction-pronation', 1, 4, 6, 10, 2, 150, null),
    ('upper-lower', 3, 'developpe-incline-halteres', 2, 4, 8, 12, 2, 120, null),
    ('upper-lower', 3, 'tirage-horizontal', 3, 3, 10, 12, 2, 90, null),
    ('upper-lower', 3, 'elevation-laterale', 4, 3, 12, 15, 1, 60, null),
    ('upper-lower', 3, 'curl-marteau', 5, 3, 10, 12, 1, 60, null),
    ('upper-lower', 4, 'souleve-de-terre-roumain', 1, 4, 8, 10, 2, 150, null),
    ('upper-lower', 4, 'presse-a-cuisses', 2, 4, 10, 12, 2, 120, null),
    ('upper-lower', 4, 'extension-jambes', 3, 3, 12, 15, 1, 90, null),
    ('upper-lower', 4, 'mollets-assis', 4, 4, 12, 15, 1, 60, null),
    ('ppl', 1, 'developpe-couche-barre', 1, 4, 6, 8, 2, 180, null),
    ('ppl', 1, 'developpe-epaules-halteres', 2, 4, 8, 10, 2, 120, null),
    ('ppl', 1, 'elevation-laterale', 3, 3, 12, 15, 1, 60, null),
    ('ppl', 1, 'extension-triceps-poulie', 4, 3, 12, 15, 1, 60, null),
    ('ppl', 2, 'traction-pronation', 1, 4, 6, 10, 2, 150, null),
    ('ppl', 2, 'rowing-barre', 2, 4, 8, 10, 2, 120, null),
    ('ppl', 2, 'face-pull', 3, 3, 12, 15, 1, 60, null),
    ('ppl', 2, 'curl-barre', 4, 3, 8, 12, 1, 60, null),
    ('ppl', 3, 'squat-barre-dos', 1, 4, 6, 8, 2, 180, null),
    ('ppl', 3, 'leg-curl-allonge', 2, 4, 10, 12, 2, 90, null),
    ('ppl', 3, 'mollets-debout', 3, 4, 12, 15, 1, 60, null),
    ('ppl', 3, 'gainage-ventral', 4, 3, 30, 45, 2, 60, 'Les répétitions comptent des secondes de maintien.'),
    ('ppl', 4, 'developpe-incline-halteres', 1, 4, 8, 12, 2, 120, null),
    ('ppl', 4, 'developpe-machine-pectoraux', 2, 3, 10, 12, 2, 90, null),
    ('ppl', 4, 'elevation-laterale-poulie', 3, 3, 12, 15, 1, 60, null),
    ('ppl', 4, 'dips-triceps', 4, 3, 8, 12, 2, 90, null),
    ('ppl', 5, 'tirage-vertical', 1, 4, 8, 12, 2, 120, null),
    ('ppl', 5, 'rowing-haltere', 2, 4, 8, 12, 2, 120, 'Répétitions par bras.'),
    ('ppl', 5, 'oiseau', 3, 3, 12, 15, 1, 60, null),
    ('ppl', 5, 'curl-marteau', 4, 3, 10, 12, 1, 60, null),
    ('ppl', 6, 'souleve-de-terre-roumain', 1, 4, 8, 10, 2, 150, null),
    ('ppl', 6, 'presse-a-cuisses', 2, 4, 10, 12, 2, 120, null),
    ('ppl', 6, 'extension-jambes', 3, 3, 12, 15, 1, 90, null),
    ('ppl', 6, 'mollets-assis', 4, 4, 12, 15, 1, 60, null),
    ('split', 1, 'developpe-couche-barre', 1, 4, 6, 8, 2, 180, null),
    ('split', 1, 'developpe-incline-halteres', 2, 4, 8, 12, 2, 120, null),
    ('split', 1, 'developpe-machine-pectoraux', 3, 3, 10, 12, 2, 90, null),
    ('split', 1, 'dips-pectoraux', 4, 3, 8, 12, 2, 90, null),
    ('split', 2, 'traction-pronation', 1, 4, 6, 10, 2, 150, null),
    ('split', 2, 'rowing-barre', 2, 4, 8, 10, 2, 120, null),
    ('split', 2, 'tirage-horizontal', 3, 3, 10, 12, 2, 90, null),
    ('split', 2, 'pull-over-poulie', 4, 3, 12, 15, 1, 60, null),
    ('split', 3, 'squat-barre-dos', 1, 4, 6, 8, 2, 180, null),
    ('split', 3, 'presse-a-cuisses', 2, 4, 10, 12, 2, 120, null),
    ('split', 3, 'extension-jambes', 3, 3, 12, 15, 1, 90, null),
    ('split', 3, 'mollets-debout', 4, 4, 12, 15, 1, 60, null),
    ('split', 4, 'developpe-militaire', 1, 4, 6, 8, 2, 150, null),
    ('split', 4, 'elevation-laterale', 2, 4, 12, 15, 1, 60, null),
    ('split', 4, 'oiseau', 3, 3, 12, 15, 1, 60, null),
    ('split', 4, 'face-pull', 4, 3, 12, 15, 1, 60, null),
    ('split', 5, 'curl-barre', 1, 4, 8, 12, 1, 90, null),
    ('split', 5, 'extension-triceps-poulie', 2, 4, 10, 15, 1, 90, null),
    ('split', 5, 'curl-marteau', 3, 3, 10, 12, 1, 60, null),
    ('split', 5, 'extension-triceps-corde', 4, 3, 12, 15, 1, 60, null),
    ('split', 6, 'souleve-de-terre-roumain', 1, 4, 8, 10, 2, 150, null),
    ('split', 6, 'leg-curl-allonge', 2, 4, 10, 12, 2, 90, null),
    ('split', 6, 'hip-thrust', 3, 4, 10, 12, 2, 90, null),
    ('split', 6, 'mollets-assis', 4, 3, 12, 15, 1, 60, null)
       ) as v(program_slug, day_position, exercise_slug, position, target_sets,
              target_reps_min, target_reps_max, target_rir, rest_seconds, note)
  join public.programs p
    on p.slug = v.program_slug and p.is_template = true
  join public.program_days d
    on d.program_id = p.id and d.position = v.day_position
  join public.exercises e
    on e.slug = v.exercise_slug and e.is_custom = false;


-- ---------------------------------------------------------------------
-- 5. LE COMPTE DOIT TOMBER JUSTE
--
--    Sans ce bloc, un slug d'exercice mal orthographié donnerait une séance
--    plus courte que prévu, sans la moindre erreur : la jointure ne trouve
--    rien, elle n'insère rien, et `psql` rend 0.
-- ---------------------------------------------------------------------
do $$
declare
  attendu constant int := 157;
  obtenu  int;
begin
  select count(*) into obtenu
    from public.program_exercises pe
    join public.program_days d on d.id = pe.program_day_id
    join public.programs p     on p.id = d.program_id and p.is_template = true;

  if obtenu <> attendu then
    raise exception
      'Le référentiel a posé % exercices de programme au lieu de %. Un slug est introuvable au catalogue.',
      obtenu, attendu;
  end if;
end $$;

commit;
