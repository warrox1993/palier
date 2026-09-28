using System.Globalization;

namespace Palier.Api.Socle;

/// <summary>
/// Le refus de servir. Levé au démarrage, AVANT que Kestrel accepte la moindre
/// requête.
///
/// Le message NOMME toujours la cause — le rôle, la table possédée, ou la table
/// sans <c>FORCE</c>. Un « assertion de démarrage échouée » sans nom force à
/// rouvrir le code sur un serveur, un soir de déploiement.
/// </summary>
internal sealed class IsolationNonGarantieException : InvalidOperationException
{
    public IsolationNonGarantieException(string cause)
        : base(
            "L'API refuse de servir : l'isolation par ligne n'est pas garantie sur cette "
                + $"instance. {cause}\n\n"
                + "Ce refus est délibéré — D37. Sept épreuves vertes sur un conteneur ne "
                + "démontrent rien sur l'instance réelle : c'est le rôle de connexion et la "
                + "propriété des tables qui décident si RLS mord DU TOUT. Corriger la cause "
                + "ci-dessus, jamais ce contrôle."
        ) => Cause = cause;

    public IsolationNonGarantieException()
        : base("L'API refuse de servir : l'isolation par ligne n'est pas garantie.") =>
        Cause = "(inconnue)";

    public IsolationNonGarantieException(string message, Exception innerException)
        : base(message, innerException) => Cause = "(inconnue)";

    /// <summary>La cause seule, pour qu'une épreuve l'assertionne sans lire tout le message.</summary>
    public string Cause { get; }
}

/// <summary>
/// LES TROIS REQUÊTES DE D37, ET LE REFUS DE SERVIR — plus une quatrième qui
/// garde les trois autres.
///
/// Elle ne prend PAS <c>PalierDbContext</c> : elle prend
/// <see cref="LecteurDeSocle" />, qui est le seul type exempté du test
/// d'architecture. Une exemption de plus est une porte de plus.
/// </summary>
internal sealed class AssertionDIsolation(LecteurDeSocle lecteur)
{
    public async Task<DiagnosticDIsolation> VerifierAsync(CancellationToken jeton = default)
    {
        var diagnostic = await lecteur.DiagnostiquerAsync(jeton).ConfigureAwait(false);

        // (0) LA BORNE, ET ELLE VIENT EN PREMIER. Sur une base non migrée, les
        // trois listes suivantes sont vides et les trois contrôles passent au
        // vert sans avoir rien regardé — un faux vert exactement de la forme que
        // ce projet traque. Aucune table dans `public` n'est jamais un état
        // normal : l'historique des migrations en est déjà une.
        if (diagnostic.NombreDeTables == 0)
        {
            throw new IsolationNonGarantieException(
                "Le schéma `public` ne porte AUCUNE table : la base n'est pas migrée. Les "
                    + "trois contrôles d'isolation seraient vides — donc verts — sans avoir rien "
                    + "regardé. Appliquer les migrations avant de démarrer l'API "
                    + "(voir db/README.md § Appliquer)."
            );
        }

        // (1) Le rôle de l'API ne contourne pas RLS.
        if (diagnostic.IsSuperutilisateur || diagnostic.IsContournementRls)
        {
            var attributs = string.Join(
                " et ",
                new[]
                {
                    diagnostic.IsSuperutilisateur ? "SUPERUSER" : null,
                    diagnostic.IsContournementRls ? "BYPASSRLS" : null,
                }.Where(a => a is not null)
            );
            throw new IsolationNonGarantieException(
                $"Le rôle « {diagnostic.Role} » porte {attributs}. « Superusers and roles with "
                    + "the BYPASSRLS attribute always bypass the row security system » : toutes "
                    + "les politiques du schéma sont sans effet pour ce rôle, sans un message et "
                    + "sans un test rouge. L'API se connecte sous `palier_app` — vérifier "
                    + "ConnectionStrings__Palier."
            );
        }

        // (2) Le rôle de l'API ne possède aucune table.
        if (diagnostic.TablesPossedees.Count > 0)
        {
            throw new IsolationNonGarantieException(
                $"Le rôle « {diagnostic.Role} » est PROPRIÉTAIRE de "
                    + $"{diagnostic.TablesPossedees.Count.ToString(CultureInfo.InvariantCulture)} "
                    + $"table(s) de `public` : {string.Join(", ", diagnostic.TablesPossedees)}. "
                    + "« Table owners normally bypass row security as well » — `force row level "
                    + "security` est alors la SEULE barrière restante, et une seule ligne de "
                    + "migration suffirait à la retirer. L'API se connecte sous `palier_app`, "
                    + "jamais sous `palier_migrations`."
            );
        }

        // (3) Toute table a RLS activée ET forcée.
        if (diagnostic.TablesSansForce.Count > 0)
        {
            throw new IsolationNonGarantieException(
                $"{diagnostic.TablesSansForce.Count.ToString(CultureInfo.InvariantCulture)} "
                    + "table(s) de `public` n'ont pas RLS activée ET forcée : "
                    + $"{string.Join(", ", diagnostic.TablesSansForce)}. `enable` seul laisse le "
                    + "propriétaire contourner ses propres politiques ; sans `enable`, il n'y a "
                    + "aucune politique du tout."
            );
        }

        // (4) et (5) — LE QUATRIÈME RÔLE, lot 4.
        //
        // D37 ne contrôlait que le rôle de la connexion. `palier_auth` détient
        // désormais le seul chemin vers les empreintes de mots de passe, les
        // secrets TOTP et les sessions : ouvrir cet accès sans étendre le
        // contrôle qui le surveille reviendrait à poser une porte sans serrure.
        if (diagnostic.AuthIsSuperutilisateur || diagnostic.AuthIsContournementRls)
        {
            var attributsAuth = string.Join(
                " et ",
                new[]
                {
                    diagnostic.AuthIsSuperutilisateur ? "SUPERUSER" : null,
                    diagnostic.AuthIsContournementRls ? "BYPASSRLS" : null,
                }.Where(a => a is not null)
            );
            throw new IsolationNonGarantieException(
                $"Le rôle « palier_auth » porte {attributsAuth}. C'est le seul chemin vers les "
                    + "tables d'identité, que D38 avait fermées en refus par défaut : s'il "
                    + "contourne RLS, cette fermeture devient décorative et les politiques "
                    + "posées au lot 4 ne mordent plus. Vérifier `db/amorcage/01-roles.sql` — "
                    + "le rôle y est déclaré NOBYPASSRLS."
            );
        }

        if (diagnostic.AuthTablesPossedees.Count > 0)
        {
            throw new IsolationNonGarantieException(
                "Le rôle « palier_auth » est PROPRIÉTAIRE de "
                    + $"{diagnostic.AuthTablesPossedees.Count.ToString(CultureInfo.InvariantCulture)} "
                    + $"table(s) de `public` : {string.Join(", ", diagnostic.AuthTablesPossedees)}. "
                    + "« Table owners normally bypass row security as well » — un propriétaire "
                    + "échappe à ses propres politiques. Les objets appartiennent à "
                    + "`palier_migrations`, jamais au rôle qui les lit."
            );
        }

        return diagnostic;
    }
}
