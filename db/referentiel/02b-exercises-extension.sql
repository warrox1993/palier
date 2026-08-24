-- =====================================================================
-- Catalogue d'exercices — extension au-delà des 60 prioritaires
-- =====================================================================
--
-- CE FICHIER N'EST PAS RELU PAR UN PROFESSIONNEL DE SANTÉ, comme
-- `02-exercises.sql`. Mêmes règles, mêmes sources, même réserve.
--
-- POURQUOI UN SECOND FICHIER. `docs/16-projet.md` § 4 nomme
-- `02-exercises.sql` « 60 exercices prioritaires PUIS EXTENSION ». Les deux
-- moitiés n'ont ni le même statut ni le même rythme : les soixante premiers
-- couvrent « 90 % des programmes » (`14-contenu.md` § 1) et bougeront peu,
-- l'extension s'enrichira au fil des retours. Les séparer permet de relire
-- l'une sans rouvrir l'autre — et la relecture par le kinésithérapeute, quand
-- elle viendra, portera d'abord sur les soixante.
--
-- OÙ LES MOUVEMENTS ONT ÉTÉ INVENTORIÉS. Les catalogues publics de référence
-- — docteur-fitness.com, all-musculation.com, espace-musculation.com — ont
-- servi à VÉRIFIER QU'AUCUNE FAMILLE NE MANQUE. Un nom de mouvement est un
-- fait, pas une œuvre : « développé décliné » n'appartient à personne.
--
-- EN REVANCHE, AUCUN TEXTE N'EN VIENT. Les consignes et les erreurs
-- ci-dessous sont rédigées ici. `docs/14-contenu.md` § 1 le dit des images —
-- « ne jamais utiliser d'images trouvées sur internet, le contentieux en droit
-- d'auteur sur les photos de fitness est fréquent et coûteux » — et l'esprit
-- vaut mot pour mot pour les textes.
--
-- Ce fichier porte le catalogue à environ 150 mouvements, seuil que
-- `14-contenu.md` § 1 nomme : « en dessous de 150, le produit paraît vide face
-- à la concurrence ».
-- =====================================================================

insert into public.exercises
  (slug, name_fr, name_en, equipment, primary_muscles, secondary_muscles,
   is_unilateral, default_increment, contraindicated_for, movement_role,
   instructions_fr, instructions_en, common_errors_fr, common_errors_en,
   is_custom, owner_id)
values

-- ---------------------------------------------------------------------
-- Pectoraux — angles et machines
-- ---------------------------------------------------------------------
('developpe-decline', 'Développé décliné', 'Decline bench press', 'barre',
 array['pectoraux'], array['triceps'], false, 2.5, array[]::text[], 'poussee',
 'Banc incliné vers le bas, barre descendue au bas des pectoraux. Gardez les pieds bloqués.',
 'Bench declined, bar lowered to the lower chest. Keep the feet secured.',
 'Descente trop rapide, tête qui se relève.', 'Lowering too fast, head lifting off the bench.', false, null),

('pec-deck', 'Pec-deck', 'Pec deck', 'machine',
 array['pectoraux'], array[]::text[], false, 2.5, array['epaule'], 'poussee',
 'Coudes à hauteur des épaules, rapprochez les avant-bras sans forcer l''ouverture arrière.',
 'Elbows at shoulder height, bring the forearms together without forcing the stretch.',
 'Amplitude arrière excessive, épaules qui s''enroulent.', 'Excessive stretch behind, shoulders rolling forward.', false, null),

('ecarte-incline', 'Écarté incliné', 'Incline dumbbell fly', 'haltères',
 array['pectoraux'], array[]::text[], false, 2, array['epaule'], 'poussee',
 'Banc à 30°, coudes fixes légèrement fléchis. Ouvrez jusqu''au niveau des pectoraux.',
 'Bench at 30°, elbows fixed and slightly bent. Open to chest level.',
 'Coudes qui se plient pour aider, descente sous le plan du buste.', 'Bending the elbows to help, going below the torso plane.', false, null),

('pompes-inclinees', 'Pompes inclinées', 'Incline push-up', 'poids de corps',
 array['pectoraux'], array['triceps'], false, 2.5, array[]::text[], 'poussee',
 'Mains surélevées sur un banc. Corps aligné, descendez la poitrine vers l''appui.',
 'Hands elevated on a bench. Body in line, lower the chest to the support.',
 'Bassin qui s''affaisse, appui trop haut qui vide le mouvement.', 'Hips sagging, support too high making it trivial.', false, null),

('pompes-declinees', 'Pompes déclinées', 'Decline push-up', 'poids de corps',
 array['pectoraux'], array['deltoides','triceps'], false, 2.5, array[]::text[], 'poussee',
 'Pieds surélevés, mains au sol sous les épaules. Corps gainé.',
 'Feet elevated, hands on the floor under the shoulders. Core braced.',
 'Cambrure lombaire, nuque en hyperextension.', 'Lower-back arching, neck hyperextended.', false, null),

('developpe-machine-convergent', 'Développé convergent', 'Converging chest press', 'machine',
 array['pectoraux'], array['triceps'], false, 2.5, array[]::text[], 'poussee',
 'Poignées à hauteur des pectoraux, poussez en rapprochant les mains.',
 'Handles at chest height, press while the hands converge.',
 'Siège mal réglé, verrouillage sec.', 'Seat poorly set, harsh lockout.', false, null),

('ecarte-poulie-basse', 'Écarté poulie basse', 'Low cable fly', 'poulie',
 array['pectoraux'], array['deltoides'], false, 2.5, array['epaule'], 'poussee',
 'Poulies en bas, montez les mains devant la poitrine en arc de cercle.',
 'Pulleys set low, sweep the hands up and together in front of the chest.',
 'Bras qui se plient, buste qui recule.', 'Arms bending, torso leaning back.', false, null),

-- ---------------------------------------------------------------------
-- Dos — machines, poulies, variantes
-- ---------------------------------------------------------------------
('rowing-t-bar', 'Rowing T-bar', 'T-bar row', 'barre',
 array['dos'], array['biceps','trapezes'], false, 2.5, array['lombaire'], 'tirage',
 'Buste incliné, dos neutre. Tirez la poignée vers le bas des côtes.',
 'Torso hinged, spine neutral. Pull the handle to the lower ribs.',
 'Buste qui se redresse, dos rond en bas.', 'Torso rising, rounded back at the bottom.', false, null),

('rack-pull', 'Rack pull', 'Rack pull', 'barre',
 array['dos','trapezes'], array['ischio-jambiers','lombaires'], false, 5, array['lombaire'], 'tirage',
 'Barre posée aux genoux dans un rack. Fermez les hanches en gardant le dos neutre.',
 'Bar set at knee height in a rack. Lock the hips keeping a neutral spine.',
 'Barre qui s''éloigne des jambes, hyperextension en haut.', 'Bar drifting from the legs, overextending at the top.', false, null),

('seal-row', 'Seal row', 'Seal row', 'barre',
 array['dos'], array['biceps','trapezes'], false, 2.5, array[]::text[], 'tirage',
 'Allongé face contre un banc haut, tirez la barre vers le banc. Le buste ne bouge pas.',
 'Lying face-down on a high bench, pull the bar to the bench. The torso stays still.',
 'Décollement du buste, élan des jambes.', 'Lifting the chest, using the legs.', false, null),

('tirage-unilateral-poulie', 'Tirage unilatéral', 'Single-arm cable row', 'poulie',
 array['dos'], array['biceps'], true, 2.5, array[]::text[], 'tirage',
 'Une main à la fois, laissez l''omoplate s''avancer puis tirez le coude vers la hanche.',
 'One arm at a time, let the shoulder blade travel forward then drive the elbow to the hip.',
 'Rotation du buste pour gagner de l''amplitude.', 'Rotating the torso to gain range.', false, null),

('pull-over-haltere', 'Pull-over haltère', 'Dumbbell pull-over', 'haltère',
 array['dos'], array['pectoraux'], false, 2, array['epaule'], 'tirage',
 'Allongé, haltère au-dessus de la poitrine. Descendez derrière la tête sans cambrer.',
 'Lying down, dumbbell above the chest. Lower behind the head without arching.',
 'Cambrure lombaire, amplitude au-delà du confort de l''épaule.', 'Lower-back arching, range beyond shoulder comfort.', false, null),

('tirage-vertical-prise-serree', 'Tirage prise serrée', 'Close-grip pulldown', 'poulie',
 array['dos'], array['biceps'], false, 2.5, array[]::text[], 'tirage',
 'Poignée en V, tirez vers le sternum en gardant le buste presque vertical.',
 'V-handle, pull to the sternum keeping the torso nearly upright.',
 'Balancement, épaules qui montent.', 'Swinging, shoulders shrugging.', false, null),

('traction-lestee', 'Traction lestée', 'Weighted pull-up', 'poids de corps',
 array['dos'], array['biceps'], false, 2.5, array[]::text[], 'tirage',
 'Ceinture lestée, même exécution qu''une traction. Charge modeste au départ.',
 'Weight belt, same execution as a pull-up. Start with a modest load.',
 'Amplitude écourtée dès que la charge monte.', 'Cutting the range short as the load rises.', false, null),

('shrug-barre', 'Shrug barre', 'Barbell shrug', 'barre',
 array['trapezes'], array[]::text[], false, 2.5, array['cervicale'], 'tirage',
 'Barre devant les cuisses, montez les épaules verticalement.',
 'Bar in front of the thighs, shrug the shoulders straight up.',
 'Rotation des épaules, tête qui avance.', 'Rolling the shoulders, head jutting forward.', false, null),

('high-pull', 'Tirage menton large', 'Wide upright row', 'barre',
 array['trapezes'], array['deltoides'], false, 2.5, array['epaule','cervicale'], 'tirage',
 'Prise large, montez les coudes jusqu''à hauteur d''épaules, pas plus haut.',
 'Wide grip, raise the elbows to shoulder height, no higher.',
 'Prise serrée et montée haute, qui pincent l''épaule.', 'Narrow grip and high pull, impinging the shoulder.', false, null),

-- ---------------------------------------------------------------------
-- Épaules
-- ---------------------------------------------------------------------
('developpe-arnold', 'Développé Arnold', 'Arnold press', 'haltères',
 array['deltoides'], array['triceps'], false, 2, array['epaule','cervicale'], 'poussee',
 'Départ paumes vers soi, pivotez en montant jusqu''à paumes vers l''avant.',
 'Start with palms facing you, rotate as you press to palms forward.',
 'Rotation trop rapide, charge excessive.', 'Rotating too fast, load too heavy.', false, null),

('elevation-laterale-poulie', 'Élévation latérale poulie', 'Cable lateral raise', 'poulie',
 array['deltoides'], array[]::text[], true, 1, array[]::text[], 'aucun',
 'Poulie basse derrière le corps, montez le bras sur le côté jusqu''à l''horizontale.',
 'Low pulley behind the body, raise the arm out to horizontal.',
 'Buste qui s''incline, montée au-dessus de l''épaule.', 'Torso leaning, raising above shoulder height.', false, null),

('elevation-laterale-machine', 'Élévation latérale machine', 'Machine lateral raise', 'machine',
 array['deltoides'], array[]::text[], false, 2.5, array[]::text[], 'aucun',
 'Coudes contre les coussins, montez jusqu''à l''horizontale.',
 'Elbows against the pads, raise to horizontal.',
 'Élan du buste, amplitude excessive.', 'Using body English, excessive range.', false, null),

('oiseau-machine', 'Oiseau machine', 'Reverse pec deck', 'machine',
 array['deltoides'], array['trapezes'], false, 2.5, array[]::text[], 'tirage',
 'Poitrine contre le dossier, ouvrez les bras en arrière sans cambrer.',
 'Chest against the pad, open the arms back without arching.',
 'Décollement de la poitrine, charge trop lourde.', 'Chest lifting off the pad, load too heavy.', false, null),

('developpe-epaules-machine', 'Développé épaules machine', 'Machine shoulder press', 'machine',
 array['deltoides'], array['triceps'], false, 2.5, array['epaule','cervicale'], 'poussee',
 'Poignées à hauteur des oreilles, poussez sans verrouiller sèchement.',
 'Handles at ear height, press without harsh lockout.',
 'Siège trop bas, cambrure lombaire.', 'Seat too low, lower-back arching.', false, null),

('rotation-interne-poulie', 'Rotation interne', 'Cable internal rotation', 'poulie',
 array['deltoides'], array[]::text[], true, 1, array[]::text[], 'aucun',
 'Coude collé au corps à 90°, ramenez l''avant-bras vers le ventre. Charge légère.',
 'Elbow pinned at 90°, bring the forearm toward the stomach. Light load.',
 'Coude qui décolle, buste qui pivote.', 'Elbow drifting, torso rotating.', false, null),

-- ---------------------------------------------------------------------
-- Bras
-- ---------------------------------------------------------------------
('curl-pupitre', 'Curl pupitre', 'Preacher curl', 'barre',
 array['biceps'], array[]::text[], false, 2.5, array[]::text[], 'tirage',
 'Bras posés sur le pupitre, descendez sans tendre complètement d''un coup.',
 'Arms on the pad, lower without snapping into full extension.',
 'Extension brutale en bas, décollement des coudes.', 'Snapping into extension, elbows lifting.', false, null),

('curl-incline', 'Curl incliné', 'Incline dumbbell curl', 'haltères',
 array['biceps'], array[]::text[], true, 2, array[]::text[], 'tirage',
 'Assis sur un banc incliné, bras pendants. Montez sans avancer les coudes.',
 'Seated on an incline bench, arms hanging. Curl without letting the elbows travel forward.',
 'Coudes qui avancent, épaules qui montent.', 'Elbows drifting forward, shoulders rising.', false, null),

('curl-poulie', 'Curl à la poulie', 'Cable curl', 'poulie',
 array['biceps'], array[]::text[], false, 2.5, array[]::text[], 'tirage',
 'Poulie basse, coudes fixes le long du corps.',
 'Low pulley, elbows fixed at the sides.',
 'Buste qui recule, coudes qui remontent.', 'Torso leaning back, elbows rising.', false, null),

('curl-concentre', 'Curl concentré', 'Concentration curl', 'haltère',
 array['biceps'], array[]::text[], true, 2, array[]::text[], 'tirage',
 'Assis, coude calé contre la cuisse. Montez sans bouger le bras.',
 'Seated, elbow braced against the thigh. Curl without moving the upper arm.',
 'Élan du dos, coude qui glisse.', 'Swinging from the back, elbow sliding.', false, null),

('extension-triceps-corde', 'Extension à la corde', 'Rope pushdown', 'poulie',
 array['triceps'], array[]::text[], false, 2.5, array[]::text[], 'poussee',
 'Coudes collés, écartez la corde en fin d''extension.',
 'Elbows pinned, spread the rope at the end of the extension.',
 'Coudes qui s''écartent, buste qui pousse.', 'Elbows flaring, leaning in to push.', false, null),

('kickback-triceps', 'Kickback triceps', 'Triceps kickback', 'haltère',
 array['triceps'], array[]::text[], true, 2, array[]::text[], 'poussee',
 'Buste penché, bras collé au corps. Tendez l''avant-bras vers l''arrière.',
 'Torso hinged, upper arm against the side. Extend the forearm back.',
 'Bras qui bouge, élan de l''épaule.', 'Upper arm moving, swinging from the shoulder.', false, null),

('extension-triceps-unilaterale', 'Extension unilatérale', 'Single-arm overhead extension', 'haltère',
 array['triceps'], array[]::text[], true, 2, array['epaule','cervicale'], 'poussee',
 'Haltère au-dessus de la tête, coude fixe. Descendez derrière la nuque.',
 'Dumbbell overhead, elbow fixed. Lower behind the head.',
 'Coude qui s''écarte, cambrure.', 'Elbow flaring, arching.', false, null),

('dips-machine', 'Dips machine', 'Assisted dip machine', 'machine',
 array['triceps'], array['pectoraux'], false, 2.5, array['epaule'], 'poussee',
 'Buste vertical, descendez jusqu''au parallèle. L''assistance allège, elle ne pousse pas.',
 'Upright torso, lower to parallel. The assistance lightens, it does not push.',
 'Descente trop profonde, appui sur l''assistance.', 'Dropping too deep, resting on the assistance.', false, null),

('curl-barre-ez', 'Curl barre EZ', 'EZ-bar curl', 'barre',
 array['biceps'], array['avant-bras'], false, 2.5, array[]::text[], 'tirage',
 'Prise sur les angles de la barre, poignets détendus. Coudes fixes.',
 'Grip the angled sections, wrists relaxed. Elbows fixed.',
 'Balancement lombaire, poignets cassés.', 'Swinging from the lower back, bent wrists.', false, null),

-- ---------------------------------------------------------------------
-- Avant-bras et grip
-- ---------------------------------------------------------------------
('curl-poignet', 'Curl poignet', 'Wrist curl', 'barre',
 array['avant-bras'], array[]::text[], false, 1, array[]::text[], 'tirage',
 'Avant-bras posés sur les cuisses, laissez la barre rouler puis refermez les doigts.',
 'Forearms on the thighs, let the bar roll to the fingers then curl it back.',
 'Amplitude nulle, charge trop lourde.', 'No range, load too heavy.', false, null),

('extension-poignet', 'Extension poignet', 'Reverse wrist curl', 'barre',
 array['avant-bras'], array[]::text[], false, 1, array[]::text[], 'poussee',
 'Paumes vers le bas, remontez le dos de la main. Charge très légère.',
 'Palms down, lift the back of the hand. Very light load.',
 'Charge excessive, mouvement des coudes.', 'Load too heavy, elbows moving.', false, null),

('farmer-walk', 'Marche du fermier', 'Farmer''s walk', 'haltères',
 array['avant-bras','trapezes'], array['abdominaux'], false, 2, array[]::text[], 'aucun',
 'Charges lourdes dans chaque main, marchez droit, épaules basses.',
 'Heavy load in each hand, walk tall with the shoulders down.',
 'Buste penché, épaules enroulées.', 'Leaning torso, shoulders rolled forward.', false, null),

('suspension-barre', 'Suspension à la barre', 'Dead hang', 'poids de corps',
 array['avant-bras'], array['dos'], false, 2.5, array[]::text[], 'tirage',
 'Suspendu bras tendus, épaules actives et non relâchées.',
 'Hanging with straight arms, shoulders active rather than fully relaxed.',
 'Épaules totalement relâchées, balancement.', 'Fully passive shoulders, swinging.', false, null),

-- ---------------------------------------------------------------------
-- Jambes — machines et variantes
-- ---------------------------------------------------------------------
('hack-squat', 'Hack squat', 'Hack squat', 'machine',
 array['quadriceps'], array['fessiers'], false, 5, array['genou'], 'aucun',
 'Dos plaqué, pieds à mi-plateforme. Descendez jusqu''au parallèle sans décoller le bassin.',
 'Back flat on the pad, feet mid-platform. Lower to parallel without the hips lifting.',
 'Talons qui décollent, descente au-delà du contrôle.', 'Heels lifting, descending beyond control.', false, null),

('presse-une-jambe', 'Presse une jambe', 'Single-leg press', 'machine',
 array['quadriceps','fessiers'], array['ischio-jambiers'], true, 5, array[]::text[], 'aucun',
 'Un pied centré sur la plateforme, l''autre au repos. Poussez sans verrouiller.',
 'One foot centred on the platform, the other resting. Press without locking out.',
 'Bassin qui pivote, amplitude inégale entre les côtés.', 'Hips rotating, uneven range between sides.', false, null),

('fente-arriere', 'Fente arrière', 'Reverse lunge', 'haltères',
 array['quadriceps','fessiers'], array['ischio-jambiers'], true, 2, array[]::text[], 'aucun',
 'Grand pas en arrière, descendez verticalement. Moins de contrainte au genou avant que la fente avant.',
 'Long step backward, descend vertically. Less knee stress than a forward lunge.',
 'Pas trop court, buste qui s''effondre.', 'Step too short, torso collapsing.', false, null),

('fente-marchee', 'Fente marchée', 'Walking lunge', 'haltères',
 array['quadriceps','fessiers'], array['ischio-jambiers'], true, 2, array['genou'], 'aucun',
 'Enchaînez les pas en avançant, buste droit.',
 'Chain the steps moving forward, torso upright.',
 'Pas trop courts, déséquilibre latéral.', 'Steps too short, lateral instability.', false, null),

('squat-sumo', 'Squat sumo', 'Sumo squat', 'haltère',
 array['quadriceps','fessiers'], array['adducteurs'], false, 2, array[]::text[], 'aucun',
 'Pieds très écartés, pointes vers l''extérieur. Descendez entre les jambes, buste droit.',
 'Feet wide, toes turned out. Sit between the legs with an upright torso.',
 'Genoux qui rentrent, buste qui bascule.', 'Knees caving in, torso tipping.', false, null),

('souleve-de-terre-sumo', 'Soulevé de terre sumo', 'Sumo deadlift', 'barre',
 array['fessiers','quadriceps'], array['adducteurs','dos'], false, 5, array['lombaire'], 'tirage',
 'Pieds larges, prise entre les jambes. Poussez le sol, buste plus vertical qu''en conventionnel.',
 'Wide stance, grip inside the legs. Push the floor, torso more upright than conventional.',
 'Hanches qui montent avant la barre, genoux qui rentrent.', 'Hips shooting up before the bar, knees caving.', false, null),

('adducteurs-machine', 'Adducteurs machine', 'Hip adduction machine', 'machine',
 array['adducteurs'], array[]::text[], false, 2.5, array[]::text[], 'aucun',
 'Serrez les cuisses en contrôlant le retour. Amplitude progressive.',
 'Squeeze the thighs together and control the return. Build the range gradually.',
 'Amplitude d''ouverture excessive à froid.', 'Excessive opening range when cold.', false, null),

('abducteurs-machine', 'Abducteurs machine', 'Hip abduction machine', 'machine',
 array['fessiers'], array[]::text[], false, 2.5, array[]::text[], 'aucun',
 'Écartez les cuisses en gardant le dos appuyé. Buste légèrement penché pour cibler le moyen fessier.',
 'Push the thighs apart with the back supported. Lean forward slightly to target the glute medius.',
 'Élan, dos décollé.', 'Using momentum, back leaving the pad.', false, null),

('leg-curl-debout', 'Leg curl debout', 'Standing leg curl', 'machine',
 array['ischio-jambiers'], array[]::text[], true, 2.5, array[]::text[], 'aucun',
 'Une jambe à la fois, hanche fixe. Ramenez le talon vers le fessier.',
 'One leg at a time, hip fixed. Curl the heel toward the glute.',
 'Bassin qui bascule, élan.', 'Hips tilting, using momentum.', false, null),

('glute-kickback', 'Kickback fessier', 'Cable glute kickback', 'poulie',
 array['fessiers'], array['ischio-jambiers'], true, 2.5, array['lombaire'], 'aucun',
 'Sanglé à la cheville, poussez la jambe en arrière sans cambrer le bas du dos.',
 'Ankle strap, drive the leg back without arching the lower back.',
 'Hyperextension lombaire pour gagner de l''amplitude.', 'Arching the lower back to gain range.', false, null),

('romanian-une-jambe', 'Soulevé une jambe', 'Single-leg RDL', 'haltère',
 array['ischio-jambiers','fessiers'], array['lombaires'], true, 2, array['lombaire'], 'tirage',
 'En appui sur une jambe, penchez le buste en levant la jambe libre. Hanches parallèles au sol.',
 'Balanced on one leg, hinge forward raising the free leg. Keep the hips square.',
 'Ouverture de la hanche, perte d''équilibre qui arrondit le dos.', 'Hip opening up, losing balance and rounding the back.', false, null),

('sissy-squat', 'Sissy squat', 'Sissy squat', 'poids de corps',
 array['quadriceps'], array[]::text[], false, 2.5, array['genou'], 'aucun',
 'Genoux qui avancent, buste et cuisses alignés. Amplitude progressive et contrôlée.',
 'Knees travelling forward, torso and thighs in line. Build the range gradually and under control.',
 'Amplitude maximale d''emblée, appui qui glisse.', 'Going to full range immediately, support slipping.', false, null),

('mollets-ane', 'Mollets penché', 'Donkey calf raise', 'machine',
 array['mollets'], array[]::text[], false, 2.5, array[]::text[], 'aucun',
 'Buste penché, charge sur les hanches. Montez sur la pointe des pieds.',
 'Torso hinged, load on the hips. Rise onto the toes.',
 'Rebond, genoux qui plient.', 'Bouncing, knees bending.', false, null),

-- ---------------------------------------------------------------------
-- Abdominaux et obliques
-- ---------------------------------------------------------------------
('crunch-sol', 'Crunch au sol', 'Floor crunch', 'poids de corps',
 array['abdominaux'], array[]::text[], false, 2.5, array['cervicale'], 'aucun',
 'Mains aux tempes sans tirer, décollez les omoplates en soufflant.',
 'Hands at the temples without pulling, lift the shoulder blades while exhaling.',
 'Traction sur la nuque, amplitude excessive.', 'Pulling on the neck, excessive range.', false, null),

('crunch-machine', 'Crunch machine', 'Machine crunch', 'machine',
 array['abdominaux'], array[]::text[], false, 2.5, array['cervicale'], 'aucun',
 'Enroulez le buste contre la résistance, sans tirer sur les poignées avec les bras.',
 'Curl the torso against the resistance, without pulling with the arms.',
 'Mouvement de hanches, traction des bras.', 'Hinging at the hips, pulling with the arms.', false, null),

('releve-jambes-sol', 'Relevé de jambes au sol', 'Lying leg raise', 'poids de corps',
 array['abdominaux'], array[]::text[], false, 2.5, array['lombaire'], 'aucun',
 'Lombaires plaquées au sol, descendez les jambes tant que le bas du dos reste collé.',
 'Lower back pressed to the floor, lower the legs only while it stays flat.',
 'Décollement lombaire, élan.', 'Lower back lifting, using momentum.', false, null),

('russian-twist', 'Russian twist', 'Russian twist', 'haltère',
 array['abdominaux'], array[]::text[], false, 2, array['lombaire'], 'aucun',
 'Assis en équilibre, pivotez le buste d''un côté à l''autre en contrôlant.',
 'Seated and balanced, rotate the torso side to side under control.',
 'Rotation trop rapide, dos rond.', 'Rotating too fast, rounded back.', false, null),

('wood-chop', 'Wood chop', 'Cable wood chop', 'poulie',
 array['abdominaux'], array['deltoides'], true, 2.5, array['lombaire'], 'aucun',
 'Poulie haute, tirez en diagonale vers la hanche opposée. Le mouvement vient du tronc.',
 'High pulley, pull diagonally to the opposite hip. The movement comes from the trunk.',
 'Traction des bras seuls, rotation lombaire forcée.', 'Pulling with the arms only, forcing lumbar rotation.', false, null),

('hollow-hold', 'Hollow hold', 'Hollow hold', 'poids de corps',
 array['abdominaux'], array[]::text[], false, 2.5, array[]::text[], 'aucun',
 'Sur le dos, lombaires plaquées, bras et jambes tendus près du sol.',
 'On the back, lower spine pressed down, arms and legs extended close to the floor.',
 'Lombaires qui décollent, apnée.', 'Lower back lifting, holding the breath.', false, null),

('planche-dynamique', 'Planche dynamique', 'Plank up-down', 'poids de corps',
 array['abdominaux'], array['triceps','deltoides'], false, 2.5, array[]::text[], 'aucun',
 'Depuis la planche sur coudes, montez sur les mains puis redescendez. Bassin stable.',
 'From the forearm plank, rise to the hands then back down. Keep the hips still.',
 'Bassin qui roule, cadence trop rapide.', 'Hips rocking, moving too fast.', false, null),

('gainage-lateral-dynamique', 'Gainage latéral dynamique', 'Side plank hip dip', 'poids de corps',
 array['abdominaux'], array['fessiers'], true, 2.5, array[]::text[], 'aucun',
 'En gainage latéral, descendez la hanche puis remontez sans toucher le sol.',
 'From the side plank, lower the hip then lift without touching the floor.',
 'Bascule du buste, épaule qui s''affaisse.', 'Torso rotating, shoulder collapsing.', false, null),

-- ---------------------------------------------------------------------
-- Kettlebell et fonctionnel
-- ---------------------------------------------------------------------
('kettlebell-swing', 'Swing kettlebell', 'Kettlebell swing', 'kettlebell',
 array['fessiers','ischio-jambiers'], array['dos','abdominaux'], false, 2, array['lombaire'], 'aucun',
 'Charnière de hanche explosive, la kettlebell monte par l''élan des hanches, pas des bras.',
 'Explosive hip hinge; the bell rises from hip drive, not from the arms.',
 'Squat au lieu de charnière, montée à la force des épaules.', 'Squatting instead of hinging, lifting with the shoulders.', false, null),

('goblet-squat-kettlebell', 'Squat gobelet kettlebell', 'Kettlebell goblet squat', 'kettlebell',
 array['quadriceps','fessiers'], array['abdominaux'], false, 2, array[]::text[], 'aucun',
 'Kettlebell contre la poitrine, descendez entre les genoux.',
 'Bell against the chest, sit between the knees.',
 'Buste qui bascule, talons qui décollent.', 'Torso tipping, heels lifting.', false, null),

('press-kettlebell', 'Développé kettlebell', 'Kettlebell press', 'kettlebell',
 array['deltoides'], array['triceps','abdominaux'], true, 2, array['epaule','cervicale'], 'poussee',
 'Kettlebell en rack sur l''avant-bras, poussez à la verticale en gainant.',
 'Bell racked on the forearm, press vertically with the core braced.',
 'Cambrure lombaire, poignet cassé.', 'Lower-back arching, bent wrist.', false, null),

-- ---------------------------------------------------------------------
-- Élastiques — reprise et rééducation
-- ---------------------------------------------------------------------
('rotation-externe-elastique', 'Rotation externe élastique', 'Band external rotation', 'élastique',
 array['deltoides'], array[]::text[], true, 1, array[]::text[], 'aucun',
 'Coude au corps à 90°, ouvrez l''avant-bras contre l''élastique. Résistance faible.',
 'Elbow at the side at 90°, rotate the forearm out against the band. Light resistance.',
 'Coude qui décolle, résistance trop forte.', 'Elbow drifting, resistance too strong.', false, null),

('pull-apart-elastique', 'Pull-apart élastique', 'Band pull-apart', 'élastique',
 array['trapezes'], array['deltoides'], false, 1, array[]::text[], 'tirage',
 'Bras tendus devant, écartez l''élastique jusqu''à la poitrine en serrant les omoplates.',
 'Arms straight in front, pull the band apart to the chest while squeezing the shoulder blades.',
 'Épaules qui montent, coudes qui se plient.', 'Shoulders shrugging, elbows bending.', false, null),

('monster-walk', 'Marche latérale élastique', 'Banded lateral walk', 'élastique',
 array['fessiers'], array[]::text[], false, 1, array[]::text[], 'aucun',
 'Élastique aux genoux ou aux chevilles, pas de côté en gardant la tension.',
 'Band at the knees or ankles, step sideways keeping the tension.',
 'Genoux qui rentrent, buste qui se penche.', 'Knees caving, torso leaning.', false, null),

-- ---------------------------------------------------------------------
-- Compléments poulie
-- ---------------------------------------------------------------------
('face-pull-genoux', 'Face pull à genoux', 'Half-kneeling face pull', 'poulie',
 array['trapezes'], array['deltoides'], false, 1, array[]::text[], 'tirage',
 'À genoux, ce qui supprime l''élan des jambes. Tirez vers le front, coudes hauts.',
 'Half-kneeling, which removes leg drive. Pull to the forehead with high elbows.',
 'Charge trop lourde, coudes qui tombent.', 'Load too heavy, elbows dropping.', false, null),

('tirage-corde-nuque', 'Tirage corde vers le visage', 'Rope pull to face', 'poulie',
 array['trapezes'], array['deltoides'], false, 1, array[]::text[], 'tirage',
 'Corde à hauteur du visage, séparez les brins en fin de tirage.',
 'Rope at face height, separate the strands at the end of the pull.',
 'Mouvement de biceps, épaules qui montent.', 'Turning it into a curl, shoulders shrugging.', false, null),

('crunch-inverse', 'Crunch inversé', 'Reverse crunch', 'poids de corps',
 array['abdominaux'], array[]::text[], false, 2.5, array[]::text[], 'aucun',
 'Sur le dos, enroulez le bassin vers la poitrine sans élan.',
 'On the back, curl the pelvis toward the chest without momentum.',
 'Balancement des jambes, décollement lombaire non contrôlé.', 'Swinging the legs, uncontrolled lower-back lift.', false, null),

('extension-mollets-poulie', 'Mollets à la poulie', 'Cable calf raise', 'poulie',
 array['mollets'], array[]::text[], true, 2.5, array[]::text[], 'aucun',
 'Une jambe à la fois, montez sur la pointe en contrôlant la descente.',
 'One leg at a time, rise onto the toes and control the descent.',
 'Rebond, appui instable.', 'Bouncing, unstable support.', false, null),

-- ---------------------------------------------------------------------
-- Compléments — le seuil de 150 de `14-contenu.md` § 1
-- ---------------------------------------------------------------------
('floor-press', 'Développé au sol', 'Floor press', 'barre',
 array['pectoraux'], array['triceps'], false, 2.5, array[]::text[], 'poussee',
 'Allongé au sol, les coudes touchent le sol en bas. L''amplitude réduite ménage l''épaule.',
 'Lying on the floor, elbows touch down at the bottom. The reduced range spares the shoulder.',
 'Rebond des coudes sur le sol.', 'Bouncing the elbows off the floor.', false, null),

('pompes-diamant', 'Pompes diamant', 'Diamond push-up', 'poids de corps',
 array['triceps'], array['pectoraux'], false, 2.5, array[]::text[], 'poussee',
 'Mains jointes sous la poitrine, coudes le long du corps.',
 'Hands together under the chest, elbows tucked to the sides.',
 'Coudes en croix, bassin qui s''affaisse.', 'Elbows flaring, hips sagging.', false, null),

('pompes-prise-large', 'Pompes prise large', 'Wide push-up', 'poids de corps',
 array['pectoraux'], array['deltoides'], false, 2.5, array['epaule'], 'poussee',
 'Mains plus larges que les épaules, descendez sans forcer l''ouverture.',
 'Hands wider than the shoulders, lower without forcing the stretch.',
 'Amplitude excessive qui met l''épaule en tension.', 'Excessive range straining the shoulder.', false, null),

('rowing-yates', 'Rowing Yates', 'Yates row', 'barre',
 array['dos'], array['biceps','trapezes'], false, 2.5, array['lombaire'], 'tirage',
 'Buste incliné à 60° seulement, prise en supination. Tirez vers le bas du ventre.',
 'Torso hinged to only 60°, underhand grip. Pull to the lower abdomen.',
 'Buste qui se redresse, élan lombaire.', 'Torso rising, swinging from the lower back.', false, null),

('tirage-vertical-supination', 'Tirage supination', 'Reverse-grip pulldown', 'poulie',
 array['dos'], array['biceps'], false, 2.5, array[]::text[], 'tirage',
 'Prise en supination largeur d''épaules, tirez vers le haut de la poitrine.',
 'Underhand grip at shoulder width, pull to the upper chest.',
 'Poignets cassés, buste qui bascule loin en arrière.', 'Bent wrists, torso leaning far back.', false, null),

('elevation-frontale-poulie', 'Élévation frontale poulie', 'Cable front raise', 'poulie',
 array['deltoides'], array['pectoraux'], false, 1, array['epaule'], 'poussee',
 'Poulie basse derrière soi, montez le bras tendu jusqu''à l''horizontale.',
 'Low pulley behind you, raise the straight arm to horizontal.',
 'Montée trop haute, balancement.', 'Raising too high, swinging.', false, null),

('developpe-halteres-unilateral', 'Développé épaule unilatéral', 'Single-arm shoulder press', 'haltère',
 array['deltoides'], array['abdominaux','triceps'], true, 2, array['epaule','cervicale'], 'poussee',
 'Un bras à la fois, gainage serré pour empêcher l''inclinaison latérale.',
 'One arm at a time, brace hard to prevent leaning sideways.',
 'Inclinaison du buste, cambrure.', 'Torso leaning, arching.', false, null),

('curl-araignee', 'Curl araignée', 'Spider curl', 'haltères',
 array['biceps'], array[]::text[], false, 2, array[]::text[], 'tirage',
 'Poitrine appuyée sur un banc incliné, bras pendants à la verticale.',
 'Chest on an incline bench, arms hanging vertically.',
 'Décollement de la poitrine, élan.', 'Chest lifting off the bench, swinging.', false, null),

('curl-inverse', 'Curl inversé', 'Reverse curl', 'barre',
 array['avant-bras'], array['biceps'], false, 2.5, array[]::text[], 'tirage',
 'Prise en pronation, coudes fixes. Charge plus légère qu''un curl classique.',
 'Overhand grip, elbows fixed. Lighter load than a standard curl.',
 'Poignets qui cassent, coudes qui avancent.', 'Wrists bending, elbows drifting forward.', false, null),

('bench-dip', 'Dips sur banc', 'Bench dip', 'poids de corps',
 array['triceps'], array['deltoides'], false, 2.5, array['epaule'], 'poussee',
 'Mains sur un banc derrière soi, descendez en gardant les coudes vers l''arrière.',
 'Hands on a bench behind you, lower keeping the elbows pointing back.',
 'Descente trop profonde, épaules en rotation interne forcée.', 'Dropping too deep, shoulders forced into internal rotation.', false, null),

('fente-bulgare-barre', 'Fente bulgare barre', 'Barbell split squat', 'barre',
 array['quadriceps','fessiers'], array['ischio-jambiers'], true, 2.5, array['genou','cervicale'], 'aucun',
 'Barre sur les trapèzes, pied arrière surélevé. Descendez verticalement.',
 'Bar on the traps, rear foot elevated. Descend vertically.',
 'Perte d''équilibre, buste qui bascule.', 'Losing balance, torso tipping.', false, null),

('presse-pieds-hauts', 'Presse pieds hauts', 'High-foot leg press', 'machine',
 array['fessiers','ischio-jambiers'], array['quadriceps'], false, 5, array[]::text[], 'aucun',
 'Pieds hauts sur la plateforme, ce qui sollicite davantage les fessiers.',
 'Feet high on the platform, which shifts the emphasis to the glutes.',
 'Bassin qui décolle en bas, talons qui sortent.', 'Hips lifting at the bottom, heels off the plate.', false, null),

('extension-jambes-unilaterale', 'Extension unilatérale', 'Single-leg extension', 'machine',
 array['quadriceps'], array[]::text[], true, 2.5, array['genou'], 'aucun',
 'Une jambe à la fois, ce qui révèle les écarts de force entre les côtés.',
 'One leg at a time, which reveals strength differences between sides.',
 'Verrouillage sec, bassin qui pivote.', 'Snapping into lockout, hips rotating.', false, null),

('box-squat', 'Squat sur boîte', 'Box squat', 'barre',
 array['quadriceps','fessiers'], array['ischio-jambiers'], false, 5, array['cervicale','lombaire'], 'aucun',
 'Descendez jusqu''à effleurer la boîte, marquez un temps, remontez sans rebondir.',
 'Lower until you touch the box, pause, then rise without bouncing.',
 'Rebond sur la boîte, relâchement du dos en bas.', 'Bouncing off the box, releasing the back at the bottom.', false, null),

('step-down', 'Descente contrôlée', 'Step-down', 'poids de corps',
 array['quadriceps','fessiers'], array[]::text[], true, 2.5, array['genou'], 'aucun',
 'Debout sur une marche, descendez lentement l''autre pied vers le sol sans y prendre appui.',
 'Standing on a step, slowly lower the other foot toward the floor without weighting it.',
 'Descente lâchée, genou qui rentre.', 'Dropping down, knee caving inward.', false, null),

('good-morning-elastique', 'Good morning élastique', 'Banded good morning', 'élastique',
 array['ischio-jambiers','fessiers'], array['lombaires'], false, 1, array['lombaire'], 'aucun',
 'Élastique sur la nuque et sous les pieds, charnière de hanche avec dos neutre.',
 'Band over the neck and under the feet, hip hinge with a neutral spine.',
 'Dos rond, amplitude excessive.', 'Rounded back, excessive range.', false, null),

('sit-up', 'Sit-up', 'Sit-up', 'poids de corps',
 array['abdominaux'], array[]::text[], false, 2.5, array['lombaire','cervicale'], 'aucun',
 'Remontez le buste jusqu''à l''assise en enroulant vertèbre par vertèbre.',
 'Curl the torso up to seated, one vertebra at a time.',
 'Élan des bras, traction sur la nuque.', 'Swinging the arms, pulling on the neck.', false, null),

('mountain-climber', 'Mountain climber', 'Mountain climber', 'poids de corps',
 array['abdominaux'], array['deltoides'], false, 2.5, array[]::text[], 'aucun',
 'En position de planche, ramenez alternativement les genoux vers la poitrine. Bassin stable.',
 'From a plank, alternately drive the knees to the chest. Keep the hips level.',
 'Bassin qui monte, cadence au détriment de la posture.', 'Hips rising, speed at the cost of position.', false, null),

('bird-dog', 'Bird-dog', 'Bird dog', 'poids de corps',
 array['lombaires'], array['abdominaux','fessiers'], true, 2.5, array[]::text[], 'aucun',
 'À quatre pattes, tendez bras et jambe opposés sans laisser le bassin tourner.',
 'On all fours, extend the opposite arm and leg without letting the hips rotate.',
 'Rotation du bassin, cambrure lombaire.', 'Hips rotating, lower-back arching.', false, null),

('gainage-bras-tendus', 'Planche bras tendus', 'High plank', 'poids de corps',
 array['abdominaux'], array['deltoides'], false, 2.5, array[]::text[], 'aucun',
 'Mains sous les épaules, corps aligné, omoplates légèrement écartées.',
 'Hands under the shoulders, body in line, shoulder blades slightly spread.',
 'Bassin haut, coudes verrouillés en hyperextension.', 'Hips too high, elbows hyperextended.', false, null),

('mollets-une-jambe', 'Mollets une jambe', 'Single-leg calf raise', 'poids de corps',
 array['mollets'], array[]::text[], true, 2.5, array[]::text[], 'aucun',
 'Sur une marche, montez sur la pointe d''un pied puis descendez en étirant.',
 'On a step, rise onto the toes of one foot then lower into the stretch.',
 'Appui de la main qui porte le poids, rebond.', 'Leaning on the hand for support, bouncing.', false, null),

('turkish-get-up', 'Turkish get-up', 'Turkish get-up', 'kettlebell',
 array['abdominaux','deltoides'], array['fessiers','quadriceps'], true, 2, array['epaule'], 'aucun',
 'Passage du sol à debout, bras tendu au-dessus, en gardant la charge à la verticale.',
 'Move from lying to standing with the arm locked overhead, keeping the load vertical.',
 'Bras qui plie, mouvement précipité.', 'Arm bending, rushing the sequence.', false, null),

('bear-crawl', 'Marche de l''ours', 'Bear crawl', 'poids de corps',
 array['abdominaux'], array['deltoides','quadriceps'], false, 2.5, array[]::text[], 'aucun',
 'Genoux à quelques centimètres du sol, avancez main et pied opposés. Bassin bas et stable.',
 'Knees hovering just off the floor, move the opposite hand and foot. Hips low and steady.',
 'Bassin qui monte, genoux qui touchent.', 'Hips rising, knees touching down.', false, null),

('rowing-elastique', 'Rowing élastique', 'Band row', 'élastique',
 array['dos'], array['biceps'], false, 1, array[]::text[], 'tirage',
 'Élastique ancré devant, tirez les coudes vers les côtes en serrant les omoplates.',
 'Band anchored in front, drive the elbows to the ribs while squeezing the shoulder blades.',
 'Épaules qui montent, buste qui recule.', 'Shoulders shrugging, torso leaning back.', false, null),

('hip-thrust-une-jambe', 'Hip thrust une jambe', 'Single-leg hip thrust', 'poids de corps',
 array['fessiers'], array['ischio-jambiers'], true, 2.5, array[]::text[], 'aucun',
 'Dos sur un banc, une jambe tendue. Poussez la hanche sans laisser le bassin tourner.',
 'Back on a bench, one leg extended. Drive the hip up without letting the pelvis rotate.',
 'Bascule du bassin, hyperextension lombaire.', 'Pelvis tilting, lower-back overextension.', false, null)

on conflict (slug) where is_custom = false do update set
  name_fr = excluded.name_fr, name_en = excluded.name_en,
  equipment = excluded.equipment,
  primary_muscles = excluded.primary_muscles,
  secondary_muscles = excluded.secondary_muscles,
  is_unilateral = excluded.is_unilateral,
  default_increment = excluded.default_increment,
  contraindicated_for = excluded.contraindicated_for,
  movement_role = excluded.movement_role,
  instructions_fr = excluded.instructions_fr,
  instructions_en = excluded.instructions_en,
  common_errors_fr = excluded.common_errors_fr,
  common_errors_en = excluded.common_errors_en;

-- =====================================================================
-- Les variantes de l'extension
-- =====================================================================
insert into public.exercise_variants (exercise_id, variant_id)
select a.id, b.id
  from (values
    ('developpe-couche-barre',   'developpe-decline'),
    ('ecarte-couche',            'pec-deck'),
    ('pec-deck',                 'ecarte-poulie'),
    ('ecarte-incline',           'ecarte-poulie-basse'),
    ('pompes',                   'pompes-inclinees'),
    ('pompes',                   'pompes-declinees'),
    ('rowing-barre',             'rowing-t-bar'),
    ('rowing-t-bar',             'seal-row'),
    ('tirage-horizontal',        'tirage-unilateral-poulie'),
    ('pull-over-poulie',         'pull-over-haltere'),
    ('tirage-vertical',          'tirage-vertical-prise-serree'),
    ('traction-pronation',       'traction-lestee'),
    ('shrug',                    'shrug-barre'),
    ('developpe-militaire',      'developpe-arnold'),
    ('developpe-epaules-halteres','developpe-epaules-machine'),
    ('elevation-laterale',       'elevation-laterale-poulie'),
    ('elevation-laterale',       'elevation-laterale-machine'),
    ('oiseau',                   'oiseau-machine'),
    ('rotation-externe-poulie',  'rotation-externe-elastique'),
    ('curl-barre',               'curl-barre-ez'),
    ('curl-barre',               'curl-pupitre'),
    ('curl-halteres',            'curl-incline'),
    ('curl-halteres',            'curl-concentre'),
    ('curl-halteres',            'curl-poulie'),
    ('extension-triceps-poulie', 'extension-triceps-corde'),
    ('extension-triceps-nuque',  'extension-triceps-unilaterale'),
    ('dips-triceps',             'dips-machine'),
    ('extension-triceps-poulie', 'kickback-triceps'),
    ('squat-barre-dos',          'hack-squat'),
    ('presse-a-cuisses',         'presse-une-jambe'),
    ('fente-avant',              'fente-arriere'),
    ('fente-avant',              'fente-marchee'),
    ('squat-gobelet',            'squat-sumo'),
    ('squat-gobelet',            'goblet-squat-kettlebell'),
    ('souleve-de-terre',         'souleve-de-terre-sumo'),
    ('souleve-de-terre-roumain', 'romanian-une-jambe'),
    ('leg-curl-allonge',         'leg-curl-debout'),
    ('abduction-hanche-poulie',  'abducteurs-machine'),
    ('hip-thrust',               'glute-kickback'),
    ('extension-jambes',         'sissy-squat'),
    ('mollets-debout',           'mollets-ane'),
    ('mollets-assis',            'extension-mollets-poulie'),
    ('crunch-poulie',            'crunch-machine'),
    ('crunch-poulie',            'crunch-sol'),
    ('releve-de-jambes-suspendu','releve-jambes-sol'),
    ('releve-de-jambes-suspendu','crunch-inverse'),
    ('pallof-press',             'wood-chop'),
    ('gainage-ventral',          'hollow-hold'),
    ('gainage-ventral',          'planche-dynamique'),
    ('gainage-lateral',          'gainage-lateral-dynamique'),
    ('face-pull',                'face-pull-genoux'),
    ('face-pull',                'tirage-corde-nuque'),
    ('y-t-w',                    'pull-apart-elastique'),
    ('hip-thrust',               'kettlebell-swing'),
    ('developpe-epaules-halteres','press-kettlebell'),
    ('abduction-hanche-poulie',  'monster-walk'),
    ('adducteurs-machine',       'squat-sumo'),
    ('developpe-couche-barre',   'floor-press'),
    ('pompes',                   'pompes-diamant'),
    ('pompes',                   'pompes-prise-large'),
    ('rowing-barre',             'rowing-yates'),
    ('tirage-vertical',          'tirage-vertical-supination'),
    ('elevation-frontale',       'elevation-frontale-poulie'),
    ('developpe-epaules-halteres','developpe-halteres-unilateral'),
    ('curl-pupitre',             'curl-araignee'),
    ('curl-marteau',             'curl-inverse'),
    ('dips-triceps',             'bench-dip'),
    ('fente-bulgare',            'fente-bulgare-barre'),
    ('presse-a-cuisses',         'presse-pieds-hauts'),
    ('extension-jambes',         'extension-jambes-unilaterale'),
    ('squat-barre-dos',          'box-squat'),
    ('step-up',                  'step-down'),
    ('good-morning',             'good-morning-elastique'),
    ('crunch-sol',               'sit-up'),
    ('gainage-ventral',          'mountain-climber'),
    ('dead-bug',                 'bird-dog'),
    ('gainage-ventral',          'gainage-bras-tendus'),
    ('mollets-debout',           'mollets-une-jambe'),
    ('press-kettlebell',         'turkish-get-up'),
    ('planche-dynamique',        'bear-crawl'),
    ('tirage-horizontal',        'rowing-elastique'),
    ('hip-thrust',               'hip-thrust-une-jambe')
  ) as lien(source, cible)
  join public.exercises a on a.slug = lien.source and a.is_custom = false
  join public.exercises b on b.slug = lien.cible  and b.is_custom = false
on conflict (exercise_id, variant_id) do nothing;
