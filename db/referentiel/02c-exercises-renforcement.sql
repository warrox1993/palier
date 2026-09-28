-- =====================================================================
-- Catalogue — renforcement, stabilité et compléments d'équipement
-- =====================================================================
--
-- CE FICHIER N'EST PAS RELU PAR UN PROFESSIONNEL DE SANTÉ, et ici la
-- réserve pèse PLUS LOURD qu'ailleurs.
--
-- Les mouvements ci-dessous appartiennent au vocabulaire de la
-- rééducation : rotateurs de l'épaule, stabilité scapulaire, contrôle
-- lombo-pelvien, proprioception de cheville, rétraction cervicale. Ils sont
-- décrits comme des mouvements de RENFORCEMENT, jamais comme un protocole de
-- soin — `docs/01-conformite.md` § 2 : « un chiffre, une référence, un écart.
-- Jamais une action. »
--
-- CE QUE CE FICHIER NE FAIT PAS, ET NE DOIT JAMAIS FAIRE :
--   • nommer une pathologie (« pour votre tendinite », « en cas de hernie »)
--   • annoncer un effet thérapeutique (« soulage », « guérit », « répare »)
--   • proposer un protocole (« trois fois par jour pendant six semaines »)
--
-- Un exercice de renforcement décrit un geste. Le reste appartient au
-- professionnel qui suit la personne, et `docs/00-produit.md` place cette
-- population — « sortie de kinésithérapie, retour après grossesse, prothèse »
-- — au cœur de la cible précisément parce qu'elle est mal servie par les
-- générateurs d'exercices.
--
-- POURQUOI `02c` ET NON `03`. `docs/16-projet.md` § 4 réserve le numéro `03`
-- à `03-foods.sql`. Les fichiers d'exercices restent groupés sous le `02` que
-- le document leur attribue, et l'ordre alphabétique les applique dans le bon
-- ordre. Le premier jet de l'extension avait pris `03` par inattention.
-- =====================================================================

insert into public.exercises
  (slug, name_fr, name_en, equipment, primary_muscles, secondary_muscles,
   is_unilateral, default_increment, contraindicated_for, movement_role,
   instructions_fr, instructions_en, common_errors_fr, common_errors_en,
   is_custom, owner_id)
values

-- ---------------------------------------------------------------------
-- Coiffe des rotateurs et stabilité d'épaule
-- ---------------------------------------------------------------------
('rotation-externe-couche', 'Rotation externe couché', 'Side-lying external rotation', 'haltère',
 array['deltoides'], array[]::text[], true, 1, array[]::text[], 'aucun',
 'Sur le côté, coude collé aux côtes à 90°. Faites pivoter l''avant-bras vers le plafond. Charge très légère.',
 'Lying on your side, elbow pinned to the ribs at 90°. Rotate the forearm toward the ceiling. Very light load.',
 'Coude qui décolle, charge qui fait rouler le buste.', 'Elbow lifting away, load heavy enough to roll the torso.', false, null),

('rotation-externe-90', 'Rotation externe à 90°', 'External rotation at 90°', 'élastique',
 array['deltoides'], array[]::text[], true, 1, array['epaule'], 'aucun',
 'Bras en abduction à 90°, coude fléchi. Pivotez l''avant-bras vers le haut sans bouger le coude.',
 'Arm abducted to 90°, elbow bent. Rotate the forearm upward without moving the elbow.',
 'Coude qui descend, amplitude forcée en fin de rotation.', 'Elbow dropping, forcing the end of the rotation.', false, null),

('scaption', 'Élévation dans le plan de l''omoplate', 'Scaption raise', 'haltères',
 array['deltoides'], array['trapezes'], false, 1, array[]::text[], 'aucun',
 'Bras à trente degrés en avant du plan frontal, pouces vers le haut. Montez à l''horizontale.',
 'Arms thirty degrees forward of the frontal plane, thumbs up. Raise to horizontal.',
 'Montée au-dessus de l''épaule, pouces vers le bas.', 'Raising above shoulder height, thumbs pointing down.', false, null),

('scapular-pull-up', 'Traction scapulaire', 'Scapular pull-up', 'poids de corps',
 array['trapezes'], array['dos'], false, 2.5, array[]::text[], 'tirage',
 'Suspendu bras tendus, abaissez les épaules sans plier les coudes. Amplitude courte.',
 'Hanging with straight arms, depress the shoulders without bending the elbows. Short range.',
 'Coudes qui se plient, mouvement transformé en traction.', 'Elbows bending, turning it into a pull-up.', false, null),

('wall-slide', 'Glissé au mur', 'Wall slide', 'poids de corps',
 array['trapezes'], array['deltoides'], false, 1, array[]::text[], 'tirage',
 'Dos et avant-bras au mur, montez les bras en gardant le contact. Bas du dos plaqué.',
 'Back and forearms against the wall, slide the arms up keeping contact. Lower back flat.',
 'Perte de contact, cambrure pour gagner de la hauteur.', 'Losing contact, arching to gain height.', false, null),

('serratus-punch', 'Protraction scapulaire', 'Serratus punch', 'haltère',
 array['trapezes'], array['pectoraux'], false, 1, array[]::text[], 'poussee',
 'Allongé, bras tendu vers le plafond. Poussez l''omoplate vers l''avant sans plier le coude.',
 'Lying down, arm extended to the ceiling. Push the shoulder blade forward without bending the elbow.',
 'Flexion du coude, amplitude nulle.', 'Bending the elbow, no range at all.', false, null),

('retraction-scapulaire', 'Rétraction scapulaire', 'Scapular retraction', 'élastique',
 array['trapezes'], array[]::text[], false, 1, array[]::text[], 'tirage',
 'Bras tendus devant, serrez les omoplates sans plier les coudes ni monter les épaules.',
 'Arms straight in front, squeeze the shoulder blades without bending the elbows or shrugging.',
 'Épaules qui montent, coudes qui se plient.', 'Shoulders shrugging, elbows bending.', false, null),

-- ---------------------------------------------------------------------
-- Contrôle lombo-pelvien
-- ---------------------------------------------------------------------
('mcgill-curl-up', 'Curl-up de McGill', 'McGill curl-up', 'poids de corps',
 array['abdominaux'], array[]::text[], false, 2.5, array[]::text[], 'aucun',
 'Une jambe fléchie, mains sous le bas du dos. Décollez à peine la tête et les épaules, sans plaquer les lombaires.',
 'One knee bent, hands under the lower back. Lift the head and shoulders only slightly, without flattening the lumbar curve.',
 'Amplitude excessive, mains qui ne soutiennent plus la courbure.', 'Excessive range, hands no longer supporting the curve.', false, null),

('pont-fessier', 'Pont fessier', 'Glute bridge', 'poids de corps',
 array['fessiers'], array['ischio-jambiers','abdominaux'], false, 2.5, array[]::text[], 'aucun',
 'Sur le dos, pieds au sol. Montez les hanches jusqu''à l''alignement genoux-hanches-épaules.',
 'On the back, feet planted. Lift the hips until knees, hips and shoulders align.',
 'Hyperextension lombaire, poussée sur la nuque.', 'Lower-back overextension, pushing through the neck.', false, null),

('pont-fessier-une-jambe', 'Pont fessier une jambe', 'Single-leg glute bridge', 'poids de corps',
 array['fessiers'], array['ischio-jambiers'], true, 2.5, array[]::text[], 'aucun',
 'Une jambe tendue, montez la hanche sans laisser le bassin tourner.',
 'One leg extended, lift the hip without letting the pelvis rotate.',
 'Bascule du bassin, cambrure.', 'Pelvis tilting, arching.', false, null),

('cat-cow', 'Chat-vache', 'Cat-cow', 'poids de corps',
 array['lombaires'], array['abdominaux'], false, 2.5, array[]::text[], 'aucun',
 'À quatre pattes, alternez enroulement et extension de la colonne, lentement, sans forcer les fins d''amplitude.',
 'On all fours, alternate spinal flexion and extension slowly, without forcing the end ranges.',
 'Rythme trop rapide, amplitude forcée.', 'Moving too fast, forcing the range.', false, null),

('rotation-thoracique', 'Rotation thoracique', 'Thoracic rotation', 'poids de corps',
 array['lombaires'], array['deltoides'], true, 2.5, array[]::text[], 'aucun',
 'À quatre pattes, main derrière la tête. Ouvrez le coude vers le plafond en pivotant le haut du dos.',
 'On all fours, hand behind the head. Open the elbow to the ceiling by rotating the upper back.',
 'Rotation lombaire au lieu de thoracique, bassin qui suit.', 'Rotating from the lower back instead of the thoracic spine, hips following.', false, null),

('gainage-lateral-genoux', 'Gainage latéral genoux fléchis', 'Modified side plank', 'poids de corps',
 array['abdominaux'], array['fessiers'], true, 2.5, array[]::text[], 'aucun',
 'Appui sur le coude et les genoux, hanches hautes. Version allégée du gainage latéral.',
 'Supported on the elbow and knees, hips high. A lighter version of the side plank.',
 'Hanches basses, épaule qui s''affaisse.', 'Hips dropping, shoulder collapsing.', false, null),

-- ---------------------------------------------------------------------
-- Hanche et bassin
-- ---------------------------------------------------------------------
('clamshell', 'Clamshell', 'Clamshell', 'élastique',
 array['fessiers'], array[]::text[], true, 1, array[]::text[], 'aucun',
 'Sur le côté, genoux fléchis. Ouvrez le genou du dessus sans que le bassin bascule en arrière.',
 'On your side, knees bent. Open the top knee without letting the pelvis roll back.',
 'Bascule du bassin, amplitude gagnée par la rotation du tronc.', 'Pelvis rolling, range gained by trunk rotation.', false, null),

('fire-hydrant', 'Fire hydrant', 'Fire hydrant', 'poids de corps',
 array['fessiers'], array[]::text[], true, 2.5, array[]::text[], 'aucun',
 'À quatre pattes, ouvrez la hanche sur le côté en gardant le genou fléchi. Bassin stable.',
 'On all fours, open the hip out to the side with the knee bent. Keep the hips level.',
 'Bascule du bassin, cambrure lombaire.', 'Hips tilting, lower-back arching.', false, null),

('hip-airplane', 'Hip airplane', 'Hip airplane', 'poids de corps',
 array['fessiers'], array['ischio-jambiers'], true, 2.5, array[]::text[], 'aucun',
 'En appui sur une jambe, buste penché. Ouvrez puis refermez la hanche lentement, sans perdre l''équilibre.',
 'Balanced on one leg, torso hinged. Slowly open then close the hip without losing balance.',
 'Perte d''équilibre, amplitude prise sur le bas du dos.', 'Losing balance, range taken from the lower back.', false, null),

('marche-elastique-avant', 'Marche élastique avant', 'Banded forward walk', 'élastique',
 array['fessiers'], array['quadriceps'], false, 1, array[]::text[], 'aucun',
 'Élastique aux chevilles, avancez à petits pas en gardant les genoux écartés.',
 'Band at the ankles, take small steps forward keeping the knees apart.',
 'Genoux qui rentrent, pas trop grands.', 'Knees caving in, steps too long.', false, null),

-- ---------------------------------------------------------------------
-- Genou et cheville
-- ---------------------------------------------------------------------
('terminal-knee-extension', 'Extension terminale du genou', 'Terminal knee extension', 'élastique',
 array['quadriceps'], array[]::text[], true, 1, array[]::text[], 'aucun',
 'Élastique derrière le genou, tendez la jambe sur les derniers degrés en gardant le pied au sol.',
 'Band behind the knee, straighten the leg through the last degrees with the foot planted.',
 'Verrouillage brutal, talon qui décolle.', 'Snapping into lockout, heel lifting.', false, null),

('wall-sit', 'Chaise au mur', 'Wall sit', 'poids de corps',
 array['quadriceps'], array['fessiers'], false, 2.5, array[]::text[], 'aucun',
 'Dos au mur, cuisses parallèles au sol. Tenez la position sans bloquer la respiration.',
 'Back against the wall, thighs parallel to the floor. Hold without holding your breath.',
 'Genoux au-delà des orteils, apnée.', 'Knees past the toes, holding the breath.', false, null),

('quad-setting', 'Contraction du quadriceps', 'Quad setting', 'poids de corps',
 array['quadriceps'], array[]::text[], true, 2.5, array[]::text[], 'aucun',
 'Jambe tendue au sol, contractez le quadriceps en écrasant le creux du genou. Isométrie douce.',
 'Leg straight on the floor, contract the quadriceps by pressing the back of the knee down. Gentle isometric.',
 'Contraction du fessier à la place, apnée.', 'Contracting the glute instead, holding the breath.', false, null),

('nordic-curl', 'Nordic curl', 'Nordic hamstring curl', 'poids de corps',
 array['ischio-jambiers'], array['fessiers'], false, 2.5, array[]::text[], 'aucun',
 'Chevilles bloquées, descendez le buste vers l''avant en retenant. Rattrapez avec les mains.',
 'Ankles secured, lower the torso forward under control. Catch yourself with the hands.',
 'Chute libre sans retenue, hanches qui se plient.', 'Free-falling without control, hips bending.', false, null),

('dorsiflexion-cheville', 'Dorsiflexion de cheville', 'Ankle dorsiflexion', 'élastique',
 array['mollets'], array[]::text[], true, 1, array[]::text[], 'aucun',
 'Élastique autour de l''avant-pied, ramenez les orteils vers le tibia. Amplitude complète, lentement.',
 'Band around the forefoot, pull the toes toward the shin. Full range, slowly.',
 'Mouvement du genou pour aider, à-coups.', 'Moving the knee to help, jerking.', false, null),

('eversion-cheville', 'Éversion de cheville', 'Ankle eversion', 'élastique',
 array['mollets'], array[]::text[], true, 1, array[]::text[], 'aucun',
 'Élastique sur le bord externe du pied, poussez vers l''extérieur sans tourner la jambe.',
 'Band on the outer edge of the foot, push outward without rotating the leg.',
 'Rotation de la jambe entière, résistance trop forte.', 'Rotating the whole leg, resistance too strong.', false, null),

('equilibre-unipodal', 'Équilibre sur une jambe', 'Single-leg balance', 'poids de corps',
 array['mollets'], array['fessiers','abdominaux'], true, 2.5, array[]::text[], 'aucun',
 'Debout sur un pied, regard fixe. Tenez sans poser l''autre pied ni attraper d''appui.',
 'Standing on one foot, eyes fixed. Hold without putting the other foot down or grabbing support.',
 'Appui répété, hanche qui s''affaisse.', 'Repeatedly touching down, hip dropping.', false, null),

-- ---------------------------------------------------------------------
-- Cervicales, poignets, coudes
-- ---------------------------------------------------------------------
('retraction-cervicale', 'Rétraction cervicale', 'Chin tuck', 'poids de corps',
 array['trapezes'], array[]::text[], false, 2.5, array[]::text[], 'aucun',
 'Reculez le menton à l''horizontale, comme pour faire un double menton. Sans forcer, sans bascule de la tête.',
 'Draw the chin straight back, as if making a double chin. Without forcing, without tilting the head.',
 'Bascule de la tête vers le bas, amplitude forcée.', 'Tilting the head down, forcing the range.', false, null),

('excentrique-poignet', 'Excentrique du poignet', 'Eccentric wrist extension', 'haltère',
 array['avant-bras'], array[]::text[], true, 1, array[]::text[], 'poussee',
 'Avant-bras posé, paume vers le bas. Montez avec l''aide de l''autre main, descendez lentement seul.',
 'Forearm supported, palm down. Lift with the other hand, lower slowly on your own.',
 'Descente trop rapide, charge excessive.', 'Lowering too fast, load too heavy.', false, null),

('pronation-supination', 'Pronation-supination', 'Forearm pronation-supination', 'haltère',
 array['avant-bras'], array[]::text[], true, 1, array[]::text[], 'aucun',
 'Avant-bras posé, tenez l''haltère par une extrémité. Tournez la paume vers le haut puis vers le bas.',
 'Forearm supported, hold the dumbbell by one end. Turn the palm up then down.',
 'Mouvement de l''épaule, charge trop lourde.', 'Moving from the shoulder, load too heavy.', false, null),

-- ---------------------------------------------------------------------
-- Machines guidées
-- ---------------------------------------------------------------------
('squat-smith', 'Squat à la barre guidée', 'Smith machine squat', 'machine',
 array['quadriceps','fessiers'], array['ischio-jambiers'], false, 5, array['cervicale','lombaire'], 'aucun',
 'Pieds légèrement avancés, la barre guidée supprime le besoin d''équilibre latéral.',
 'Feet slightly forward; the guided bar removes the need for lateral balance.',
 'Pieds mal placés qui bloquent le genou, descente au-delà du contrôle.', 'Feet misplaced binding the knee, descending beyond control.', false, null),

('developpe-smith', 'Développé à la barre guidée', 'Smith machine bench press', 'machine',
 array['pectoraux'], array['triceps','deltoides'], false, 2.5, array['epaule'], 'poussee',
 'Banc centré sous la barre, descendez au bas des pectoraux.',
 'Bench centred under the bar, lower to the lower chest.',
 'Banc décalé, trajectoire imposée qui force l''épaule.', 'Bench off-centre, the fixed path straining the shoulder.', false, null),

('rowing-smith', 'Rowing à la barre guidée', 'Smith machine row', 'machine',
 array['dos'], array['biceps','trapezes'], false, 2.5, array['lombaire'], 'tirage',
 'Buste incliné sous la barre, tirez vers le ventre. La trajectoire guidée soulage le maintien lombaire.',
 'Torso hinged under the bar, pull to the abdomen. The guided path eases the lumbar demand.',
 'Buste qui se redresse, barre trop haute ou trop basse.', 'Torso rising, bar set too high or too low.', false, null),

('belt-squat', 'Squat à la ceinture', 'Belt squat', 'machine',
 array['quadriceps','fessiers'], array[]::text[], false, 5, array[]::text[], 'aucun',
 'Charge suspendue à la ceinture, aucune compression du rachis. Descendez au parallèle.',
 'Load hanging from the belt, no spinal loading. Lower to parallel.',
 'Talons qui décollent, amplitude écourtée.', 'Heels lifting, cutting the range short.', false, null),

('pendulum-squat', 'Squat pendulaire', 'Pendulum squat', 'machine',
 array['quadriceps'], array['fessiers'], false, 5, array['genou'], 'aucun',
 'Dos appuyé, trajectoire en arc. Descendez sans que le bassin décolle du dossier.',
 'Back supported, arcing path. Lower without the hips leaving the pad.',
 'Bassin qui décolle, amplitude excessive.', 'Hips lifting off the pad, excessive range.', false, null),

('tirage-horizontal-machine', 'Tirage horizontal machine', 'Machine row', 'machine',
 array['dos'], array['biceps'], false, 2.5, array[]::text[], 'tirage',
 'Poitrine contre le dossier, tirez les poignées vers les côtes.',
 'Chest against the pad, pull the handles to the ribs.',
 'Décollement de la poitrine, épaules qui montent.', 'Chest lifting, shoulders shrugging.', false, null),

('presse-mollets-assis-machine', 'Mollets machine assise', 'Seated calf machine', 'machine',
 array['mollets'], array[]::text[], false, 2.5, array[]::text[], 'aucun',
 'Genoux sous les coussins, montez sur la pointe puis descendez en étirant.',
 'Knees under the pads, rise onto the toes then lower into the stretch.',
 'Rebond, amplitude partielle.', 'Bouncing, partial range.', false, null),

-- ---------------------------------------------------------------------
-- Poids de corps avancés
-- ---------------------------------------------------------------------
('pistol-squat', 'Squat pistolet', 'Pistol squat', 'poids de corps',
 array['quadriceps','fessiers'], array['abdominaux'], true, 2.5, array['genou'], 'aucun',
 'Sur une jambe, l''autre tendue devant. Descendez lentement. Utilisez un appui tant que le contrôle manque.',
 'On one leg, the other extended forward. Lower slowly. Use support until the control is there.',
 'Descente en chute, talon qui décolle.', 'Dropping down, heel lifting.', false, null),

('archer-push-up', 'Pompe archer', 'Archer push-up', 'poids de corps',
 array['pectoraux'], array['triceps'], true, 2.5, array['epaule'], 'poussee',
 'Mains larges, descendez vers une main en tendant l''autre bras.',
 'Wide hands, lower toward one hand while the other arm straightens.',
 'Bassin qui pivote, épaule tendue en fin d''amplitude.', 'Hips rotating, shoulder strained at the end range.', false, null),

('l-sit', 'L-sit', 'L-sit', 'poids de corps',
 array['abdominaux'], array['triceps'], false, 2.5, array[]::text[], 'aucun',
 'Appui sur les mains, jambes tendues à l''horizontale. Épaules basses.',
 'Supported on the hands, legs extended horizontally. Shoulders depressed.',
 'Épaules qui montent, dos rond.', 'Shoulders shrugging, rounded back.', false, null),

('handstand-hold', 'Équilibre au mur', 'Wall handstand hold', 'poids de corps',
 array['deltoides'], array['abdominaux','triceps'], false, 2.5, array['epaule','cervicale'], 'poussee',
 'Pieds au mur, corps aligné, épaules actives. Descendez dès que l''alignement se perd.',
 'Feet on the wall, body in line, shoulders active. Come down as soon as the line is lost.',
 'Cambrure lombaire, coudes fléchis.', 'Lower-back arching, elbows bending.', false, null),

('suitcase-carry', 'Port valise', 'Suitcase carry', 'haltère',
 array['abdominaux'], array['avant-bras','trapezes'], true, 2, array[]::text[], 'aucun',
 'Une charge d''un seul côté, marchez droit sans vous pencher du côté opposé.',
 'Load in one hand only, walk tall without leaning to the other side.',
 'Inclinaison latérale, épaule qui monte.', 'Leaning sideways, shoulder hiking.', false, null),

('front-rack-carry', 'Port en rack frontal', 'Front rack carry', 'kettlebell',
 array['abdominaux'], array['deltoides','trapezes'], false, 2, array[]::text[], 'aucun',
 'Charges en rack sur les avant-bras, buste droit, respiration ample malgré la compression.',
 'Bells racked on the forearms, torso tall, breathing full despite the compression.',
 'Buste qui se penche en arrière, apnée.', 'Leaning back, holding the breath.', false, null),

('zercher-squat', 'Squat Zercher', 'Zercher squat', 'barre',
 array['quadriceps','fessiers'], array['abdominaux','dos'], false, 2.5, array['lombaire'], 'aucun',
 'Barre dans le pli des coudes, buste vertical. Descendez entre les genoux.',
 'Bar in the crooks of the elbows, torso upright. Lower between the knees.',
 'Buste qui bascule, barre qui glisse.', 'Torso tipping, bar sliding.', false, null),

-- ---------------------------------------------------------------------
-- Élastiques
-- ---------------------------------------------------------------------
('developpe-elastique', 'Développé élastique', 'Band chest press', 'élastique',
 array['pectoraux'], array['triceps'], false, 1, array[]::text[], 'poussee',
 'Élastique passé dans le dos, poussez devant la poitrine. La tension monte en fin de course.',
 'Band around the back, press forward from the chest. Tension rises at the end of the range.',
 'Retour lâché, épaules qui montent.', 'Releasing the return, shoulders shrugging.', false, null),

('tirage-elastique-haut', 'Tirage vertical élastique', 'Band pulldown', 'élastique',
 array['dos'], array['biceps'], false, 1, array[]::text[], 'tirage',
 'Élastique ancré en haut, tirez les coudes vers les côtes.',
 'Band anchored above, drive the elbows toward the ribs.',
 'Tirage des bras seuls, buste qui recule.', 'Pulling with the arms only, torso leaning back.', false, null),

('squat-elastique', 'Squat élastique', 'Band squat', 'élastique',
 array['quadriceps','fessiers'], array[]::text[], false, 1, array[]::text[], 'aucun',
 'Élastique sous les pieds et sur les épaules. La résistance croît en montant.',
 'Band under the feet and over the shoulders. Resistance increases as you rise.',
 'Élastique qui glisse, genoux qui rentrent.', 'Band slipping, knees caving in.', false, null),

('souleve-elastique', 'Soulevé élastique', 'Band deadlift', 'élastique',
 array['ischio-jambiers','fessiers'], array['dos'], false, 1, array['lombaire'], 'tirage',
 'Élastique sous les pieds, charnière de hanche avec dos neutre.',
 'Band under the feet, hip hinge with a neutral spine.',
 'Dos rond, élastique mal centré.', 'Rounded back, band off-centre.', false, null),

('elevation-laterale-elastique', 'Élévation latérale élastique', 'Band lateral raise', 'élastique',
 array['deltoides'], array[]::text[], false, 1, array[]::text[], 'aucun',
 'Pied sur l''élastique, montez le bras à l''horizontale.',
 'Foot on the band, raise the arm to horizontal.',
 'Montée trop haute, buste qui s''incline.', 'Raising too high, torso leaning.', false, null),

('curl-elastique', 'Curl élastique', 'Band curl', 'élastique',
 array['biceps'], array[]::text[], false, 1, array[]::text[], 'tirage',
 'Pieds sur l''élastique, coudes fixes. Contrôlez le retour, où la tension reste forte.',
 'Feet on the band, elbows fixed. Control the return, where the tension stays high.',
 'Retour lâché, coudes qui avancent.', 'Releasing the return, elbows drifting forward.', false, null),

('extension-triceps-elastique', 'Extension triceps élastique', 'Band pushdown', 'élastique',
 array['triceps'], array[]::text[], false, 1, array[]::text[], 'poussee',
 'Élastique ancré en haut, coudes collés au corps. Tendez sans bouger les épaules.',
 'Band anchored above, elbows at the sides. Extend without moving the shoulders.',
 'Coudes qui s''écartent, buste qui pousse.', 'Elbows flaring, leaning in.', false, null),

-- ---------------------------------------------------------------------
-- Poulies
-- ---------------------------------------------------------------------
('cable-crossover-haut', 'Crossover poulie haute', 'High cable crossover', 'poulie',
 array['pectoraux'], array[]::text[], false, 2.5, array['epaule'], 'poussee',
 'Poulies hautes, descendez les mains devant les hanches en croisant légèrement.',
 'High pulleys, bring the hands down in front of the hips, crossing slightly.',
 'Coudes qui se plient, buste qui s''effondre.', 'Elbows bending, torso collapsing.', false, null),

('rowing-poulie-debout', 'Rowing poulie debout', 'Standing cable row', 'poulie',
 array['dos'], array['biceps','trapezes'], false, 2.5, array[]::text[], 'tirage',
 'Debout face à la poulie, tirez vers le ventre en gardant le buste immobile.',
 'Standing facing the pulley, pull to the abdomen keeping the torso still.',
 'Buste qui recule à chaque répétition.', 'Torso leaning back with every rep.', false, null),

('pull-through', 'Pull-through', 'Cable pull-through', 'poulie',
 array['fessiers','ischio-jambiers'], array['lombaires'], false, 2.5, array['lombaire'], 'tirage',
 'Dos à la poulie basse, corde entre les jambes. Charnière de hanche, puis fermez les hanches.',
 'Back to the low pulley, rope between the legs. Hinge at the hips, then drive them forward.',
 'Squat au lieu de charnière, hyperextension en fin.', 'Squatting instead of hinging, overextending at the end.', false, null),

('rotation-poulie-debout', 'Rotation debout à la poulie', 'Standing cable rotation', 'poulie',
 array['abdominaux'], array[]::text[], true, 2.5, array['lombaire'], 'aucun',
 'De profil, tirez en pivotant le tronc, bras tendus. Les hanches suivent, elles ne mènent pas.',
 'Side-on, rotate the trunk with the arms extended. The hips follow, they do not lead.',
 'Rotation lombaire forcée, bras qui font le travail.', 'Forcing lumbar rotation, arms doing the work.', false, null),

('shrug-poulie', 'Shrug à la poulie', 'Cable shrug', 'poulie',
 array['trapezes'], array[]::text[], false, 2.5, array['cervicale'], 'tirage',
 'Poulie basse, montez les épaules verticalement, tension constante.',
 'Low pulley, shrug straight up with constant tension.',
 'Rotation des épaules, tête qui avance.', 'Rolling the shoulders, head jutting forward.', false, null),

('abduction-elastique-debout', 'Abduction debout élastique', 'Standing band abduction', 'élastique',
 array['fessiers'], array[]::text[], true, 1, array[]::text[], 'aucun',
 'Élastique à la cheville, écartez la jambe sans pencher le buste.',
 'Band at the ankle, take the leg out without leaning the torso.',
 'Inclinaison du buste, amplitude excessive.', 'Torso leaning, excessive range.', false, null),

-- ---------------------------------------------------------------------
-- Haltères et barres — compléments
-- ---------------------------------------------------------------------
('rowing-halteres-deux-bras', 'Rowing haltères deux bras', 'Two-arm dumbbell row', 'haltères',
 array['dos'], array['biceps','trapezes'], false, 2, array['lombaire'], 'tirage',
 'Buste incliné, tirez les deux haltères vers les hanches.',
 'Torso hinged, pull both dumbbells to the hips.',
 'Buste qui se redresse, dos rond.', 'Torso rising, rounded back.', false, null),

('developpe-couche-prise-serree', 'Développé prise serrée', 'Close-grip bench press', 'barre',
 array['triceps'], array['pectoraux'], false, 2.5, array['epaule'], 'poussee',
 'Prise largeur d''épaules, coudes le long du corps. Descendez au bas des pectoraux.',
 'Shoulder-width grip, elbows tucked. Lower to the lower chest.',
 'Prise trop serrée qui tord les poignets, coudes en croix.', 'Grip too narrow twisting the wrists, elbows flaring.', false, null),

('souleve-de-terre-trap-bar', 'Soulevé trap bar', 'Trap bar deadlift', 'barre',
 array['quadriceps','fessiers'], array['dos','trapezes'], false, 5, array['lombaire'], 'tirage',
 'Debout dans la barre, poignées sur les côtés. Buste plus vertical qu''en conventionnel.',
 'Standing inside the bar, handles at the sides. Torso more upright than conventional.',
 'Hanches qui montent en premier, dos rond.', 'Hips rising first, rounded back.', false, null),

('epaule-halteres', 'Épaulé haltères', 'Dumbbell clean', 'haltères',
 array['deltoides','trapezes'], array['quadriceps','fessiers'], false, 2, array['lombaire','epaule'], 'tirage',
 'Charnière puis extension explosive des hanches. Les haltères montent par l''élan, pas par les bras.',
 'Hinge then explosive hip extension. The dumbbells rise from the drive, not the arms.',
 'Tirage des bras, réception poignets cassés.', 'Pulling with the arms, catching with bent wrists.', false, null),

('fente-laterale', 'Fente latérale', 'Lateral lunge', 'haltère',
 array['quadriceps','fessiers'], array['adducteurs'], true, 2, array['genou'], 'aucun',
 'Grand pas de côté, poussez la hanche vers l''arrière sur la jambe fléchie. L''autre reste tendue.',
 'Long step to the side, push the hip back over the bent leg. The other stays straight.',
 'Genou qui dépasse le pied, buste qui s''effondre.', 'Knee past the foot, torso collapsing.', false, null),

('curl-nordique-assiste', 'Leg curl glissé', 'Slider leg curl', 'poids de corps',
 array['ischio-jambiers'], array['fessiers'], false, 2.5, array[]::text[], 'aucun',
 'En pont fessier, faites glisser les talons vers l''avant puis ramenez-les sans poser le bassin.',
 'In a glute bridge, slide the heels away then draw them back without dropping the hips.',
 'Bassin qui touche le sol, crampes par charge trop longue.', 'Hips touching down, cramping from over-long sets.', false, null),

('gainage-ventral-instable', 'Planche sur ballon', 'Stability ball plank', 'poids de corps',
 array['abdominaux'], array['deltoides'], false, 2.5, array[]::text[], 'aucun',
 'Avant-bras sur le ballon, corps aligné. L''instabilité augmente la demande sans ajouter de charge.',
 'Forearms on the ball, body in line. The instability raises the demand without adding load.',
 'Bassin qui s''affaisse, ballon qui roule sans contrôle.', 'Hips sagging, ball rolling out of control.', false, null),

('extension-hanche-quadrupedie', 'Extension de hanche à quatre pattes', 'Quadruped hip extension', 'poids de corps',
 array['fessiers'], array['ischio-jambiers'], true, 2.5, array['lombaire'], 'aucun',
 'À quatre pattes, poussez le talon vers le plafond sans cambrer le bas du dos.',
 'On all fours, press the heel toward the ceiling without arching the lower back.',
 'Hyperextension lombaire, bassin qui tourne.', 'Lower-back overextension, hips rotating.', false, null),

('marche-talons', 'Marche sur les talons', 'Heel walk', 'poids de corps',
 array['mollets'], array[]::text[], false, 2.5, array[]::text[], 'aucun',
 'Orteils relevés, marchez sur les talons. Renforce les releveurs du pied.',
 'Toes lifted, walk on the heels. Strengthens the ankle dorsiflexors.',
 'Pas trop longs, pointe qui touche.', 'Steps too long, toes touching down.', false, null),

('marche-pointes', 'Marche sur les pointes', 'Toe walk', 'poids de corps',
 array['mollets'], array[]::text[], false, 2.5, array[]::text[], 'aucun',
 'Talons décollés, marchez sur la pointe des pieds en gardant le buste droit.',
 'Heels raised, walk on the toes keeping the torso upright.',
 'Talons qui redescendent, buste penché.', 'Heels dropping, torso leaning.', false, null),

-- ---------------------------------------------------------------------
-- Variantes de prise et d'angle — la cible de 250 de `14-contenu.md` § 1
-- ---------------------------------------------------------------------
('developpe-couche-prise-large', 'Développé prise large', 'Wide-grip bench press', 'barre',
 array['pectoraux'], array['deltoides'], false, 2.5, array['epaule'], 'poussee',
 'Prise large, amplitude réduite. Descendez au bas des pectoraux sans forcer l''ouverture.',
 'Wide grip, shorter range. Lower to the lower chest without forcing the stretch.',
 'Prise excessive qui met l''épaule en tension, rebond.', 'Grip too wide straining the shoulder, bouncing.', false, null),

('developpe-incline-machine', 'Développé incliné machine', 'Incline machine press', 'machine',
 array['pectoraux'], array['deltoides','triceps'], false, 2.5, array[]::text[], 'poussee',
 'Poignées au niveau du haut des pectoraux, poussez en gardant le dos appuyé.',
 'Handles at upper-chest height, press keeping the back supported.',
 'Décollement du dos, verrouillage sec.', 'Back leaving the pad, harsh lockout.', false, null),

('ecarte-unilateral-poulie', 'Écarté unilatéral poulie', 'Single-arm cable fly', 'poulie',
 array['pectoraux'], array[]::text[], true, 2.5, array['epaule'], 'poussee',
 'Un bras à la fois, amenez la main vers la ligne médiane. Buste stable.',
 'One arm at a time, bring the hand toward the midline. Keep the torso still.',
 'Rotation du buste, coude qui se plie.', 'Torso rotating, elbow bending.', false, null),

('tirage-vertical-unilateral', 'Tirage vertical unilatéral', 'Single-arm pulldown', 'poulie',
 array['dos'], array['biceps'], true, 2.5, array[]::text[], 'tirage',
 'Une main, laissez l''omoplate monter puis tirez le coude vers la hanche.',
 'One hand, let the shoulder blade rise then drive the elbow to the hip.',
 'Buste qui bascule, épaule qui reste haute.', 'Torso leaning, shoulder staying elevated.', false, null),

('rowing-machine-unilateral', 'Rowing machine unilatéral', 'Single-arm machine row', 'machine',
 array['dos'], array['biceps'], true, 2.5, array[]::text[], 'tirage',
 'Un bras à la fois, poitrine contre le dossier. Amplitude complète.',
 'One arm at a time, chest against the pad. Full range.',
 'Rotation du buste pour tricher.', 'Rotating the torso to cheat.', false, null),

('traction-neutre', 'Traction prise neutre', 'Neutral-grip pull-up', 'poids de corps',
 array['dos'], array['biceps'], false, 2.5, array[]::text[], 'tirage',
 'Paumes face à face, la prise la plus douce pour l''épaule et le coude.',
 'Palms facing each other — the kindest grip for the shoulder and elbow.',
 'Amplitude écourtée, élan.', 'Short range, kipping.', false, null),

('traction-assistee', 'Traction assistée', 'Assisted pull-up', 'machine',
 array['dos'], array['biceps'], false, 2.5, array[]::text[], 'tirage',
 'Genoux sur le coussin, l''assistance allège sans faire le mouvement.',
 'Knees on the pad; the assistance lightens without doing the work.',
 'Assistance trop forte, descente lâchée.', 'Too much assistance, dropping on the way down.', false, null),

('tirage-menton-halteres', 'Tirage menton haltères', 'Dumbbell upright row', 'haltères',
 array['trapezes'], array['deltoides'], false, 2, array['epaule','cervicale'], 'tirage',
 'Coudes menés vers le haut, sans dépasser la hauteur d''épaules.',
 'Lead with the elbows, without going above shoulder height.',
 'Montée au-dessus des épaules, poignets tordus.', 'Pulling above the shoulders, wrists twisted.', false, null),

('oiseau-poulie', 'Oiseau à la poulie', 'Cable rear delt fly', 'poulie',
 array['deltoides'], array['trapezes'], false, 1, array[]::text[], 'tirage',
 'Poulies croisées à hauteur d''épaules, ouvrez les bras en arrière.',
 'Cables crossed at shoulder height, open the arms back.',
 'Coudes qui se plient, buste qui recule.', 'Elbows bending, torso leaning back.', false, null),

('developpe-militaire-assis', 'Développé militaire assis', 'Seated overhead press', 'barre',
 array['deltoides'], array['triceps'], false, 2.5, array['epaule','cervicale'], 'poussee',
 'Assis dossier haut, ce qui supprime l''élan des jambes.',
 'Seated with back support, which removes leg drive.',
 'Cambrure lombaire, barre qui part en avant.', 'Lower-back arching, bar drifting forward.', false, null),

('curl-halteres-assis', 'Curl assis', 'Seated dumbbell curl', 'haltères',
 array['biceps'], array['avant-bras'], true, 2, array[]::text[], 'tirage',
 'Assis, ce qui supprime le balancement du buste.',
 'Seated, which removes torso swing.',
 'Coudes qui avancent, épaules qui montent.', 'Elbows drifting forward, shoulders rising.', false, null),

('curl-poulie-corde', 'Curl à la corde', 'Rope hammer curl', 'poulie',
 array['biceps','avant-bras'], array[]::text[], false, 2.5, array[]::text[], 'tirage',
 'Corde, paumes face à face. Montez sans bouger les coudes.',
 'Rope, palms facing. Curl without moving the elbows.',
 'Buste qui recule, coudes qui remontent.', 'Torso leaning back, elbows rising.', false, null),

('extension-triceps-machine', 'Extension triceps machine', 'Machine triceps extension', 'machine',
 array['triceps'], array[]::text[], false, 2.5, array[]::text[], 'poussee',
 'Coudes calés, tendez les avant-bras sans décoller les bras du support.',
 'Elbows braced, extend the forearms without lifting the arms off the pad.',
 'Décollement des coudes, amplitude partielle.', 'Elbows lifting, partial range.', false, null),

('presse-a-cuisses-pieds-serres', 'Presse pieds serrés', 'Narrow-stance leg press', 'machine',
 array['quadriceps'], array['fessiers'], false, 5, array['genou'], 'aucun',
 'Pieds rapprochés au centre, ce qui accentue le travail des quadriceps.',
 'Feet close together and centred, which emphasises the quadriceps.',
 'Genoux qui rentrent, bassin qui décolle.', 'Knees caving, hips lifting.', false, null),

('presse-a-cuisses-pieds-larges', 'Presse pieds larges', 'Wide-stance leg press', 'machine',
 array['fessiers','adducteurs'], array['quadriceps'], false, 5, array[]::text[], 'aucun',
 'Pieds larges, pointes légèrement ouvertes.',
 'Wide feet, toes turned slightly out.',
 'Genoux qui rentrent, amplitude au-delà du contrôle.', 'Knees caving, range beyond control.', false, null),

('squat-halteres', 'Squat haltères', 'Dumbbell squat', 'haltères',
 array['quadriceps','fessiers'], array['avant-bras'], false, 2, array[]::text[], 'aucun',
 'Haltères le long du corps, descendez buste droit.',
 'Dumbbells at the sides, descend with an upright torso.',
 'Buste penché, talons qui décollent.', 'Torso leaning, heels lifting.', false, null),

('fente-statique', 'Fente statique', 'Split squat', 'haltères',
 array['quadriceps','fessiers'], array['ischio-jambiers'], true, 2, array[]::text[], 'aucun',
 'Position de fente maintenue, montez et descendez sans déplacer les pieds.',
 'Hold a split stance, rise and lower without moving the feet.',
 'Appui excessif sur la jambe arrière, buste qui bascule.', 'Over-loading the rear leg, torso tipping.', false, null),

('leg-curl-elastique', 'Leg curl élastique', 'Band leg curl', 'élastique',
 array['ischio-jambiers'], array[]::text[], true, 1, array[]::text[], 'aucun',
 'Élastique à la cheville, ramenez le talon vers le fessier à plat ventre.',
 'Band at the ankle, curl the heel to the glute while lying face-down.',
 'Bassin qui décolle, à-coups.', 'Hips lifting, jerking.', false, null),

('extension-jambes-elastique', 'Extension jambes élastique', 'Band leg extension', 'élastique',
 array['quadriceps'], array[]::text[], true, 1, array['genou'], 'aucun',
 'Assis, élastique à la cheville. Tendez la jambe sans verrouiller sèchement.',
 'Seated, band at the ankle. Extend without snapping into lockout.',
 'Verrouillage brutal, buste qui recule.', 'Snapping into lockout, torso leaning back.', false, null),

('hip-thrust-elastique', 'Hip thrust élastique', 'Band hip thrust', 'élastique',
 array['fessiers'], array['ischio-jambiers'], false, 1, array[]::text[], 'aucun',
 'Élastique sur les hanches, poussez jusqu''à l''alignement.',
 'Band across the hips, drive up to alignment.',
 'Hyperextension lombaire, élastique qui glisse.', 'Lower-back overextension, band slipping.', false, null),

('gainage-ventral-lest', 'Planche lestée', 'Weighted plank', 'poids de corps',
 array['abdominaux'], array['lombaires'], false, 2.5, array[]::text[], 'aucun',
 'Disque posé dans le haut du dos, corps aligné. Charge modeste.',
 'Plate on the upper back, body in line. Modest load.',
 'Bassin qui s''affaisse, charge mal placée.', 'Hips sagging, load poorly placed.', false, null),

('crunch-oblique', 'Crunch oblique', 'Oblique crunch', 'poids de corps',
 array['abdominaux'], array[]::text[], true, 2.5, array['cervicale'], 'aucun',
 'Sur le dos, amenez le coude vers le genou opposé sans tirer sur la nuque.',
 'On the back, bring the elbow toward the opposite knee without pulling the neck.',
 'Traction sur la nuque, élan.', 'Pulling on the neck, using momentum.', false, null),

('inclinaison-laterale-haltere', 'Inclinaison latérale', 'Dumbbell side bend', 'haltère',
 array['abdominaux'], array[]::text[], true, 2, array['lombaire'], 'aucun',
 'Une charge d''un côté, penchez-vous latéralement puis redressez-vous. Amplitude modérée.',
 'Load on one side, lean sideways then return. Moderate range.',
 'Amplitude excessive, rotation du buste.', 'Excessive range, torso rotating.', false, null),

('planche-alternee', 'Planche bras alternés', 'Plank shoulder tap', 'poids de corps',
 array['abdominaux'], array['deltoides'], false, 2.5, array[]::text[], 'aucun',
 'En planche bras tendus, touchez l''épaule opposée sans que le bassin bouge.',
 'In a high plank, tap the opposite shoulder without the hips moving.',
 'Bascule du bassin, appuis trop serrés.', 'Hips rocking, hands too close together.', false, null),

('releve-bassin-suspendu', 'Relevé de bassin suspendu', 'Hanging knee raise', 'poids de corps',
 array['abdominaux'], array['avant-bras'], false, 2.5, array[]::text[], 'aucun',
 'Suspendu, montez les genoux en enroulant le bassin. Version accessible du relevé de jambes.',
 'Hanging, raise the knees by curling the pelvis. An accessible version of the leg raise.',
 'Balancement, flexion de hanche seule.', 'Swinging, hip flexion only.', false, null),

('mollets-smith', 'Mollets à la barre guidée', 'Smith machine calf raise', 'machine',
 array['mollets'], array[]::text[], false, 2.5, array[]::text[], 'aucun',
 'Pointes sur une cale, barre sur les trapèzes. Amplitude complète.',
 'Toes on a block, bar on the traps. Full range.',
 'Rebond en bas, genoux qui plient.', 'Bouncing at the bottom, knees bending.', false, null),

('rowing-inverse-eleve', 'Rowing inversé pieds surélevés', 'Feet-elevated inverted row', 'poids de corps',
 array['dos'], array['biceps','abdominaux'], false, 2.5, array[]::text[], 'tirage',
 'Pieds sur un banc, corps horizontal. Version plus exigeante du rowing inversé.',
 'Feet on a bench, body horizontal. A harder version of the inverted row.',
 'Bassin qui s''affaisse, amplitude partielle.', 'Hips sagging, partial range.', false, null),

('pompes-lestees', 'Pompes lestées', 'Weighted push-up', 'poids de corps',
 array['pectoraux'], array['triceps'], false, 2.5, array[]::text[], 'poussee',
 'Disque dans le haut du dos, exécution identique aux pompes.',
 'Plate on the upper back, same execution as a push-up.',
 'Bassin qui s''affaisse sous la charge.', 'Hips sagging under the load.', false, null),

('squat-saute', 'Squat sauté', 'Jump squat', 'poids de corps',
 array['quadriceps','fessiers'], array['mollets'], false, 2.5, array['genou','lombaire'], 'aucun',
 'Squat puis extension explosive. Réception souple, genoux dans l''axe.',
 'Squat then explode upward. Land softly, knees tracking.',
 'Réception raide, genoux qui rentrent.', 'Stiff landing, knees caving.', false, null),

('fente-sautee', 'Fente sautée', 'Jump lunge', 'poids de corps',
 array['quadriceps','fessiers'], array['mollets'], true, 2.5, array['genou'], 'aucun',
 'Changez de jambe en l''air, réception amortie.',
 'Switch legs in the air, land softly.',
 'Réception raide, buste qui bascule.', 'Stiff landing, torso tipping.', false, null),

('hip-hinge-baton', 'Charnière au bâton', 'Dowel hip hinge', 'poids de corps',
 array['ischio-jambiers'], array['lombaires'], false, 2.5, array[]::text[], 'aucun',
 'Bâton en contact avec la nuque, le haut du dos et le sacrum. Poussez les hanches en arrière sans perdre un contact.',
 'Dowel touching the head, upper back and sacrum. Push the hips back without losing any contact point.',
 'Perte de contact, flexion des genoux à la place.', 'Losing contact, bending the knees instead.', false, null),

('dead-bug-lest', 'Dead bug lesté', 'Weighted dead bug', 'haltères',
 array['abdominaux'], array[]::text[], false, 2, array[]::text[], 'aucun',
 'Haltères légers tenus bras tendus, lombaires plaquées tout du long.',
 'Light dumbbells held with straight arms, lower back pressed down throughout.',
 'Décollement lombaire, charge trop lourde.', 'Lower back lifting, load too heavy.', false, null),

('face-pull-elastique', 'Face pull élastique', 'Band face pull', 'élastique',
 array['trapezes'], array['deltoides'], false, 1, array[]::text[], 'tirage',
 'Élastique ancré à hauteur du visage, tirez vers le front en écartant les mains.',
 'Band anchored at face height, pull to the forehead while spreading the hands.',
 'Coudes qui tombent, épaules qui montent.', 'Elbows dropping, shoulders shrugging.', false, null),

('rotation-interne-elastique', 'Rotation interne élastique', 'Band internal rotation', 'élastique',
 array['deltoides'], array[]::text[], true, 1, array[]::text[], 'aucun',
 'Coude au corps, ramenez l''avant-bras vers le ventre contre l''élastique.',
 'Elbow at the side, bring the forearm toward the stomach against the band.',
 'Coude qui décolle, buste qui pivote.', 'Elbow drifting, torso rotating.', false, null),

('gainage-lateral-eleve', 'Gainage latéral pieds surélevés', 'Elevated side plank', 'poids de corps',
 array['abdominaux'], array['fessiers'], true, 2.5, array[]::text[], 'aucun',
 'Pieds sur un banc, coude au sol. Hanches hautes et alignées.',
 'Feet on a bench, elbow on the floor. Hips high and in line.',
 'Hanches qui descendent, rotation du buste.', 'Hips dropping, torso rotating.', false, null),

('extension-lombaire-machine', 'Extension lombaire machine', 'Machine back extension', 'machine',
 array['lombaires'], array['fessiers'], false, 2.5, array['lombaire'], 'aucun',
 'Dos appuyé, remontez jusqu''à l''alignement du tronc sans dépasser.',
 'Back supported, rise to trunk alignment and no further.',
 'Hyperextension, élan.', 'Overextending, using momentum.', false, null)

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
-- Les variantes du renforcement
-- =====================================================================
--
-- Elles relient un mouvement chargé à sa version ALLÉGÉE, ce qui est le
-- sens du § 6 quand un ressenti `pain` appelle un remplacement : proposer
-- plus léger et plus contrôlé, pas seulement « autre chose ».

insert into public.exercise_variants (exercise_id, variant_id)
select a.id, b.id
  from (values
    ('rotation-externe-poulie',   'rotation-externe-couche'),
    ('rotation-externe-couche',   'rotation-externe-90'),
    ('elevation-laterale',        'scaption'),
    ('traction-pronation',        'scapular-pull-up'),
    ('y-t-w',                     'wall-slide'),
    ('pull-apart-elastique',      'retraction-scapulaire'),
    ('gainage-ventral',           'serratus-punch'),
    ('crunch-sol',                'mcgill-curl-up'),
    ('hip-thrust',                'pont-fessier'),
    ('pont-fessier',              'pont-fessier-une-jambe'),
    ('bird-dog',                  'cat-cow'),
    ('rotation-thoracique',       'cat-cow'),
    ('gainage-lateral',           'gainage-lateral-genoux'),
    ('monster-walk',              'clamshell'),
    ('abducteurs-machine',        'fire-hydrant'),
    ('romanian-une-jambe',        'hip-airplane'),
    ('monster-walk',              'marche-elastique-avant'),
    ('extension-jambes',          'terminal-knee-extension'),
    ('squat-gobelet',             'wall-sit'),
    ('extension-jambes',          'quad-setting'),
    ('leg-curl-allonge',          'nordic-curl'),
    ('nordic-curl',               'curl-nordique-assiste'),
    ('equilibre-unipodal',        'dorsiflexion-cheville'),
    ('dorsiflexion-cheville',     'eversion-cheville'),
    ('shrug',                     'retraction-cervicale'),
    ('curl-poignet',              'excentrique-poignet'),
    ('extension-poignet',         'pronation-supination'),
    ('squat-barre-dos',           'squat-smith'),
    ('developpe-couche-barre',    'developpe-smith'),
    ('rowing-barre',              'rowing-smith'),
    ('squat-barre-dos',           'belt-squat'),
    ('hack-squat',                'pendulum-squat'),
    ('tirage-horizontal',         'tirage-horizontal-machine'),
    ('mollets-assis',             'presse-mollets-assis-machine'),
    ('fente-bulgare',             'pistol-squat'),
    ('pompes',                    'archer-push-up'),
    ('hollow-hold',               'l-sit'),
    ('developpe-militaire',       'handstand-hold'),
    ('farmer-walk',               'suitcase-carry'),
    ('farmer-walk',               'front-rack-carry'),
    ('squat-avant',               'zercher-squat'),
    ('developpe-couche-halteres', 'developpe-elastique'),
    ('tirage-vertical',           'tirage-elastique-haut'),
    ('squat-gobelet',             'squat-elastique'),
    ('souleve-de-terre-roumain',  'souleve-elastique'),
    ('elevation-laterale',        'elevation-laterale-elastique'),
    ('curl-halteres',             'curl-elastique'),
    ('extension-triceps-poulie',  'extension-triceps-elastique'),
    ('ecarte-poulie',             'cable-crossover-haut'),
    ('tirage-horizontal',         'rowing-poulie-debout'),
    ('hip-thrust',                'pull-through'),
    ('wood-chop',                 'rotation-poulie-debout'),
    ('shrug',                     'shrug-poulie'),
    ('abduction-hanche-poulie',   'abduction-elastique-debout'),
    ('rowing-haltere',            'rowing-halteres-deux-bras'),
    ('barre-au-front',            'developpe-couche-prise-serree'),
    ('souleve-de-terre',          'souleve-de-terre-trap-bar'),
    ('kettlebell-swing',          'epaule-halteres'),
    ('fente-avant',               'fente-laterale'),
    ('gainage-ventral',           'gainage-ventral-instable'),
    ('glute-kickback',            'extension-hanche-quadrupedie'),
    ('equilibre-unipodal',        'marche-talons'),
    ('marche-talons',             'marche-pointes'),
    ('developpe-couche-barre',    'developpe-couche-prise-large'),
    ('developpe-incline-barre',   'developpe-incline-machine'),
    ('ecarte-poulie',             'ecarte-unilateral-poulie'),
    ('tirage-vertical',           'tirage-vertical-unilateral'),
    ('rowing-machine-buste-soutenu','rowing-machine-unilateral'),
    ('traction-pronation',        'traction-neutre'),
    ('traction-pronation',        'traction-assistee'),
    ('high-pull',                 'tirage-menton-halteres'),
    ('oiseau',                    'oiseau-poulie'),
    ('developpe-militaire',       'developpe-militaire-assis'),
    ('curl-halteres',             'curl-halteres-assis'),
    ('curl-marteau',              'curl-poulie-corde'),
    ('extension-triceps-poulie',  'extension-triceps-machine'),
    ('presse-a-cuisses',          'presse-a-cuisses-pieds-serres'),
    ('presse-a-cuisses',          'presse-a-cuisses-pieds-larges'),
    ('squat-gobelet',             'squat-halteres'),
    ('fente-bulgare',             'fente-statique'),
    ('leg-curl-allonge',          'leg-curl-elastique'),
    ('extension-jambes',          'extension-jambes-elastique'),
    ('hip-thrust',                'hip-thrust-elastique'),
    ('gainage-ventral',           'gainage-ventral-lest'),
    ('crunch-sol',                'crunch-oblique'),
    ('gainage-lateral',           'inclinaison-laterale-haltere'),
    ('planche-dynamique',         'planche-alternee'),
    ('releve-de-jambes-suspendu', 'releve-bassin-suspendu'),
    ('mollets-debout',            'mollets-smith'),
    ('rowing-inverse',            'rowing-inverse-eleve'),
    ('pompes',                    'pompes-lestees'),
    ('squat-gobelet',             'squat-saute'),
    ('fente-avant',               'fente-sautee'),
    ('souleve-de-terre-roumain',  'hip-hinge-baton'),
    ('dead-bug',                  'dead-bug-lest'),
    ('face-pull',                 'face-pull-elastique'),
    ('rotation-interne-poulie',   'rotation-interne-elastique'),
    ('gainage-lateral',           'gainage-lateral-eleve'),
    ('extension-lombaire',        'extension-lombaire-machine')
  ) as lien(source, cible)
  join public.exercises a on a.slug = lien.source and a.is_custom = false
  join public.exercises b on b.slug = lien.cible  and b.is_custom = false
on conflict (exercise_id, variant_id) do nothing;
