-- =====================================================================
-- Catalogue d'exercices — 60 mouvements prioritaires
-- =====================================================================
--
-- CE FICHIER N'EST PAS RELU PAR UN PROFESSIONNEL DE SANTÉ.
--
-- `docs/14-contenu.md` § 2 l'exige — « relus par un kinésithérapeute pour la
-- partie contraintes » — et cette relecture n'a pas eu lieu. Les
-- contre-indications ci-dessous sont DÉRIVÉES du tableau de
-- `docs/05-entrainement.md` § 4, mécaniquement :
--
--   Cervicale | charge axiale, développés au-dessus de la tête, shrugs
--   Lombaire  | soulevé de terre lourd, squat barre, good morning chargé
--   Épaule    | développé militaire, dips lestés, écarté en amplitude maximale
--   Genou     | fentes profondes, extensions lourdes en fin d'amplitude
--
-- C'est une application du document, pas un avis médical. La distinction doit
-- rester visible : `db/SOURCES.md` la porte aussi, et le journal du lot la
-- redit.
--
-- ---------------------------------------------------------------------
-- LES INCRÉMENTS NE SONT PAS INVENTÉS. `docs/05-entrainement.md` § 3 :
-- « 5 kg à la presse, 2,5 kg aux poulies et machines, 2 kg aux haltères, 1 kg
-- sur les élévations et les mouvements de rotateurs ».
--
-- LE RÔLE DU MOUVEMENT sert au ratio tirage/poussée du § 4, qui oppose « dos et
-- trapèzes contre pectoraux et triceps ». Les jambes, les mollets et le gainage
-- valent donc `aucun` — ils ne tirent ni ne poussent au sens de ce ratio. Le
-- deltoïde latéral aussi, et le document l'exclut nommément.
--
-- IDEMPOTENT. Le fichier se rejoue après un `db:reset` ou une correction :
-- `on conflict (slug)` met à jour plutôt que d'échouer. C'est ce que le slug
-- existe pour permettre — D68.
-- =====================================================================

insert into public.exercises
  (slug, name_fr, name_en, equipment, primary_muscles, secondary_muscles,
   is_unilateral, default_increment, contraindicated_for, movement_role,
   instructions_fr, instructions_en, common_errors_fr, common_errors_en,
   is_custom, owner_id)
values

-- ---------------------------------------------------------------------
-- Presse — quadriceps et fessiers
-- ---------------------------------------------------------------------
('squat-barre-dos', 'Squat barre nuque', 'Back squat', 'barre',
 array['quadriceps','fessiers'], array['ischio-jambiers','lombaires'],
 false, 5, array['cervicale','lombaire'], 'aucun',
 'Barre sur les trapèzes, pieds écartés largeur d''épaules. Descendez en poussant les hanches vers l''arrière, genoux dans l''axe des pieds. Remontez en poussant le sol.',
 'Bar on the traps, feet shoulder-width. Descend by pushing the hips back, knees tracking over the toes. Drive through the floor to stand.',
 'Talons qui décollent, genoux qui rentrent vers l''intérieur, dos qui s''arrondit en bas.',
 'Heels lifting, knees caving inward, lower back rounding at the bottom.',
 false, null),

('squat-gobelet', 'Squat gobelet', 'Goblet squat', 'haltère',
 array['quadriceps','fessiers'], array['abdominaux'],
 false, 2, array[]::text[], 'aucun',
 'Haltère tenu contre la poitrine, coudes vers le bas. Descendez entre les genoux en gardant le buste vertical.',
 'Hold a dumbbell against the chest, elbows down. Sit between the knees keeping the torso upright.',
 'Buste qui bascule vers l''avant, charge qui s''éloigne du corps.',
 'Torso tipping forward, weight drifting away from the body.',
 false, null),

('presse-a-cuisses', 'Presse à cuisses', 'Leg press', 'machine',
 array['quadriceps','fessiers'], array['ischio-jambiers'],
 false, 5, array[]::text[], 'aucun',
 'Pieds à mi-hauteur de la plateforme, largeur d''épaules. Descendez jusqu''à ce que les cuisses approchent le buste, sans que le bassin décolle.',
 'Feet mid-platform, shoulder-width. Lower until the thighs approach the torso, without the hips lifting off the pad.',
 'Bassin qui décolle en bas, genoux verrouillés en haut.',
 'Hips lifting at the bottom, knees locked out at the top.',
 false, null),

('fente-avant', 'Fente avant', 'Forward lunge', 'haltères',
 array['quadriceps','fessiers'], array['ischio-jambiers'],
 true, 2, array['genou'], 'aucun',
 'Grand pas en avant, descendez jusqu''à ce que le genou arrière frôle le sol. Poussez sur le talon avant pour revenir.',
 'Take a long step forward, lower until the rear knee nearly touches the floor. Push through the front heel to return.',
 'Pas trop court qui avance le genou au-delà des orteils, buste qui s''effondre.',
 'Step too short pushing the knee past the toes, torso collapsing.',
 false, null),

('fente-bulgare', 'Fente bulgare', 'Bulgarian split squat', 'haltères',
 array['quadriceps','fessiers'], array['ischio-jambiers'],
 true, 2, array['genou'], 'aucun',
 'Pied arrière surélevé sur un banc. Descendez verticalement, le poids sur la jambe avant.',
 'Rear foot elevated on a bench. Descend vertically, weight on the front leg.',
 'Appui sur le pied arrière, buste qui part en avant.',
 'Pushing off the rear foot, torso pitching forward.',
 false, null),

('extension-jambes', 'Extension des jambes', 'Leg extension', 'machine',
 array['quadriceps'], array[]::text[],
 false, 2.5, array['genou'], 'aucun',
 'Dos plaqué au dossier. Tendez les jambes sans verrouiller brutalement, redescendez en retenant.',
 'Back against the pad. Extend without slamming into lockout, lower under control.',
 'Élan avec le buste, verrouillage sec en fin d''amplitude.',
 'Swinging the torso, snapping into full lockout.',
 false, null),

('step-up', 'Montée sur banc', 'Step-up', 'haltères',
 array['quadriceps','fessiers'], array['ischio-jambiers'],
 true, 2, array['genou'], 'aucun',
 'Posez tout le pied sur la marche, montez sans pousser sur la jambe restée au sol.',
 'Place the whole foot on the step, rise without pushing off the trailing leg.',
 'Impulsion de la jambe basse, marche trop haute.',
 'Driving off the bottom leg, step set too high.',
 false, null),

('squat-avant', 'Squat avant', 'Front squat', 'barre',
 array['quadriceps'], array['fessiers','abdominaux'],
 false, 5, array['cervicale','lombaire'], 'aucun',
 'Barre en rack frontal sur les deltoïdes, coudes hauts. Descendez buste vertical.',
 'Bar racked on the front delts, elbows high. Descend with an upright torso.',
 'Coudes qui tombent, barre qui roule vers l''avant.',
 'Elbows dropping, bar rolling forward.',
 false, null),

-- ---------------------------------------------------------------------
-- Charnière de hanche — ischio-jambiers et fessiers
-- ---------------------------------------------------------------------
('souleve-de-terre', 'Soulevé de terre', 'Deadlift', 'barre',
 array['ischio-jambiers','fessiers'], array['lombaires','trapezes','dos'],
 false, 5, array['lombaire'], 'tirage',
 'Barre au-dessus du milieu du pied. Dos neutre, poussez le sol avec les jambes et fermez les hanches en haut.',
 'Bar over mid-foot. Neutral spine, push the floor away with the legs and lock the hips at the top.',
 'Dos qui s''arrondit au décollage, barre qui s''éloigne des tibias.',
 'Lower back rounding off the floor, bar drifting away from the shins.',
 false, null),

('souleve-de-terre-roumain', 'Soulevé de terre roumain', 'Romanian deadlift', 'barre',
 array['ischio-jambiers'], array['fessiers','lombaires'],
 false, 5, array['lombaire'], 'tirage',
 'Jambes presque tendues, poussez les hanches vers l''arrière en gardant la barre contre les cuisses. Descendez tant que le dos reste neutre.',
 'Legs nearly straight, push the hips back keeping the bar against the thighs. Descend only as far as the spine stays neutral.',
 'Descente en pliant les genoux, dos qui s''arrondit en bas.',
 'Bending the knees to go lower, spine rounding at the bottom.',
 false, null),

('hip-thrust', 'Hip thrust', 'Hip thrust', 'barre',
 array['fessiers'], array['ischio-jambiers'],
 false, 5, array[]::text[], 'aucun',
 'Haut du dos calé sur un banc, menton rentré. Poussez les hanches vers le plafond jusqu''à l''alignement genoux-hanches-épaules.',
 'Upper back on a bench, chin tucked. Drive the hips up until knees, hips and shoulders align.',
 'Hyperextension lombaire en haut, menton qui part en arrière.',
 'Overarching the lower back at the top, chin lifting away.',
 false, null),

('leg-curl-allonge', 'Leg curl allongé', 'Lying leg curl', 'machine',
 array['ischio-jambiers'], array['mollets'],
 false, 2.5, array[]::text[], 'aucun',
 'Bassin plaqué, ramenez les talons vers les fessiers sans décoller les hanches.',
 'Hips flat on the pad, curl the heels toward the glutes without lifting the hips.',
 'Bassin qui décolle, à-coups en fin de mouvement.',
 'Hips lifting off the pad, jerking at the end of the range.',
 false, null),

('good-morning', 'Good morning', 'Good morning', 'barre',
 array['ischio-jambiers','lombaires'], array['fessiers'],
 false, 5, array['lombaire','cervicale'], 'aucun',
 'Barre sur les trapèzes, charge légère. Poussez les hanches vers l''arrière, buste vers l''avant, dos neutre.',
 'Bar on the traps, light load. Push the hips back, hinge the torso forward, spine neutral.',
 'Charge trop lourde, dos qui s''arrondit.',
 'Load too heavy, spine rounding.',
 false, null),

('souleve-de-terre-jambes-tendues-halteres', 'Soulevé de terre haltères', 'Dumbbell RDL', 'haltères',
 array['ischio-jambiers'], array['fessiers'],
 false, 2, array['lombaire'], 'tirage',
 'Haltères devant les cuisses, hanches en arrière, dos neutre. Descendez le long des jambes.',
 'Dumbbells in front of the thighs, hips back, spine neutral. Lower along the legs.',
 'Haltères qui s''éloignent du corps, dos rond.',
 'Dumbbells drifting forward, rounded back.',
 false, null),

('extension-lombaire', 'Extension lombaire', 'Back extension', 'poids de corps',
 array['lombaires'], array['fessiers','ischio-jambiers'],
 false, 2.5, array['lombaire'], 'aucun',
 'Remontez jusqu''à l''alignement du tronc, sans dépasser. Le mouvement vient des hanches.',
 'Rise until the trunk is in line, no further. The movement comes from the hips.',
 'Hyperextension en haut, élan.',
 'Overextending at the top, using momentum.',
 false, null),

-- ---------------------------------------------------------------------
-- Développés — pectoraux, triceps, deltoïde antérieur
-- ---------------------------------------------------------------------
('developpe-couche-barre', 'Développé couché', 'Bench press', 'barre',
 array['pectoraux'], array['triceps','deltoides'],
 false, 2.5, array['epaule'], 'poussee',
 'Omoplates serrées et basses, pieds au sol. Descendez la barre au bas des pectoraux, coudes à environ 45°.',
 'Shoulder blades retracted and depressed, feet planted. Lower the bar to the lower chest, elbows around 45°.',
 'Rebond sur le sternum, coudes écartés à 90°, fessiers qui décollent.',
 'Bouncing off the chest, elbows flared to 90°, glutes lifting.',
 false, null),

('developpe-couche-halteres', 'Développé couché haltères', 'Dumbbell bench press', 'haltères',
 array['pectoraux'], array['triceps','deltoides'],
 false, 2, array['epaule'], 'poussee',
 'Haltères au niveau des pectoraux, poignets alignés sur les avant-bras. Poussez sans entrechoquer en haut.',
 'Dumbbells at chest level, wrists stacked over the forearms. Press without clashing at the top.',
 'Amplitude excessive qui étire l''épaule, poignets cassés.',
 'Excessive range straining the shoulder, wrists bent back.',
 false, null),

('developpe-incline-barre', 'Développé incliné', 'Incline bench press', 'barre',
 array['pectoraux'], array['deltoides','triceps'],
 false, 2.5, array['epaule'], 'poussee',
 'Banc à 30°, barre descendue au haut des pectoraux. Coudes légèrement rentrés.',
 'Bench at 30°, bar lowered to the upper chest. Elbows slightly tucked.',
 'Inclinaison trop forte qui transforme le mouvement en développé épaules.',
 'Bench too steep, turning it into a shoulder press.',
 false, null),

('developpe-incline-halteres', 'Développé incliné haltères', 'Incline dumbbell press', 'haltères',
 array['pectoraux'], array['deltoides','triceps'],
 false, 2, array['epaule'], 'poussee',
 'Banc à 30°, haltères descendus au niveau du haut des pectoraux.',
 'Bench at 30°, dumbbells lowered to upper-chest level.',
 'Descente trop basse, coudes qui partent loin derrière.',
 'Lowering too far, elbows travelling behind the body.',
 false, null),

('dips-pectoraux', 'Dips pectoraux', 'Chest dip', 'poids de corps',
 array['pectoraux'], array['triceps','deltoides'],
 false, 2.5, array['epaule'], 'poussee',
 'Buste légèrement penché en avant, descendez jusqu''à ce que les bras soient parallèles au sol.',
 'Torso leaning slightly forward, lower until the upper arms are parallel to the floor.',
 'Descente au-delà du parallèle, épaules qui montent aux oreilles.',
 'Dropping below parallel, shoulders shrugging up.',
 false, null),

('pompes', 'Pompes', 'Push-up', 'poids de corps',
 array['pectoraux'], array['triceps','abdominaux'],
 false, 2.5, array[]::text[], 'poussee',
 'Corps aligné des chevilles aux épaules, mains sous les épaules. Descendez la poitrine vers le sol.',
 'Body in a straight line from ankles to shoulders, hands under the shoulders. Lower the chest to the floor.',
 'Bassin qui s''affaisse, coudes en croix.',
 'Hips sagging, elbows flaring straight out.',
 false, null),

('ecarte-couche', 'Écarté couché', 'Dumbbell fly', 'haltères',
 array['pectoraux'], array[]::text[],
 false, 2, array['epaule'], 'poussee',
 'Coudes légèrement fléchis et fixes, ouvrez jusqu''au niveau des pectoraux. Ne descendez pas plus bas.',
 'Elbows slightly bent and fixed, open to chest level. Do not go lower.',
 'Amplitude maximale qui met l''épaule en tension extrême, coudes qui se plient.',
 'Maximal range placing extreme strain on the shoulder, elbows bending.',
 false, null),

('developpe-machine-pectoraux', 'Développé machine', 'Chest press machine', 'machine',
 array['pectoraux'], array['triceps','deltoides'],
 false, 2.5, array[]::text[], 'poussee',
 'Réglez le siège pour que les poignées soient au niveau des pectoraux. Poussez sans verrouiller sèchement.',
 'Set the seat so the handles sit at chest height. Press without harshly locking out.',
 'Siège trop bas qui transforme le mouvement en développé incliné.',
 'Seat too low, turning it into an incline press.',
 false, null),

('ecarte-poulie', 'Écarté à la poulie', 'Cable fly', 'poulie',
 array['pectoraux'], array[]::text[],
 false, 2.5, array['epaule'], 'poussee',
 'Buste légèrement penché, rapprochez les mains devant la poitrine en gardant les coudes fixes.',
 'Torso slightly forward, bring the hands together in front of the chest with fixed elbows.',
 'Amplitude arrière excessive, bras qui se plient pour tricher.',
 'Excessive stretch behind the body, bending the arms to cheat.',
 false, null),

-- ---------------------------------------------------------------------
-- Tirages verticaux — dorsaux
-- ---------------------------------------------------------------------
('traction-pronation', 'Traction pronation', 'Pull-up', 'poids de corps',
 array['dos'], array['biceps','trapezes'],
 false, 2.5, array[]::text[], 'tirage',
 'Prise légèrement plus large que les épaules. Tirez les coudes vers les côtes, poitrine vers la barre.',
 'Grip slightly wider than the shoulders. Pull the elbows toward the ribs, chest to the bar.',
 'Élan des jambes, amplitude partielle en haut.',
 'Kipping with the legs, cutting the range short at the top.',
 false, null),

('traction-supination', 'Traction supination', 'Chin-up', 'poids de corps',
 array['dos'], array['biceps'],
 false, 2.5, array[]::text[], 'tirage',
 'Prise en supination largeur d''épaules. Tirez jusqu''à ce que le menton dépasse la barre.',
 'Underhand grip, shoulder-width. Pull until the chin clears the bar.',
 'Épaules qui montent aux oreilles au départ.',
 'Shoulders shrugging up at the start.',
 false, null),

('tirage-vertical', 'Tirage vertical', 'Lat pulldown', 'poulie',
 array['dos'], array['biceps'],
 false, 2.5, array[]::text[], 'tirage',
 'Buste légèrement incliné en arrière, tirez la barre vers le haut de la poitrine.',
 'Torso leaning slightly back, pull the bar to the upper chest.',
 'Barre derrière la nuque, buste qui se balance.',
 'Pulling behind the neck, swinging the torso.',
 false, null),

('tirage-vertical-prise-neutre', 'Tirage vertical prise neutre', 'Neutral-grip pulldown', 'poulie',
 array['dos'], array['biceps'],
 false, 2.5, array[]::text[], 'tirage',
 'Paumes face à face. Tirez les coudes vers le bas et l''arrière.',
 'Palms facing each other. Drive the elbows down and back.',
 'Tirage avec les bras seuls, omoplates immobiles.',
 'Pulling with the arms only, shoulder blades not moving.',
 false, null),

('pull-over-poulie', 'Pull-over à la poulie', 'Cable pull-over', 'poulie',
 array['dos'], array['pectoraux'],
 false, 2.5, array['epaule'], 'tirage',
 'Bras presque tendus, ramenez la barre vers les cuisses en gardant les coudes fixes.',
 'Arms nearly straight, sweep the bar toward the thighs with fixed elbows.',
 'Coudes qui se plient, mouvement qui devient un tirage.',
 'Elbows bending, turning it into a pulldown.',
 false, null),

-- ---------------------------------------------------------------------
-- Tirages horizontaux — dos et trapèzes
-- ---------------------------------------------------------------------
('rowing-barre', 'Rowing barre', 'Barbell row', 'barre',
 array['dos'], array['biceps','trapezes','lombaires'],
 false, 2.5, array['lombaire'], 'tirage',
 'Buste incliné à environ 45°, dos neutre. Tirez la barre vers le nombril.',
 'Torso hinged to about 45°, spine neutral. Pull the bar to the navel.',
 'Buste qui se redresse à chaque répétition, dos arrondi.',
 'Torso rising with every rep, rounded back.',
 false, null),

('rowing-haltere', 'Rowing haltère', 'Dumbbell row', 'haltère',
 array['dos'], array['biceps','trapezes'],
 true, 2, array[]::text[], 'tirage',
 'Un genou et une main sur le banc, dos plat. Tirez l''haltère vers la hanche.',
 'One knee and one hand on the bench, flat back. Pull the dumbbell toward the hip.',
 'Rotation du buste pour aider, épaule qui monte.',
 'Twisting the torso to help, shoulder hiking up.',
 false, null),

('tirage-horizontal', 'Tirage horizontal', 'Seated cable row', 'poulie',
 array['dos'], array['biceps','trapezes'],
 false, 2.5, array[]::text[], 'tirage',
 'Buste vertical, tirez la poignée vers le nombril en resserrant les omoplates.',
 'Upright torso, pull the handle to the navel while squeezing the shoulder blades.',
 'Balancement du buste, épaules qui s''enroulent en avant au retour.',
 'Rocking the torso, shoulders rolling forward on the return.',
 false, null),

('rowing-inverse', 'Rowing inversé', 'Inverted row', 'poids de corps',
 array['dos'], array['biceps','trapezes'],
 false, 2.5, array[]::text[], 'tirage',
 'Corps gainé sous une barre fixe, tirez la poitrine vers la barre.',
 'Body rigid under a fixed bar, pull the chest to the bar.',
 'Bassin qui s''affaisse, amplitude partielle.',
 'Hips sagging, partial range.',
 false, null),

('rowing-machine-buste-soutenu', 'Rowing buste soutenu', 'Chest-supported row', 'machine',
 array['dos','trapezes'], array['biceps'],
 false, 2.5, array[]::text[], 'tirage',
 'Poitrine appuyée sur le dossier, tirez les coudes vers l''arrière.',
 'Chest against the pad, drive the elbows back.',
 'Décollement de la poitrine pour tricher.',
 'Lifting the chest off the pad to cheat.',
 false, null),

('face-pull', 'Face pull', 'Face pull', 'poulie',
 array['trapezes'], array['deltoides'],
 false, 1, array[]::text[], 'tirage',
 'Corde à hauteur du visage, tirez vers le front en écartant les mains, coudes hauts.',
 'Rope at face height, pull toward the forehead while spreading the hands, elbows high.',
 'Charge trop lourde qui fait descendre les coudes.',
 'Load too heavy, causing the elbows to drop.',
 false, null),

('y-t-w', 'Y-T-W', 'Y-T-W raise', 'haltères',
 array['trapezes'], array['deltoides'],
 false, 1, array[]::text[], 'tirage',
 'Buste penché ou sur banc incliné, dessinez les lettres Y, T puis W avec les bras.',
 'Bent over or on an incline bench, trace the letters Y, T then W with the arms.',
 'Charge trop lourde, épaules qui montent.',
 'Load too heavy, shoulders shrugging.',
 false, null),

('shrug', 'Shrug', 'Shrug', 'haltères',
 array['trapezes'], array[]::text[],
 false, 2, array['cervicale'], 'tirage',
 'Épaules montées verticalement, sans rotation. Marquez un temps en haut.',
 'Shrug the shoulders straight up, no rolling. Pause at the top.',
 'Rotation des épaules, tête qui avance.',
 'Rolling the shoulders, head jutting forward.',
 false, null),

-- ---------------------------------------------------------------------
-- Épaules
-- ---------------------------------------------------------------------
('developpe-militaire', 'Développé militaire', 'Overhead press', 'barre',
 array['deltoides'], array['triceps','trapezes'],
 false, 2.5, array['epaule','cervicale'], 'poussee',
 'Barre au niveau des clavicules, gainage serré. Poussez au-dessus de la tête en passant la tête sous la barre.',
 'Bar at collarbone height, core braced. Press overhead, moving the head through under the bar.',
 'Cambrure lombaire excessive, barre qui part en avant.',
 'Excessive lower-back arch, bar drifting forward.',
 false, null),

('developpe-epaules-halteres', 'Développé épaules haltères', 'Dumbbell shoulder press', 'haltères',
 array['deltoides'], array['triceps'],
 false, 2, array['epaule','cervicale'], 'poussee',
 'Assis dossier haut, haltères au niveau des oreilles. Poussez sans entrechoquer.',
 'Seated with back support, dumbbells at ear level. Press without clashing.',
 'Amplitude basse excessive, dos qui se cambre.',
 'Lowering too far, back arching.',
 false, null),

('elevation-laterale', 'Élévation latérale', 'Lateral raise', 'haltères',
 array['deltoides'], array[]::text[],
 false, 1, array[]::text[], 'aucun',
 'Coudes légèrement fléchis, montez jusqu''à l''horizontale, pas plus haut.',
 'Elbows slightly bent, raise to horizontal, no higher.',
 'Élan des jambes, montée au-dessus de l''épaule, trapèzes qui prennent le relais.',
 'Using leg drive, raising above shoulder height, traps taking over.',
 false, null),

('elevation-frontale', 'Élévation frontale', 'Front raise', 'haltères',
 array['deltoides'], array['pectoraux'],
 false, 1, array['epaule'], 'poussee',
 'Bras tendus devant, montez jusqu''à l''horizontale.',
 'Arms straight in front, raise to horizontal.',
 'Balancement du buste, montée trop haute.',
 'Swinging the torso, raising too high.',
 false, null),

('oiseau', 'Oiseau', 'Rear delt fly', 'haltères',
 array['deltoides'], array['trapezes'],
 false, 1, array[]::text[], 'tirage',
 'Buste penché, ouvrez les bras sur les côtés en gardant les coudes légèrement fléchis.',
 'Bent over, open the arms out to the sides with slightly bent elbows.',
 'Utilisation du dos pour lancer la charge.',
 'Using the back to swing the weight up.',
 false, null),

('rotation-externe-poulie', 'Rotation externe', 'Cable external rotation', 'poulie',
 array['deltoides'], array[]::text[],
 true, 1, array[]::text[], 'aucun',
 'Coude collé au corps à 90°, faites pivoter l''avant-bras vers l''extérieur. Charge légère.',
 'Elbow pinned to the side at 90°, rotate the forearm outward. Light load.',
 'Coude qui décolle, charge trop lourde qui fait tourner le buste.',
 'Elbow drifting away, load too heavy causing the torso to rotate.',
 false, null),

-- ---------------------------------------------------------------------
-- Bras
-- ---------------------------------------------------------------------
('curl-barre', 'Curl barre', 'Barbell curl', 'barre',
 array['biceps'], array['avant-bras'],
 false, 2.5, array[]::text[], 'tirage',
 'Coudes fixes le long du corps, montez la barre sans balancer le buste.',
 'Elbows fixed at the sides, curl the bar without swinging the torso.',
 'Balancement lombaire, coudes qui avancent.',
 'Swinging from the lower back, elbows drifting forward.',
 false, null),

('curl-halteres', 'Curl haltères', 'Dumbbell curl', 'haltères',
 array['biceps'], array['avant-bras'],
 true, 2, array[]::text[], 'tirage',
 'Coudes fixes, supinez en montant. Redescendez en retenant.',
 'Elbows fixed, supinate as you lift. Lower under control.',
 'Épaule qui monte pour aider, descente lâchée.',
 'Shoulder rising to help, dropping the weight on the way down.',
 false, null),

('curl-marteau', 'Curl marteau', 'Hammer curl', 'haltères',
 array['biceps','avant-bras'], array[]::text[],
 true, 2, array[]::text[], 'tirage',
 'Paumes face à face tout au long du mouvement, coudes fixes.',
 'Palms facing each other throughout, elbows fixed.',
 'Balancement, amplitude écourtée.',
 'Swinging, cutting the range short.',
 false, null),

('extension-triceps-poulie', 'Extension triceps poulie', 'Triceps pushdown', 'poulie',
 array['triceps'], array[]::text[],
 false, 2.5, array[]::text[], 'poussee',
 'Coudes collés au corps, tendez les bras sans bouger les épaules.',
 'Elbows pinned to the sides, extend the arms without moving the shoulders.',
 'Buste qui s''incline pour pousser, coudes qui s''écartent.',
 'Leaning in to push, elbows flaring out.',
 false, null),

('barre-au-front', 'Barre au front', 'Skull crusher', 'barre',
 array['triceps'], array[]::text[],
 false, 2.5, array['epaule'], 'poussee',
 'Allongé, coudes fixes vers le plafond. Descendez la barre vers le front.',
 'Lying down, elbows fixed and pointing up. Lower the bar toward the forehead.',
 'Coudes qui s''écartent, mouvement qui devient un développé.',
 'Elbows flaring, turning it into a press.',
 false, null),

('dips-triceps', 'Dips triceps', 'Triceps dip', 'poids de corps',
 array['triceps'], array['pectoraux','deltoides'],
 false, 2.5, array['epaule'], 'poussee',
 'Buste vertical, coudes vers l''arrière. Descendez jusqu''au parallèle.',
 'Upright torso, elbows tracking back. Lower to parallel.',
 'Descente trop profonde, épaules en rotation interne forcée.',
 'Dropping too deep, shoulders forced into internal rotation.',
 false, null),

('extension-triceps-nuque', 'Extension nuque', 'Overhead triceps extension', 'haltère',
 array['triceps'], array[]::text[],
 false, 2, array['epaule','cervicale'], 'poussee',
 'Haltère au-dessus de la tête, coudes fixes. Descendez derrière la nuque.',
 'Dumbbell overhead, elbows fixed. Lower behind the head.',
 'Coudes qui s''écartent, cambrure lombaire.',
 'Elbows flaring, lower-back arching.',
 false, null),

-- ---------------------------------------------------------------------
-- Mollets
-- ---------------------------------------------------------------------
('mollets-debout', 'Mollets debout', 'Standing calf raise', 'machine',
 array['mollets'], array[]::text[],
 false, 2.5, array[]::text[], 'aucun',
 'Montez sur la pointe des pieds au maximum, marquez un temps, redescendez en étirant.',
 'Rise onto the toes as high as possible, pause, lower into a stretch.',
 'Rebond en bas, amplitude écourtée.',
 'Bouncing at the bottom, cutting the range short.',
 false, null),

('mollets-assis', 'Mollets assis', 'Seated calf raise', 'machine',
 array['mollets'], array[]::text[],
 false, 2.5, array[]::text[], 'aucun',
 'Genoux fléchis à 90°, montez sur la pointe des pieds.',
 'Knees bent at 90°, rise onto the toes.',
 'Amplitude partielle, rythme trop rapide.',
 'Partial range, moving too fast.',
 false, null),

-- ---------------------------------------------------------------------
-- Gainage et abdominaux
-- ---------------------------------------------------------------------
('gainage-ventral', 'Gainage ventral', 'Front plank', 'poids de corps',
 array['abdominaux'], array['lombaires'],
 false, 2.5, array[]::text[], 'aucun',
 'Coudes sous les épaules, corps aligné. Serrez les fessiers et rentrez les côtes.',
 'Elbows under the shoulders, body in line. Squeeze the glutes and tuck the ribs.',
 'Bassin trop haut ou affaissé, apnée.',
 'Hips too high or sagging, holding the breath.',
 false, null),

('gainage-lateral', 'Gainage latéral', 'Side plank', 'poids de corps',
 array['abdominaux'], array['fessiers'],
 true, 2.5, array[]::text[], 'aucun',
 'Coude sous l''épaule, corps aligné de la tête aux pieds. Hanches hautes.',
 'Elbow under the shoulder, body in line from head to feet. Hips high.',
 'Hanches qui descendent, épaule qui s''affaisse.',
 'Hips dropping, shoulder collapsing.',
 false, null),

('pallof-press', 'Pallof press', 'Pallof press', 'poulie',
 array['abdominaux'], array[]::text[],
 true, 2.5, array[]::text[], 'aucun',
 'De profil à la poulie, tendez les bras devant le sternum en résistant à la rotation.',
 'Standing side-on to the cable, extend the arms in front of the sternum while resisting rotation.',
 'Rotation du buste, charge trop lourde.',
 'Torso rotating, load too heavy.',
 false, null),

('releve-de-jambes-suspendu', 'Relevé de jambes suspendu', 'Hanging leg raise', 'poids de corps',
 array['abdominaux'], array['avant-bras'],
 false, 2.5, array[]::text[], 'aucun',
 'Suspendu, montez les jambes en enroulant le bassin. Contrôlez la descente.',
 'Hanging, raise the legs by curling the pelvis. Control the descent.',
 'Balancement, mouvement des hanches seules sans enroulement.',
 'Swinging, moving from the hips only without curling.',
 false, null),

('crunch-poulie', 'Crunch à la poulie', 'Cable crunch', 'poulie',
 array['abdominaux'], array[]::text[],
 false, 2.5, array['cervicale'], 'aucun',
 'À genoux, enroulez le buste vers les cuisses. Le mouvement vient des abdominaux, pas des hanches.',
 'Kneeling, curl the torso toward the thighs. The movement comes from the abs, not the hips.',
 'Traction sur la nuque, flexion des hanches.',
 'Pulling on the neck, hinging at the hips.',
 false, null),

('dead-bug', 'Dead bug', 'Dead bug', 'poids de corps',
 array['abdominaux'], array[]::text[],
 false, 2.5, array[]::text[], 'aucun',
 'Sur le dos, lombaires plaquées au sol. Tendez bras et jambe opposés sans décoller le bas du dos.',
 'On the back, lower spine pressed to the floor. Extend the opposite arm and leg without letting the lower back lift.',
 'Lombaires qui décollent, amplitude trop grande.',
 'Lower back lifting, range too large.',
 false, null),

('roue-abdominale', 'Roue abdominale', 'Ab wheel rollout', 'poids de corps',
 array['abdominaux'], array['dos'],
 false, 2.5, array['lombaire'], 'aucun',
 'À genoux, déroulez vers l''avant en gardant les hanches fermées et le dos neutre.',
 'From the knees, roll forward keeping the hips closed and the spine neutral.',
 'Cambrure lombaire, amplitude au-delà du contrôle.',
 'Lower-back arching, rolling beyond control.',
 false, null),

-- ---------------------------------------------------------------------
-- Poulies et compléments
-- ---------------------------------------------------------------------
('tirage-bras-tendus', 'Tirage bras tendus', 'Straight-arm pulldown', 'poulie',
 array['dos'], array['triceps'],
 false, 2.5, array[]::text[], 'tirage',
 'Bras tendus, ramenez la barre vers les cuisses sans plier les coudes.',
 'Arms straight, sweep the bar to the thighs without bending the elbows.',
 'Coudes qui se plient, buste qui se balance.',
 'Elbows bending, torso swinging.',
 false, null),

('abduction-hanche-poulie', 'Abduction de hanche', 'Cable hip abduction', 'poulie',
 array['fessiers'], array[]::text[],
 true, 2.5, array[]::text[], 'aucun',
 'Debout, écartez la jambe sur le côté sans pencher le buste.',
 'Standing, take the leg out to the side without leaning the torso.',
 'Buste qui s''incline, amplitude excessive.',
 'Torso leaning, excessive range.',
 false, null),

('hip-thrust-machine', 'Hip thrust machine', 'Machine hip thrust', 'machine',
 array['fessiers'], array['ischio-jambiers'],
 false, 2.5, array[]::text[], 'aucun',
 'Dos calé, poussez les hanches jusqu''à l''extension complète sans cambrer.',
 'Back supported, drive the hips to full extension without arching.',
 'Hyperextension lombaire, amplitude partielle.',
 'Lower-back overextension, partial range.',
 false, null),

('leg-curl-assis', 'Leg curl assis', 'Seated leg curl', 'machine',
 array['ischio-jambiers'], array['mollets'],
 false, 2.5, array[]::text[], 'aucun',
 'Dos plaqué, ramenez les talons sous le siège en contrôlant le retour.',
 'Back against the pad, curl the heels under the seat and control the return.',
 'Bassin qui décolle, retour lâché.',
 'Hips lifting, dropping the weight on the return.',
 false, null),

('presse-mollets', 'Mollets à la presse', 'Calf press', 'machine',
 array['mollets'], array[]::text[],
 false, 5, array[]::text[], 'aucun',
 'Pointes de pieds sur le bord de la plateforme, poussez en tendant les chevilles.',
 'Toes on the edge of the platform, press by extending the ankles.',
 'Genoux qui se plient pour aider, amplitude courte.',
 'Bending the knees to help, short range.',
 false, null)

on conflict (slug) where is_custom = false do update set
  name_fr           = excluded.name_fr,
  name_en           = excluded.name_en,
  equipment         = excluded.equipment,
  primary_muscles   = excluded.primary_muscles,
  secondary_muscles = excluded.secondary_muscles,
  is_unilateral     = excluded.is_unilateral,
  default_increment = excluded.default_increment,
  contraindicated_for = excluded.contraindicated_for,
  movement_role     = excluded.movement_role,
  instructions_fr   = excluded.instructions_fr,
  instructions_en   = excluded.instructions_en,
  common_errors_fr  = excluded.common_errors_fr,
  common_errors_en  = excluded.common_errors_en;

-- =====================================================================
-- Les variantes — `docs/05-entrainement.md` § 6
-- =====================================================================
--
-- « Seuls les accessoires proposent des variantes. » Les mouvements socles —
-- presse, charnière de hanche, développés, tirages — ne tournent PAS : c'est
-- sur eux que se mesure la progression, et changer de mouvement détruit le
-- repère de surcharge.
--
-- Les liens ci-dessous relient donc des mouvements ÉQUIVALENTS, ceux qu'on
-- peut substituer quand un ressenti `pain` ou trois `meh` consécutifs
-- appellent un remplacement. Ils sont déclarés DANS LES DEUX SENS quand la
-- réciproque a du sens : la table ne force pas la symétrie, elle l'accepte.

insert into public.exercise_variants (exercise_id, variant_id)
select a.id, b.id
  from (values
    ('developpe-couche-barre',        'developpe-couche-halteres'),
    ('developpe-couche-halteres',     'developpe-couche-barre'),
    ('developpe-couche-barre',        'developpe-machine-pectoraux'),
    ('developpe-incline-barre',       'developpe-incline-halteres'),
    ('developpe-incline-halteres',    'developpe-incline-barre'),
    ('ecarte-couche',                 'ecarte-poulie'),
    ('ecarte-poulie',                 'ecarte-couche'),
    ('dips-pectoraux',                'pompes'),
    ('traction-pronation',            'tirage-vertical'),
    ('tirage-vertical',               'traction-pronation'),
    ('traction-supination',           'tirage-vertical-prise-neutre'),
    ('rowing-barre',                  'rowing-machine-buste-soutenu'),
    ('rowing-haltere',                'tirage-horizontal'),
    ('tirage-horizontal',             'rowing-haltere'),
    ('rowing-inverse',                'rowing-machine-buste-soutenu'),
    ('developpe-militaire',           'developpe-epaules-halteres'),
    ('developpe-epaules-halteres',    'developpe-militaire'),
    ('curl-barre',                    'curl-halteres'),
    ('curl-halteres',                 'curl-marteau'),
    ('barre-au-front',                'extension-triceps-poulie'),
    ('extension-triceps-poulie',      'barre-au-front'),
    ('dips-triceps',                  'extension-triceps-nuque'),
    ('fente-avant',                   'fente-bulgare'),
    ('fente-bulgare',                 'step-up'),
    ('leg-curl-allonge',              'leg-curl-assis'),
    ('leg-curl-assis',                'leg-curl-allonge'),
    ('mollets-debout',                'mollets-assis'),
    ('mollets-assis',                 'presse-mollets'),
    ('hip-thrust',                    'hip-thrust-machine'),
    ('hip-thrust-machine',            'hip-thrust'),
    ('souleve-de-terre-roumain',      'souleve-de-terre-jambes-tendues-halteres'),
    ('gainage-ventral',               'dead-bug'),
    ('roue-abdominale',               'gainage-ventral'),
    ('releve-de-jambes-suspendu',     'crunch-poulie'),
    ('pallof-press',                  'gainage-lateral')
  ) as lien(source, cible)
  join public.exercises a on a.slug = lien.source and a.is_custom = false
  join public.exercises b on b.slug = lien.cible  and b.is_custom = false
on conflict (exercise_id, variant_id) do nothing;
