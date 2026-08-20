
using Npgsql;

namespace Palier.Database.Tests;

/// <summary>
/// La collation du cluster — D44.
///
/// Ce que ces épreuves protègent n'est PAS l'ordre de tri d'aujourd'hui. Mesuré
/// le 20/08/2026 : `en_US.utf8` sous glibc et `fr-BE` sous ICU rendent le MÊME
/// ordre sur « eau &lt; Éclair &lt; élan &lt; Ève &lt; œuf &lt; zèbre ». La glibc applique
/// ISO 14651, qui gère déjà les accents, la casse et la ligature « œ ». Affirmer
/// que le tri français serait faux sous `libc` serait faux.
///
/// Ce qu'elles protègent est la STABILITÉ de ce tri d'un environnement à
/// l'autre. Sous le fournisseur `libc`, `datcollversion` est la version de la
/// glibc de l'image — 2.41 sur l'image Debian que `db/compose.yaml` désigne, et
/// le tag n'est PAS recopié ici : `tests-harness/db.test.mjs` refuse toute
/// seconde déclaration, y compris dans un commentaire, et il a mordu sur cette
/// ligne même le 20/08/2026. Elle change avec l'image de
/// base, avec une mise à jour du socle de l'hébergeur, avec un passage de
/// Debian 13 à 14. Or un changement d'ordre de tri INVALIDE les index sur les
/// colonnes texte : les requêtes rendent alors des résultats faux, et
/// PostgreSQL ne le signale que par un avertissement au démarrage.
///
/// Sous ICU, la version est celle de la bibliothèque — 153.128 — versionnée
/// indépendamment du système, et la locale est inscrite DANS la base plutôt
/// qu'héritée d'une variable d'environnement.
///
/// Sans ces épreuves, le réglage disparaîtrait au premier `npm run db:reset` de
/// quelqu'un qui ignore pourquoi il est là — et le symptôme n'apparaîtrait
/// qu'en production, sur un ordre de résultats.
/// </summary>
[Collection(BaseFixture.Collection)]
public sealed class CollationTests(BaseFixture baseDeDonnees)
{
    /// <summary>Les six mots qui exercent accents, casse et ligature.</summary>
    private static readonly string[] _mots =
    [
        "zèbre",
        "œuf",
        "Éclair",
        "eau",
        "élan",
        "Ève",
    ];

    [Fact]
    public async Task Le_cluster_est_en_ICU_fr_BE_et_non_sur_le_fournisseur_du_systeme()
    {
        // On interroge le MOTEUR, pas le fichier compose : un argument accepté
        // par `initdb` n'est pas un argument appliqué (P11). En PostgreSQL 18 la
        // colonne s'appelle `datlocale` — `daticulocale` a disparu, et une
        // épreuve écrite sur l'ancien nom échouerait à la compilation SQL, pas
        // en silence.
        await using var source = new NpgsqlDataSourceBuilder(baseDeDonnees.ChaineApp).Build();
        await using var connexion = await source.OpenConnectionAsync();
        await using var commande = new NpgsqlCommand(
            """
            select datlocprovider::text, coalesce(datlocale, ''), coalesce(datcollversion, '')
            from pg_database
            where datname = current_database()
            """,
            connexion
        );
        await using var lecteur = await commande.ExecuteReaderAsync();
        Assert.True(await lecteur.ReadAsync(), "pg_database n'a rendu aucune ligne");

        var fournisseur = lecteur.GetString(0);
        var locale = lecteur.GetString(1);
        var versionCollation = lecteur.GetString(2);

        Assert.True(
            fournisseur == "i",
            $"Le cluster utilise le fournisseur de collation « {fournisseur} » et non « i » (ICU). "
                + "Sous « c » (libc), la version de collation est celle de la glibc de l'image : "
                + "elle change avec le système, et un changement d'ordre de tri invalide les index "
                + "sur les colonnes texte. Vérifier POSTGRES_INITDB_ARGS dans db/compose.yaml, "
                + "puis `npm run db:reset` — l'entrypoint n'initialise que sur un volume vide."
        );

        Assert.True(
            locale == "fr-BE",
            $"La locale du cluster est « {locale} » et non « fr-BE ». Le produit s'adresse à une "
                + "clientèle belge francophone ; la locale est inscrite dans la base, pas héritée "
                + "d'une variable d'environnement."
        );

        // La version de collation est relevée, jamais assertée sur une valeur :
        // elle CHANGERA avec ICU, et c'est légitime. Ce qui compte est qu'elle
        // soit portée par la bibliothèque et non par le système.
        Assert.False(
            string.IsNullOrWhiteSpace(versionCollation),
            "La base ne porte aucune version de collation : PostgreSQL ne pourra pas signaler "
                + "un changement d'ordre de tri, et les index deviendraient faux en silence."
        );
    }

    [Fact]
    public async Task Le_tri_place_les_accents_la_casse_et_la_ligature_a_leur_place()
    {
        // L'ordre attendu est celui du français : les accents s'intercalent au
        // lieu de rejeter le mot en fin de liste, la casse est ignorée au
        // premier niveau, et « œ » se trie comme « oe ».
        //
        // Cette épreuve rougirait sous un tri octet par octet — celui que musl
        // impose, faute de supporter LC_COLLATE. C'est le motif pour lequel D34
        // écarte la variante `alpine` de l'image, et cette épreuve est ce qui
        // rend ce motif vérifiable au lieu d'être une note dans un commentaire.
        await using var source = new NpgsqlDataSourceBuilder(baseDeDonnees.ChaineApp).Build();
        await using var connexion = await source.OpenConnectionAsync();
        await using var commande = new NpgsqlCommand(
            "select m from unnest(@mots) as m order by m",
            connexion
        );
        commande.Parameters.AddWithValue("mots", _mots);

        var obtenu = new List<string>();
        await using (var lecteur = await commande.ExecuteReaderAsync())
        {
            while (await lecteur.ReadAsync())
            {
                obtenu.Add(lecteur.GetString(0));
            }
        }

        string[] attendu = ["eau", "Éclair", "élan", "Ève", "œuf", "zèbre"];
        Assert.True(
            attendu.SequenceEqual(obtenu, StringComparer.Ordinal),
            "Le tri ne suit pas les règles du français.\n"
                + "  attendu : "
                + string.Join(" < ", attendu)
                + "\n  obtenu  : "
                + string.Join(" < ", obtenu)
                + "\nUn tri octet par octet placerait les majuscules avant les minuscules et "
                + "rejetterait les accents en fin de liste."
        );
    }
}
