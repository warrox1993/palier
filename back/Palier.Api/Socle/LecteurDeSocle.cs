using System.Data.Common;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Palier.Infrastructure;

namespace Palier.Api.Socle;

/// <summary>
/// Ce que les trois requêtes de catalogue de D37 ont lu sur la base RÉELLE.
/// Aucun jugement ici : le diagnostic constate, <see cref="AssertionDIsolation" />
/// décide. Séparer les deux permet d'éprouver ce qui est lu sans avoir à
/// provoquer un refus, et de nommer la cause dans le message de refus.
/// </summary>
/// <param name="Role">Le rôle sous lequel l'API se connecte — <c>current_user</c>.</param>
/// <param name="IsSuperutilisateur"><c>rolsuper</c> : contourne tout, RLS compris.</param>
/// <param name="IsContournementRls"><c>rolbypassrls</c> : contourne RLS et rien d'autre.</param>
/// <param name="TablesPossedees">
/// Les tables de <c>public</c> dont ce rôle est propriétaire. « Table owners
/// normally bypass row security as well » : dès qu'il y en a une, <c>FORCE</c>
/// est la seule barrière restante, et une seule ligne d'une seule migration
/// suffirait à la retirer.
/// </param>
/// <param name="TablesSansForce">Les tables de <c>public</c> sans RLS activée ET forcée.</param>
/// <param name="NombreDeTables">
/// Le nombre total de tables de <c>public</c>. Il est lu pour une seule raison :
/// sur une base non migrée, les trois listes ci-dessus sont VIDES et les trois
/// contrôles passent au vert sans avoir rien regardé. Quatrième question du
/// franchissement — un contrôle qui n'a plus de cible doit crier.
/// </param>
/// <param name="AuthIsSuperutilisateur">
/// <c>palier_auth</c> est-il superutilisateur ? Il détient le seul chemin vers
/// les tables d'identité : la question se pose sur lui comme sur le rôle de
/// l'API.
/// </param>
/// <param name="AuthIsContournementRls">
/// <c>palier_auth</c> porte-t-il <c>BYPASSRLS</c> ? Si oui, la fermeture que D38
/// avait posée sur les tables d'identité devient décorative.
/// </param>
/// <param name="AuthTablesPossedees">
/// Les tables de <c>public</c> que <c>palier_auth</c> possède. « Table owners
/// normally bypass row security as well » : la liste doit rester vide.
/// </param>
internal sealed record DiagnosticDIsolation(
    string Role,
    bool IsSuperutilisateur,
    bool IsContournementRls,
    IReadOnlyList<string> TablesPossedees,
    IReadOnlyList<string> TablesSansForce,
    int NombreDeTables,
    // ---- Le quatrième rôle, lot 4 ------------------------------------------
    //
    // D37 ne contrôlait que le rôle de la connexion. `palier_auth` détient
    // désormais le seul chemin vers les empreintes de mots de passe, les secrets
    // TOTP et les sessions : les mêmes questions se posent sur lui, et y
    // répondre suppose de l'interroger nommément — `pg_roles` est « a publicly
    // readable view », donc `palier_app` le peut sans aucun privilège.
    bool AuthIsSuperutilisateur,
    bool AuthIsContournementRls,
    IReadOnlyList<string> AuthTablesPossedees
);

/// <summary>
/// Ce que <c>GET /api/v1/sante</c> rend. AUCUN compte, AUCUNE donnée de santé —
/// `01-conformite.md` § 4. Trois champs, et pas un de plus : la version de
/// schéma réellement appliquée, la base joignable, le nombre de lignes du
/// référentiel de nutriments.
/// </summary>
/// <param name="Schema">Le dernier <c>MigrationId</c> inscrit dans l'historique.</param>
/// <param name="IsBaseJoignable">Toujours vrai quand la réponse sort en 200 ; la route rend 503 sinon.</param>
/// <param name="ReferentielNutriments">Le nombre de lignes de <c>nutrient_refs</c>. Zéro = référentiel non chargé.</param>
internal sealed record EtatDuSocle(string Schema, bool IsBaseJoignable, long ReferentielNutriments);

/// <summary>
/// LE SEUL TYPE DE CE LOT QUI TIENT <c>PalierDbContext</c> HORS DU PIPELINE, et
/// il est nommément exempté dans <c>ArchitectureTests</c>, avec son motif écrit
/// dans l'épreuve.
///
/// Pourquoi l'exemption est légitime ici, et nulle part ailleurs : ce lecteur ne
/// touche AUCUNE table portant une donnée personnelle. Il interroge les
/// catalogues du moteur (<c>pg_roles</c>, <c>pg_class</c>), l'historique des
/// migrations et le compte de <c>nutrient_refs</c> — une table de référence
/// publique dont la politique est <c>using (true)</c> en lecture seule. Il n'a
/// donc pas d'identité à poser, et il ne peut pas en avoir : l'assertion tourne
/// AVANT que le serveur accepte la moindre requête, et la route de santé répond
/// sans authentification jusqu'au lot 4 (D41).
///
/// Une exemption non nommée est une porte laissée ouverte. Celle-ci est écrite,
/// datée, et l'épreuve refuse le dépôt si le type qu'elle exempte disparaît.
/// </summary>
internal sealed class LecteurDeSocle(PalierDbContext contexte)
{
    // 1. Le rôle de l'API ne contourne pas RLS.
    private const string _requeteRole = """
        select current_user::text, rolsuper, rolbypassrls
          from pg_roles where rolname = current_user
        """;

    // 2. Le rôle de l'API ne possède aucune table
    //    (sinon FORCE est la seule barrière restante).
    private const string _requeteTablesPossedees = """
        select c.relname from pg_class c join pg_namespace n on n.oid = c.relnamespace
         where n.nspname = 'public' and c.relkind = 'r'
           and pg_get_userbyid(c.relowner) = current_user
         order by c.relname
        """;

    // 3. Toute table a RLS activée ET forcée.
    private const string _requeteRoleAuth = """
        select rolsuper, rolbypassrls from pg_roles where rolname = 'palier_auth'
        """;

    private const string _requeteTablesPossedeesParAuth = """
        select c.relname from pg_class c join pg_namespace n on n.oid = c.relnamespace
         where n.nspname = 'public' and c.relkind = 'r'
           and pg_get_userbyid(c.relowner) = 'palier_auth'
         order by c.relname
        """;

    private const string _requeteTablesSansForce = """
        select c.relname from pg_class c join pg_namespace n on n.oid = c.relnamespace
         where n.nspname = 'public' and c.relkind = 'r'
           and (not c.relrowsecurity or not c.relforcerowsecurity)
         order by c.relname
        """;

    // 4. La borne : sur une base non migrée, les trois listes sont vides.
    private const string _requeteNombreDeTables = """
        select count(*)::int from pg_class c join pg_namespace n on n.oid = c.relnamespace
         where n.nspname = 'public' and c.relkind = 'r'
        """;

    private const string _requeteVersion = """
        select "MigrationId" from public."__EFMigrationsHistory"
         order by "MigrationId" desc limit 1
        """;

    private const string _requeteReferentiel = "select count(*) from public.nutrient_refs";

    /// <summary>Les quatre requêtes de catalogue, sur la connexion réelle de l'API.</summary>
    public async Task<DiagnosticDIsolation> DiagnostiquerAsync(CancellationToken jeton = default)
    {
        await contexte.Database.OpenConnectionAsync(jeton).ConfigureAwait(false);
        try
        {
            var connexion = contexte.Database.GetDbConnection();

            var role = string.Empty;
            var isSuperutilisateur = false;
            var isContournement = false;
            using (var commande = connexion.CreateCommand())
            {
                // `_requeteRole` est un `private const string` littéral, ligne 70.
                // Aucune concaténation, aucune interpolation, et ce lecteur n'a
                // pas un seul paramètre d'entrée. semgrep ne suit pas les `const`
                // C# et ne lit pas le `#pragma CA2100` que Roslyn honore déjà.
                //
                // L'exclusion est posée LIGNE PAR LIGNE et RÈGLE PAR RÈGLE :
                // l'écarter au niveau du fichier ou du dossier rendrait le
                // détecteur aveugle sur du code qui, lui, prendra des entrées.
                //
                // Deux contraintes, chacune payée par une CI rouge :
                // 1. le marqueur doit être la ligne IMMÉDIATEMENT précédente —
                //    quatre lignes de commentaire l'en séparaient, il était
                //    ignoré sans un mot ;
                // 2. l'identifiant doit être COMPLET. Le dernier segment est
                //    doublé — `…csharp-sqli.csharp-sqli` — et un identifiant
                //    partiel ne correspond à rien, en silence lui aussi.
                // Vérifié en lançant semgrep localement, pas en poussant.
                // nosemgrep: csharp.lang.security.sqli.csharp-sqli.csharp-sqli
                commande.CommandText = _requeteRole;
                var lecteur = await commande.ExecuteReaderAsync(jeton).ConfigureAwait(false);
                await using (lecteur.ConfigureAwait(false))
                {
                    if (await lecteur.ReadAsync(jeton).ConfigureAwait(false))
                    {
                        role = lecteur.GetString(0);
                        isSuperutilisateur = lecteur.GetBoolean(1);
                        isContournement = lecteur.GetBoolean(2);
                    }
                }
            }

            var possedees = await ListerAsync(connexion, _requeteTablesPossedees, jeton)
                .ConfigureAwait(false);
            var sansForce = await ListerAsync(connexion, _requeteTablesSansForce, jeton)
                .ConfigureAwait(false);
            var nombre = await EntierAsync(connexion, _requeteNombreDeTables, jeton)
                .ConfigureAwait(false);

            // Le quatrième rôle est interrogé NOMMÉMENT : il ne s'agit pas de la
            // connexion courante, mais du rôle qui détiendra le seul chemin vers
            // les tables d'identité.
            var authSuperutilisateur = false;
            var authContournement = false;
            using (var commande = connexion.CreateCommand())
            {
                commande.CommandText = _requeteRoleAuth;
                var lecteur = await commande.ExecuteReaderAsync(jeton).ConfigureAwait(false);
                await using (lecteur.ConfigureAwait(false))
                {
                    if (await lecteur.ReadAsync(jeton).ConfigureAwait(false))
                    {
                        authSuperutilisateur = lecteur.GetBoolean(0);
                        authContournement = lecteur.GetBoolean(1);
                    }
                }
            }

            var authPossedees = await ListerAsync(
                    connexion,
                    _requeteTablesPossedeesParAuth,
                    jeton
                )
                .ConfigureAwait(false);

            return new DiagnosticDIsolation(
                role,
                isSuperutilisateur,
                isContournement,
                possedees,
                sansForce,
                (int)nombre,
                authSuperutilisateur,
                authContournement,
                authPossedees
            );
        }
        finally
        {
            await contexte.Database.CloseConnectionAsync().ConfigureAwait(false);
        }
    }

    /// <summary>L'état rendu par la route de santé.</summary>
    public async Task<EtatDuSocle> LireAsync(CancellationToken jeton = default)
    {
        await contexte.Database.OpenConnectionAsync(jeton).ConfigureAwait(false);
        try
        {
            var connexion = contexte.Database.GetDbConnection();
            var schema = await TexteAsync(connexion, _requeteVersion, jeton).ConfigureAwait(false);
            var compte = await EntierAsync(connexion, _requeteReferentiel, jeton)
                .ConfigureAwait(false);
            return new EtatDuSocle(schema ?? string.Empty, true, compte);
        }
        finally
        {
            await contexte.Database.CloseConnectionAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Prépare la commande. Le <c>#pragma</c> est le même que celui de
    /// <c>SchemaTests</c>, avec le même motif : CA2100 ne sait pas prouver qu'un
    /// paramètre <c>string</c> vient d'une constante. Les six requêtes de ce
    /// fichier sont des <c>private const</c>, aucune n'est concaténée, et
    /// AUCUNE ne reçoit quoi que ce soit de l'extérieur — ce lecteur n'a pas un
    /// seul paramètre d'entrée. Élargir la règle ailleurs serait le mauvais
    /// geste ; l'éteindre sur trois lignes nommées est le bon.
    /// </summary>
    private static DbCommand Preparer(DbConnection connexion, string sql)
    {
        var commande = connexion.CreateCommand();
#pragma warning disable CA2100
        // Voir le commentaire de documentation ci-dessus : `sql` ne reçoit que
        // des `private const` de ce fichier. Même raison de l'écarter ici
        // plutôt qu'au niveau du fichier, et même contrainte de placement.
        // nosemgrep: csharp.lang.security.sqli.csharp-sqli.csharp-sqli
        commande.CommandText = sql;
#pragma warning restore CA2100
        return commande;
    }

    private static async Task<IReadOnlyList<string>> ListerAsync(
        DbConnection connexion,
        string sql,
        CancellationToken jeton
    )
    {
        using var commande = Preparer(connexion, sql);
        var noms = new List<string>();
        var lecteur = await commande.ExecuteReaderAsync(jeton).ConfigureAwait(false);
        await using (lecteur.ConfigureAwait(false))
        {
            while (await lecteur.ReadAsync(jeton).ConfigureAwait(false))
            {
                noms.Add(lecteur.GetString(0));
            }
        }

        return noms;
    }

    private static async Task<long> EntierAsync(
        DbConnection connexion,
        string sql,
        CancellationToken jeton
    )
    {
        using var commande = Preparer(connexion, sql);
        var brut = await commande.ExecuteScalarAsync(jeton).ConfigureAwait(false);
        return brut is null ? 0L : Convert.ToInt64(brut, CultureInfo.InvariantCulture);
    }

    private static async Task<string?> TexteAsync(
        DbConnection connexion,
        string sql,
        CancellationToken jeton
    )
    {
        using var commande = Preparer(connexion, sql);
        var brut = await commande.ExecuteScalarAsync(jeton).ConfigureAwait(false);
        return brut as string;
    }
}
