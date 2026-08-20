-- Les trois rôles du produit — D37. AUCUN n'est superutilisateur.
--
-- Ce fichier est exécuté par le compte d'administration du conteneur, et c'est
-- le SEUL usage de ce compte dans tout le lot. Il tourne avant la première
-- migration : les rôles doivent exister avant que `dotnet ef database update`
-- s'y connecte.
--
-- POURQUOI DEUX RÔLES PLUTÔT QU'UN. « Superusers and roles with the BYPASSRLS
-- attribute always bypass the row security system when accessing a table.
-- Table owners normally bypass row security as well » (ddl-rowsecurity.html).
-- Sans un rôle applicatif distinct du propriétaire, les migrations créent les
-- tables, le rôle qui les crée en est propriétaire, et TOUTES les politiques
-- ne font rien — sans un message, sans un test rouge. C'est ce que
-- docs/03-donnees.md § RLS nomme « la première chose à éprouver, avant les
-- politiques elles-mêmes ».
--
-- LES MOTS DE PASSE ÉCRITS ICI SONT LOCAUX ET N'OUVRENT RIEN. Même forme que
-- db/compose.yaml : une chaîne sans entropie, qui ne donne accès qu'à un
-- conteneur vide sur la machine. Sur l'instance managée, ces trois lignes sont
-- suivies d'un `alter role … password …` lancé depuis un secret — jamais depuis
-- ce fichier, qui est versionné.
--
-- Ce fichier n'est PAS réentrant : relancé sur une base qui porte déjà les
-- rôles, `create role` échoue en le disant. C'est le comportement voulu pour un
-- script d'amorçage — il refuse au lieu de laisser croire qu'il a réappliqué
-- quelque chose. En local, la reprise se fait par `npm run db:reset`.

-- Propriétaire des tables. Utilisé par `dotnet ef database update` et par le
-- chargement du référentiel. JAMAIS par l'API.
-- NOBYPASSRLS est ce qui rend `force row level security` (D38) autre chose
-- qu'une ligne décorative : sous FORCE, même le propriétaire est soumis aux
-- politiques, et il ne doit avoir aucun moyen de s'y soustraire.
create role palier_migrations
  login password 'motdepasse_local_migrations'
  nosuperuser nobypassrls nocreatedb nocreaterole;

-- Le rôle de l'API. Ne possède aucun objet ; ses privilèges sont accordés
-- objet par objet, dans la migration qui crée l'objet.
create role palier_app
  login password 'motdepasse_local_application'
  nosuperuser nobypassrls nocreatedb nocreaterole;

-- `pg_dump` / `pg_restore`. SEUL rôle du produit à porter BYPASSRLS — sous
-- FORCE, `pg_dump` pose `row_security = off` et « If the user does not have
-- sufficient privileges to bypass row security, then an error is thrown »
-- (app-pgdump.html). Le prix exact se mesure à la tâche 10 ; l'attribut est
-- posé ici parce que c'est le seul endroit où un rôle se crée.
-- Sur l'instance managée, ce rôle n'est peut-être pas créable ainsi : « Only
-- superuser roles or roles with BYPASSRLS can specify BYPASSRLS »
-- (sql-createrole.html), et le compte d'administration d'Aiven n'est pas
-- superutilisateur. D37 en fait un arbitrage du porteur, pas une découverte.
create role palier_sauvegarde
  login password 'motdepasse_local_sauvegarde'
  nosuperuser bypassrls nocreatedb nocreaterole;

-- Les trois se connectent à la base du produit, et rien d'autre n'y a droit.
revoke all on database palier from public;
grant connect on database palier to palier_migrations, palier_app, palier_sauvegarde;

-- Depuis PostgreSQL 15, `public` appartient à `pg_database_owner` et CREATE y
-- est retiré à PUBLIC : sans cette ligne, la première migration échoue sur
-- « permission denied for schema public ».
grant create, usage on schema public to palier_migrations;
grant usage on schema public to palier_app, palier_sauvegarde;

-- Aucun `alter default privileges` : les privilèges de `palier_app` sont
-- accordés objet par objet, dans la migration qui crée l'objet (D37). Un
-- privilège par défaut accorderait d'avance sur des tables que personne n'a
-- encore écrites — c'est-à-dire sur celles que personne n'aura relues.
