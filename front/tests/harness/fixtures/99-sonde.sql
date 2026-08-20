-- Fixture de violation : un fichier de référentiel qui n'est listé nulle part
-- dans db/SOURCES.md. Elle vit ICI et jamais dans db/referentiel/ — une fixture
-- posée dans le vrai dossier ferait échouer `npm run regles` sur le dépôt réel,
-- et le garde-fou refuserait le projet lui-même.
select 1;
