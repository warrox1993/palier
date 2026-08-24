using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Palier.Api.Entrainement;
using Palier.Application.Entrainement;
using Palier.Application.Pipeline;
using Palier.Infrastructure;
using Palier.Infrastructure.Entrainement;
using Palier.Infrastructure.Identite;

namespace Palier.Database.Tests;

/// <summary>
/// Les programmes sur le moteur réel — la seconde table mixte du schéma.
/// </summary>
/// <remarks>
/// <para>
/// La forme est celle du catalogue d'exercices — D72 — et elle produit les
/// mêmes trois refus distincts que le moteur réduit tous à « zéro ligne » : il
/// n'existe pas, c'est un modèle, il est à moi. Ces épreuves les provoquent un
/// par un.
/// </para>
///
/// <para>
/// <b>Deux tables filles s'y ajoutent</b>, qui ne portent aucun
/// <c>owner_id</c> : leur appartenance se lit en remontant au programme. C'est
/// l'endroit où une politique mal écrite laisserait fuir le contenu d'un
/// programme dont l'en-tête, lui, resterait caché.
/// </para>
/// </remarks>
[Collection(BaseFixture.Collection)]
public sealed class EntrainementProgrammesTests(BaseFixture baseDeDonnees)
{
    private const string _motDePasse = "brouette-hivernale-38-oscille";

    // ================================================================
    // Lister et lire
    // ================================================================

    [Fact]
    public async Task La_liste_rend_les_MODELES_et_LES_SIENS()
    {
        var proprietaire = await CompteAsync();
        var modele = await ModeleAsync();
        await using var hote = Hote(proprietaire);
        using var portee = hote.Services.CreateScope();

        var sien = await CreerAsync(portee.ServiceProvider, "Mon programme");
        var identifiants = await ListerAsync(portee.ServiceProvider);

        Assert.Contains(modele, identifiants);
        Assert.Contains(sien, identifiants);
    }

    [Fact]
    public async Task La_liste_de_B_ne_porte_AUCUN_programme_de_A()
    {
        // L'ISOLATION, éprouvée sur un vrai moteur et non déduite des
        // politiques. C'est la promesse que tout le reste suppose.
        var a = await CompteAsync();
        var b = await CompteAsync();

        Guid deA;
        await using (var hoteA = Hote(a))
        {
            using var porteeA = hoteA.Services.CreateScope();
            deA = await CreerAsync(porteeA.ServiceProvider, "Secret de A");
        }

        await using var hoteB = Hote(b);
        using var porteeB = hoteB.Services.CreateScope();

        Assert.DoesNotContain(deA, await ListerAsync(porteeB.ServiceProvider));
    }

    [Fact]
    public async Task Le_CONTENU_du_programme_de_A_est_invisible_a_B()
    {
        // La fuite que les deux tables filles rendent possible : leur politique
        // remonte au programme, et une remontée mal écrite laisserait sortir
        // les séances d'un programme dont l'en-tête reste caché.
        //
        // L'épreuve interroge donc les tables DIRECTEMENT, sous l'identité de
        // B. Passer par la route la masquerait, puisque la route ne trouve déjà
        // pas le programme.
        var a = await CompteAsync();
        var b = await CompteAsync();

        Guid deA;
        await using (var hoteA = Hote(a))
        {
            using var porteeA = hoteA.Services.CreateScope();
            deA = await CreerAsync(
                porteeA.ServiceProvider,
                "Programme de A",
                [new EcritureDeSeance("Jour secret", [])]
            );
        }

        await using var hoteB = Hote(b);
        using var porteeB = hoteB.Services.CreateScope();
        var contexte = porteeB.ServiceProvider.GetRequiredService<PalierDbContext>();

        // La lecture passe par le pipeline, donc sous l'identité de B posée au
        // moteur — sans quoi RLS n'aurait rien à borner.
        var executeur = porteeB.ServiceProvider.GetRequiredService<IExecuteurDeCasDUsage>();
        var seances = await executeur.ExecuterAsync(
            "LectureDirecte",
            async jeton =>
                await Microsoft
                    .EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync(
                        contexte.ProgramDays.Where(d => d.ProgramId == deA),
                        jeton
                    )
                    .ConfigureAwait(false),
            CancellationToken.None
        );

        Assert.Empty(seances);
    }

    [Fact]
    public async Task Un_programme_D_AUTRUI_est_INTROUVABLE()
    {
        var a = await CompteAsync();
        var b = await CompteAsync();

        Guid deA;
        await using (var hoteA = Hote(a))
        {
            using var porteeA = hoteA.Services.CreateScope();
            deA = await CreerAsync(porteeA.ServiceProvider, "Secret de A");
        }

        await using var hoteB = Hote(b);
        using var porteeB = hoteB.Services.CreateScope();

        var (code, _) = await LireAsync(porteeB.ServiceProvider, deA);

        // 404 et non 403 : l'existence même du programme de A est un secret.
        Assert.Equal(StatusCodes.Status404NotFound, code);
    }

    [Fact]
    public async Task Un_programme_INEXISTANT_rend_404()
    {
        var compte = await CompteAsync();
        await using var hote = Hote(compte);
        using var portee = hote.Services.CreateScope();

        var (code, _) = await LireAsync(portee.ServiceProvider, Guid.NewGuid());

        Assert.Equal(StatusCodes.Status404NotFound, code);
    }

    [Fact]
    public async Task La_lecture_rend_les_SEANCES_et_leurs_EXERCICES()
    {
        var compte = await CompteAsync();
        var exercice = await ExercicePublicAsync();
        await using var hote = Hote(compte);
        using var portee = hote.Services.CreateScope();

        var programme = await CreerAsync(
            portee.ServiceProvider,
            "Haut / Bas",
            [
                new EcritureDeSeance(
                    "Haut",
                    [new EcritureDExerciceDeSeance(exercice, 4, 6, 10, 2, 120, "Lentement.")]
                ),
                new EcritureDeSeance("Bas", []),
            ]
        );

        var (code, corps) = await LireAsync(portee.ServiceProvider, programme);
        Assert.Equal(StatusCodes.Status200OK, code);

        var seances = JsonDocument.Parse(corps).RootElement.GetProperty("seances");
        Assert.Equal(2, seances.GetArrayLength());

        // LES RANGS SONT ATTRIBUÉS PAR LE SERVEUR à partir de l'ordre reçu. Le
        // client n'en envoie aucun : s'il le pouvait, il poserait deux fois le
        // même et l'index unique refuserait sur un conflit qui n'existe pas.
        Assert.Equal(1, seances[0].GetProperty("position").GetInt32());
        Assert.Equal(2, seances[1].GetProperty("position").GetInt32());
        Assert.Equal("Haut", seances[0].GetProperty("libelle").GetString());

        var poses = seances[0].GetProperty("exercices");
        Assert.Equal(1, poses.GetArrayLength());
        Assert.Equal(exercice, poses[0].GetProperty("exerciceId").GetGuid());
        Assert.Equal(1, poses[0].GetProperty("position").GetInt32());
        Assert.Equal(4, poses[0].GetProperty("series").GetInt32());

        // La séance vide sort AVEC le programme, et non pas absente. Un écran
        // qui ne la recevrait pas ne pourrait pas la remplir.
        Assert.Equal(0, seances[1].GetProperty("exercices").GetArrayLength());
    }

    // ================================================================
    // Ce qu'on n'a pas le droit de faire à un modèle
    // ================================================================

    [Fact]
    public async Task Un_modele_ne_se_REMPLACE_pas()
    {
        var compte = await CompteAsync();
        var modele = await ModeleAsync();
        await using var hote = Hote(compte);
        using var portee = hote.Services.CreateScope();

        var (code, corps) = await RemplacerAsync(
            portee.ServiceProvider,
            modele,
            new EcritureDeProgramme("Détourné", null, true, null)
        );

        // 403 et non 404 : l'appelant vient de le lire dans la liste, un 404
        // serait un mensonge.
        Assert.Equal(StatusCodes.Status403Forbidden, code);
        Assert.Contains("ProgrammeModele", corps, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Un_modele_ne_se_SUPPRIME_pas()
    {
        var compte = await CompteAsync();
        var modele = await ModeleAsync();
        await using var hote = Hote(compte);
        using var portee = hote.Services.CreateScope();

        var (code, corps) = await SupprimerAsync(portee.ServiceProvider, modele);

        Assert.Equal(StatusCodes.Status403Forbidden, code);
        Assert.Contains("ProgrammeModele", corps, StringComparison.Ordinal);
    }

    // ================================================================
    // La copie — D74
    // ================================================================

    [Fact]
    public async Task Copier_un_modele_rend_un_programme_PERSONNEL()
    {
        var compte = await CompteAsync();
        var exercice = await ExercicePublicAsync();
        var modele = await ModeleAsync(exercice);
        await using var hote = Hote(compte);
        using var portee = hote.Services.CreateScope();

        var copie = await CopierAsync(portee.ServiceProvider, modele);

        var (code, corps) = await LireAsync(portee.ServiceProvider, copie);
        Assert.Equal(StatusCodes.Status200OK, code);

        var lu = JsonDocument.Parse(corps).RootElement;

        // La copie n'est PAS un modèle, et elle n'a PAS de slug : le slug est la
        // clé naturelle du catalogue, et son index unique partiel ne porte que
        // sur les modèles.
        Assert.False(lu.GetProperty("estUnModele").GetBoolean());
        Assert.Equal(JsonValueKind.Null, lu.GetProperty("slug").ValueKind);

        // LES NOTES SUIVENT. Elles expliquent pourquoi un exercice est absent,
        // et `14-contenu.md` § 2 dit ce que leur perte coûterait.
        Assert.False(string.IsNullOrEmpty(lu.GetProperty("notes").GetString()));

        // Le contenu suit aussi, sinon la copie ne servirait à rien.
        var seances = lu.GetProperty("seances");
        Assert.Equal(1, seances.GetArrayLength());
        Assert.Equal(
            exercice,
            seances[0].GetProperty("exercices")[0].GetProperty("exerciceId").GetGuid()
        );
    }

    [Fact]
    public async Task La_copie_est_MODIFIABLE_alors_que_son_modele_ne_l_est_pas()
    {
        // C'est tout l'intérêt de D74 : une copie, pas une référence.
        var compte = await CompteAsync();
        var modele = await ModeleAsync();
        await using var hote = Hote(compte);
        using var portee = hote.Services.CreateScope();

        var copie = await CopierAsync(portee.ServiceProvider, modele);

        var (code, _) = await RemplacerAsync(
            portee.ServiceProvider,
            copie,
            new EcritureDeProgramme("À ma façon", null, true, [new EcritureDeSeance("Lundi", [])])
        );

        Assert.Equal(StatusCodes.Status204NoContent, code);
    }

    [Fact]
    public async Task Copier_un_programme_INTROUVABLE_rend_404()
    {
        var compte = await CompteAsync();
        await using var hote = Hote(compte);
        using var portee = hote.Services.CreateScope();

        var (code, corps) = await HarnaisHttp.ExecuterAsync(
            portee.ServiceProvider,
            Programmes.CopierAsync(
                Guid.NewGuid(),
                portee.ServiceProvider.GetRequiredService<IExecuteurDeCasDUsage>(),
                portee.ServiceProvider.GetRequiredService<CopierUnProgramme>(),
                CancellationToken.None
            )
        );

        Assert.Equal(StatusCodes.Status404NotFound, code);
        Assert.Contains("ProgrammeIntrouvable", corps, StringComparison.Ordinal);
    }

    // ================================================================
    // Écrire
    // ================================================================

    [Fact]
    public async Task Un_exercice_INCONNU_est_refuse_en_400()
    {
        // Sans ce contrôle, la clé étrangère lèverait, et le pipeline rendrait
        // un 500 là où la requête est simplement fautive.
        var compte = await CompteAsync();
        await using var hote = Hote(compte);
        using var portee = hote.Services.CreateScope();

        var (code, corps) = await HarnaisHttp.ExecuterAsync(
            portee.ServiceProvider,
            Programmes.CreerAsync(
                new EcritureDeProgramme(
                    "Avec un fantôme",
                    null,
                    true,
                    [
                        new EcritureDeSeance(
                            "A",
                            [new EcritureDExerciceDeSeance(Guid.NewGuid(), 3, 8, 12, 2, 90, null)]
                        ),
                    ]
                ),
                portee.ServiceProvider.GetRequiredService<IExecuteurDeCasDUsage>(),
                portee.ServiceProvider.GetRequiredService<CreerUnProgramme>(),
                CancellationToken.None
            )
        );

        Assert.Equal(StatusCodes.Status400BadRequest, code);
        Assert.Contains("ExerciceInconnu", corps, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Un_exercice_D_AUTRUI_est_refuse_en_400()
    {
        // L'exercice existe, mais RLS le rend invisible à l'appelant : pour lui
        // il n'existe pas, et le refus doit être le même que pour un
        // identifiant inventé. Un message différent apprendrait à B que
        // l'exercice de A existe.
        var a = await CompteAsync();
        var b = await CompteAsync();

        Guid deA;
        await using (var hoteA = Hote(a))
        {
            using var porteeA = hoteA.Services.CreateScope();
            deA = await ExercicePersonnelAsync(porteeA.ServiceProvider);
        }

        await using var hoteB = Hote(b);
        using var porteeB = hoteB.Services.CreateScope();

        var (code, corps) = await HarnaisHttp.ExecuterAsync(
            porteeB.ServiceProvider,
            Programmes.CreerAsync(
                new EcritureDeProgramme(
                    "Avec l'exercice de A",
                    null,
                    true,
                    [
                        new EcritureDeSeance(
                            "A",
                            [new EcritureDExerciceDeSeance(deA, 3, 8, 12, 2, 90, null)]
                        ),
                    ]
                ),
                porteeB.ServiceProvider.GetRequiredService<IExecuteurDeCasDUsage>(),
                porteeB.ServiceProvider.GetRequiredService<CreerUnProgramme>(),
                CancellationToken.None
            )
        );

        Assert.Equal(StatusCodes.Status400BadRequest, code);
        Assert.Contains("ExerciceInconnu", corps, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Un_nom_ABSENT_est_refuse_en_400()
    {
        var compte = await CompteAsync();
        await using var hote = Hote(compte);
        using var portee = hote.Services.CreateScope();

        var (code, corps) = await HarnaisHttp.ExecuterAsync(
            portee.ServiceProvider,
            Programmes.CreerAsync(
                new EcritureDeProgramme("   ", null, true, null),
                portee.ServiceProvider.GetRequiredService<IExecuteurDeCasDUsage>(),
                portee.ServiceProvider.GetRequiredService<CreerUnProgramme>(),
                CancellationToken.None
            )
        );

        Assert.Equal(StatusCodes.Status400BadRequest, code);
        Assert.Contains("NomRequis", corps, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Le_remplacement_ECRASE_l_ancien_contenu()
    {
        // Un PUT remplace, il ne fusionne pas. Sans cette épreuve, une
        // suppression oubliée laisserait les anciennes séances s'accumuler à
        // chaque enregistrement.
        var compte = await CompteAsync();
        await using var hote = Hote(compte);
        using var portee = hote.Services.CreateScope();

        var programme = await CreerAsync(
            portee.ServiceProvider,
            "Avant",
            [new EcritureDeSeance("Lundi", []), new EcritureDeSeance("Mardi", [])]
        );

        var (code, _) = await RemplacerAsync(
            portee.ServiceProvider,
            programme,
            new EcritureDeProgramme("Après", null, true, [new EcritureDeSeance("Samedi", [])])
        );
        Assert.Equal(StatusCodes.Status204NoContent, code);

        var (_, corps) = await LireAsync(portee.ServiceProvider, programme);
        var lu = JsonDocument.Parse(corps).RootElement;

        Assert.Equal("Après", lu.GetProperty("nom").GetString());
        var seances = lu.GetProperty("seances");
        Assert.Equal(1, seances.GetArrayLength());
        Assert.Equal("Samedi", seances[0].GetProperty("libelle").GetString());
        Assert.Equal(1, seances[0].GetProperty("position").GetInt32());
    }

    [Fact]
    public async Task Remplacer_un_programme_INTROUVABLE_rend_404()
    {
        var compte = await CompteAsync();
        await using var hote = Hote(compte);
        using var portee = hote.Services.CreateScope();

        var (code, corps) = await RemplacerAsync(
            portee.ServiceProvider,
            Guid.NewGuid(),
            new EcritureDeProgramme("Nulle part", null, true, null)
        );

        Assert.Equal(StatusCodes.Status404NotFound, code);
        Assert.Contains("ProgrammeIntrouvable", corps, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Remplacer_avec_un_exercice_INCONNU_rend_400()
    {
        var compte = await CompteAsync();
        await using var hote = Hote(compte);
        using var portee = hote.Services.CreateScope();

        var programme = await CreerAsync(portee.ServiceProvider, "Le mien");

        var (code, corps) = await RemplacerAsync(
            portee.ServiceProvider,
            programme,
            new EcritureDeProgramme(
                "Le mien",
                null,
                true,
                [
                    new EcritureDeSeance(
                        "A",
                        [new EcritureDExerciceDeSeance(Guid.NewGuid(), 3, 8, 12, 2, 90, null)]
                    ),
                ]
            )
        );

        Assert.Equal(StatusCodes.Status400BadRequest, code);
        Assert.Contains("ExerciceInconnu", corps, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Remplacer_le_programme_D_AUTRUI_rend_404()
    {
        // L'ÉCRITURE, et non plus la lecture. C'est le chemin dangereux : un
        // PUT qui aboutirait réécrirait le programme de quelqu'un d'autre, là
        // où une lecture ne fait que le montrer.
        //
        // Rien dans le gestionnaire ne compare les propriétaires — c'est RLS
        // qui rend la ligne introuvable. Cette épreuve constate que la
        // délégation tient, plutôt que de la supposer.
        var a = await CompteAsync();
        var b = await CompteAsync();

        Guid deA;
        await using (var hoteA = Hote(a))
        {
            using var porteeA = hoteA.Services.CreateScope();
            deA = await CreerAsync(
                porteeA.ServiceProvider,
                "Programme de A",
                [new EcritureDeSeance("Lundi", [])]
            );
        }

        await using var hoteB = Hote(b);
        using var porteeB = hoteB.Services.CreateScope();

        var (code, corps) = await RemplacerAsync(
            porteeB.ServiceProvider,
            deA,
            new EcritureDeProgramme("Détourné par B", null, true, null)
        );

        Assert.Equal(StatusCodes.Status404NotFound, code);
        Assert.Contains("ProgrammeIntrouvable", corps, StringComparison.Ordinal);

        // ET LE PROGRAMME DE A EST INTACT. Le code de retour seul ne le dirait
        // pas : une écriture partielle suivie d'un échec rendrait aussi 404.
        await using var encoreA = Hote(a);
        using var porteeAprès = encoreA.Services.CreateScope();
        var (_, relu) = await LireAsync(porteeAprès.ServiceProvider, deA);

        var lu = JsonDocument.Parse(relu).RootElement;
        Assert.Equal("Programme de A", lu.GetProperty("nom").GetString());
        Assert.Equal(1, lu.GetProperty("seances").GetArrayLength());
    }

    [Fact]
    public async Task Copier_le_programme_D_AUTRUI_rend_404()
    {
        // Un modèle se copie ; le programme d'un autre utilisateur, non. Sans
        // cette épreuve, la route de copie serait le seul chemin de lecture du
        // contenu d'autrui à n'avoir jamais été éprouvé — et c'est celui qui en
        // ferait une copie durable.
        var a = await CompteAsync();
        var b = await CompteAsync();

        Guid deA;
        await using (var hoteA = Hote(a))
        {
            using var porteeA = hoteA.Services.CreateScope();
            deA = await CreerAsync(porteeA.ServiceProvider, "Programme de A");
        }

        await using var hoteB = Hote(b);
        using var porteeB = hoteB.Services.CreateScope();

        var (code, corps) = await HarnaisHttp.ExecuterAsync(
            porteeB.ServiceProvider,
            Programmes.CopierAsync(
                deA,
                porteeB.ServiceProvider.GetRequiredService<IExecuteurDeCasDUsage>(),
                porteeB.ServiceProvider.GetRequiredService<CopierUnProgramme>(),
                CancellationToken.None
            )
        );

        Assert.Equal(StatusCodes.Status404NotFound, code);
        Assert.Contains("ProgrammeIntrouvable", corps, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Un_nom_ABSENT_au_remplacement_est_refuse_en_400()
    {
        var compte = await CompteAsync();
        await using var hote = Hote(compte);
        using var portee = hote.Services.CreateScope();

        var programme = await CreerAsync(portee.ServiceProvider, "Le mien");

        var (code, corps) = await RemplacerAsync(
            portee.ServiceProvider,
            programme,
            new EcritureDeProgramme("", null, true, null)
        );

        Assert.Equal(StatusCodes.Status400BadRequest, code);
        Assert.Contains("NomRequis", corps, StringComparison.Ordinal);
    }

    // ================================================================
    // Supprimer
    // ================================================================

    [Fact]
    public async Task Le_sien_se_SUPPRIME_avec_tout_son_contenu()
    {
        var compte = await CompteAsync();
        var exercice = await ExercicePublicAsync();
        await using var hote = Hote(compte);
        using var portee = hote.Services.CreateScope();

        var programme = await CreerAsync(
            portee.ServiceProvider,
            "À jeter",
            [
                new EcritureDeSeance(
                    "A",
                    [new EcritureDExerciceDeSeance(exercice, 3, 8, 12, 2, 90, null)]
                ),
            ]
        );

        var (code, _) = await SupprimerAsync(portee.ServiceProvider, programme);
        Assert.Equal(StatusCodes.Status204NoContent, code);

        var (apres, _) = await LireAsync(portee.ServiceProvider, programme);
        Assert.Equal(StatusCodes.Status404NotFound, apres);

        // La cascade a emporté séances et exercices. Sans elle, des lignes
        // orphelines resteraient, invisibles et indestructibles : leur politique
        // remonte à un programme qui n'existe plus.
        Assert.Equal(0, await CompterSeancesAsync(programme));
    }

    [Fact]
    public async Task Supprimer_un_programme_D_AUTRUI_rend_404()
    {
        var a = await CompteAsync();
        var b = await CompteAsync();

        Guid deA;
        await using (var hoteA = Hote(a))
        {
            using var porteeA = hoteA.Services.CreateScope();
            deA = await CreerAsync(porteeA.ServiceProvider, "Secret de A");
        }

        await using var hoteB = Hote(b);
        using var porteeB = hoteB.Services.CreateScope();

        var (code, _) = await SupprimerAsync(porteeB.ServiceProvider, deA);
        Assert.Equal(StatusCodes.Status404NotFound, code);
    }

    // ================================================================
    // Le marquage — D75
    // ================================================================

    [Fact]
    public async Task Un_exercice_CONTRE_INDIQUE_est_MARQUE_mais_reste_dans_le_programme()
    {
        // LA RÈGLE DE FOND — D63 et D75. Le programme n'est pas amputé : il est
        // annoté. `docs/01-conformite.md` sépare informer de prescrire, et un
        // logiciel qui retirerait un mouvement à quelqu'un dont il ignore le
        // dossier prescrirait.
        var compte = await CompteAsync();
        var risque = await ExercicePublicAsync("lombaire");
        await using var hote = Hote(compte);
        using var portee = hote.Services.CreateScope();

        await DeclarerUneContrainteAsync(portee.ServiceProvider, "lombaire", "strict");

        var programme = await CreerAsync(
            portee.ServiceProvider,
            "Avec un mouvement à risque",
            [
                new EcritureDeSeance(
                    "A",
                    [new EcritureDExerciceDeSeance(risque, 3, 8, 12, 2, 90, null)]
                ),
            ]
        );

        var (_, corps) = await LireAsync(portee.ServiceProvider, programme);
        var pose = JsonDocument
            .Parse(corps)
            .RootElement.GetProperty("seances")[0]
            .GetProperty("exercices")[0];

        // Il est TOUJOURS LÀ.
        Assert.Equal(risque, pose.GetProperty("exerciceId").GetGuid());

        // Et il est marqué.
        var marquages = pose.GetProperty("marquages");
        Assert.Equal(1, marquages.GetArrayLength());
        Assert.Equal("lombaire", marquages[0].GetProperty("region").GetString());
    }

    [Fact]
    public async Task Sans_contrainte_declaree_AUCUN_marquage_ne_sort()
    {
        // Le marquage est l'INTERSECTION entre les contre-indications du
        // mouvement et ce que l'utilisateur a déclaré. Sortir les
        // contre-indications sans cette intersection alarmerait tout le monde
        // pour rien.
        var compte = await CompteAsync();
        var risque = await ExercicePublicAsync("lombaire");
        await using var hote = Hote(compte);
        using var portee = hote.Services.CreateScope();

        var programme = await CreerAsync(
            portee.ServiceProvider,
            "Sans contrainte déclarée",
            [
                new EcritureDeSeance(
                    "A",
                    [new EcritureDExerciceDeSeance(risque, 3, 8, 12, 2, 90, null)]
                ),
            ]
        );

        var (_, corps) = await LireAsync(portee.ServiceProvider, programme);
        var pose = JsonDocument
            .Parse(corps)
            .RootElement.GetProperty("seances")[0]
            .GetProperty("exercices")[0];

        Assert.Equal(0, pose.GetProperty("marquages").GetArrayLength());
    }

    // ================================================================
    // La recherche par nom — D73
    // ================================================================

    [Fact]
    public async Task La_recherche_TROUVE_sans_les_accents()
    {
        var compte = await CompteAsync();
        var cherche = await ExercicePublicAsync(nom: "Développé couché barre");
        await using var hote = Hote(compte);
        using var portee = hote.Services.CreateScope();

        var trouves = await ChercherAsync(portee.ServiceProvider, "developpe couche");

        Assert.Contains(cherche, trouves);
    }

    [Fact]
    public async Task La_recherche_IGNORE_la_casse()
    {
        var compte = await CompteAsync();
        var cherche = await ExercicePublicAsync(nom: "Rowing haltère");
        await using var hote = Hote(compte);
        using var portee = hote.Services.CreateScope();

        Assert.Contains(cherche, await ChercherAsync(portee.ServiceProvider, "ROWING"));
    }

    [Fact]
    public async Task La_recherche_ECARTE_ce_qui_ne_correspond_pas()
    {
        // Sans cette épreuve, une recherche qui ne filtrerait rien du tout
        // passerait les deux précédentes : elles ne vérifient qu'une présence.
        var compte = await CompteAsync();
        var cherche = await ExercicePublicAsync(nom: "Rowing haltère");
        await using var hote = Hote(compte);
        using var portee = hote.Services.CreateScope();

        Assert.DoesNotContain(cherche, await ChercherAsync(portee.ServiceProvider, "squat"));
    }

    [Fact]
    public async Task Un_terme_BLANC_rend_le_catalogue_entier()
    {
        var compte = await CompteAsync();
        var quelconque = await ExercicePublicAsync();
        await using var hote = Hote(compte);
        using var portee = hote.Services.CreateScope();

        Assert.Contains(quelconque, await ChercherAsync(portee.ServiceProvider, "   "));
    }

    [Fact]
    public async Task Un_terme_TROP_LONG_est_refuse_en_400()
    {
        // Un mégaoctet de terme est une entrée hostile ordinaire, et la borne
        // vaut ici comme partout ailleurs.
        var compte = await CompteAsync();
        await using var hote = Hote(compte);
        using var portee = hote.Services.CreateScope();

        var (code, corps) = await HarnaisHttp.ExecuterAsync(
            portee.ServiceProvider,
            Catalogue.ListerAsync(
                portee.ServiceProvider.GetRequiredService<IExecuteurDeCasDUsage>(),
                portee.ServiceProvider.GetRequiredService<ListerLeCatalogue>(),
                CancellationToken.None,
                new string('x', Catalogue.LongueurMaximaleDeLaRecherche + 1)
            )
        );

        Assert.Equal(StatusCodes.Status400BadRequest, code);
        Assert.Contains("RechercheTropLongue", corps, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Le_caractere_JOKER_est_cherche_LITTERALEMENT()
    {
        // `%` est le joker de `LIKE`. S'il traversait, un utilisateur qui tape
        // `%` recevrait tout le catalogue, et surtout le motif ne serait plus
        // sous le contrôle du serveur.
        var compte = await CompteAsync();
        var quelconque = await ExercicePublicAsync(nom: "Squat barre");
        await using var hote = Hote(compte);
        using var portee = hote.Services.CreateScope();

        Assert.DoesNotContain(quelconque, await ChercherAsync(portee.ServiceProvider, "%"));
    }

    // ================================================================
    // Le harnais
    // ================================================================

    private WebApplication Hote(Guid proprietaire) =>
        HarnaisHttp.Hote(
            baseDeDonnees,
            ajuster: services =>
                services.AddScoped<IIdentiteDemandeur>(_ => new DemandeurFixe(proprietaire))
        );

    private static async Task<IReadOnlyList<Guid>> ListerAsync(IServiceProvider services)
    {
        var (code, corps) = await HarnaisHttp.ExecuterAsync(
            services,
            Programmes.ListerAsync(
                services.GetRequiredService<IExecuteurDeCasDUsage>(),
                services.GetRequiredService<ListerLesProgrammes>(),
                CancellationToken.None
            )
        );

        Assert.Equal(StatusCodes.Status200OK, code);
        return
        [
            .. JsonDocument
                .Parse(corps)
                .RootElement.EnumerateArray()
                .Select(e => e.GetProperty("id").GetGuid()),
        ];
    }

    private static async Task<IReadOnlyList<Guid>> ChercherAsync(
        IServiceProvider services,
        string? terme
    )
    {
        var (code, corps) = await HarnaisHttp.ExecuterAsync(
            services,
            Catalogue.ListerAsync(
                services.GetRequiredService<IExecuteurDeCasDUsage>(),
                services.GetRequiredService<ListerLeCatalogue>(),
                CancellationToken.None,
                terme
            )
        );

        Assert.Equal(StatusCodes.Status200OK, code);
        return
        [
            .. JsonDocument
                .Parse(corps)
                .RootElement.EnumerateArray()
                .Select(e => e.GetProperty("id").GetGuid()),
        ];
    }

    private static Task<(int Code, string Corps)> LireAsync(
        IServiceProvider services,
        Guid identifiant
    ) =>
        HarnaisHttp.ExecuterAsync(
            services,
            Programmes.LireAsync(
                identifiant,
                services.GetRequiredService<IExecuteurDeCasDUsage>(),
                services.GetRequiredService<LireUnProgramme>(),
                CancellationToken.None
            )
        );

    private static async Task<Guid> CreerAsync(
        IServiceProvider services,
        string nom,
        IReadOnlyList<EcritureDeSeance>? seances = null
    )
    {
        var (code, corps) = await HarnaisHttp.ExecuterAsync(
            services,
            Programmes.CreerAsync(
                new EcritureDeProgramme(nom, null, true, seances),
                services.GetRequiredService<IExecuteurDeCasDUsage>(),
                services.GetRequiredService<CreerUnProgramme>(),
                CancellationToken.None
            )
        );

        Assert.Equal(StatusCodes.Status201Created, code);
        return JsonDocument.Parse(corps).RootElement.GetProperty("id").GetGuid();
    }

    private static Task<(int Code, string Corps)> RemplacerAsync(
        IServiceProvider services,
        Guid identifiant,
        EcritureDeProgramme ecriture
    ) =>
        HarnaisHttp.ExecuterAsync(
            services,
            Programmes.RemplacerAsync(
                identifiant,
                ecriture,
                services.GetRequiredService<IExecuteurDeCasDUsage>(),
                services.GetRequiredService<RemplacerUnProgramme>(),
                CancellationToken.None
            )
        );

    private static async Task<Guid> CopierAsync(IServiceProvider services, Guid source)
    {
        var (code, corps) = await HarnaisHttp.ExecuterAsync(
            services,
            Programmes.CopierAsync(
                source,
                services.GetRequiredService<IExecuteurDeCasDUsage>(),
                services.GetRequiredService<CopierUnProgramme>(),
                CancellationToken.None
            )
        );

        Assert.Equal(StatusCodes.Status201Created, code);
        return JsonDocument.Parse(corps).RootElement.GetProperty("id").GetGuid();
    }

    private static Task<(int Code, string Corps)> SupprimerAsync(
        IServiceProvider services,
        Guid identifiant
    ) =>
        HarnaisHttp.ExecuterAsync(
            services,
            Programmes.SupprimerAsync(
                identifiant,
                services.GetRequiredService<IExecuteurDeCasDUsage>(),
                services.GetRequiredService<SupprimerUnProgramme>(),
                CancellationToken.None
            )
        );

    private static async Task<Guid> ExercicePersonnelAsync(IServiceProvider services)
    {
        var (code, corps) = await HarnaisHttp.ExecuterAsync(
            services,
            Catalogue.CreerAsync(
                new CreationDExercice("Le mien", null, ["pectoraux"], [], false, 2.5m, []),
                services.GetRequiredService<IExecuteurDeCasDUsage>(),
                services.GetRequiredService<CreerUnExercice>(),
                CancellationToken.None
            )
        );

        Assert.Equal(StatusCodes.Status201Created, code);
        return JsonDocument.Parse(corps).RootElement.GetProperty("id").GetGuid();
    }

    private static async Task DeclarerUneContrainteAsync(
        IServiceProvider services,
        string region,
        string severite
    )
    {
        var (code, _) = await HarnaisHttp.ExecuterAsync(
            services,
            ContraintesDuCompte.RemplacerAsync(
                new DeclarationDeContraintes([new DeclarationDUneContrainte(region, severite, null)]),
                services.GetRequiredService<IExecuteurDeCasDUsage>(),
                services.GetRequiredService<RemplacerLesContraintes>(),
                TimeProvider.System,
                CancellationToken.None
            )
        );

        Assert.Equal(StatusCodes.Status200OK, code);
    }

    /// <summary>
    /// Un exercice du catalogue public, éventuellement contre-indiqué.
    /// </summary>
    private async Task<Guid> ExercicePublicAsync(string? contreIndiquePour = null, string? nom = null)
    {
        await using var connexion = new NpgsqlConnection(baseDeDonnees.ChaineMigrations);
        await connexion.OpenAsync();
        await using var commande = new NpgsqlCommand(
            """
            insert into public.exercises (slug, name_fr, name_en, instructions_fr, instructions_en,
                                          common_errors_fr, common_errors_en, movement_role,
                                          primary_muscles, contraindicated_for, is_custom, owner_id)
            values ($1, $2, $2, 'Consignes.', 'Cues.', 'Erreurs.', 'Errors.',
                    'poussee', array['pectoraux'], $3, false, null)
            returning id
            """,
            connexion
        );

        var suffixe = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);
        commande.Parameters.AddWithValue($"programmes-{suffixe}");
        commande.Parameters.AddWithValue(nom ?? $"Mouvement {suffixe}");
        commande.Parameters.AddWithValue(
            contreIndiquePour is null ? Array.Empty<string>() : new[] { contreIndiquePour }
        );

        return (Guid)(await commande.ExecuteScalarAsync())!;
    }

    /// <summary>
    /// Un MODÈLE du catalogue, avec une séance et sa note.
    /// </summary>
    /// <remarks>
    /// Écrit sous <c>palier_migrations</c>, comme le fera
    /// <c>04-programs.sql</c> : sous <c>palier_app</c>,
    /// <c>ck_programs_proprietaire</c> et la politique <c>proprietaire</c>
    /// refuseraient tous deux un programme sans propriétaire.
    /// </remarks>
    private async Task<Guid> ModeleAsync(Guid? exercice = null)
    {
        await using var connexion = new NpgsqlConnection(baseDeDonnees.ChaineMigrations);
        await connexion.OpenAsync();

        var suffixe = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);

        await using var poser = new NpgsqlCommand(
            """
            insert into public.programs
              (slug, is_template, owner_id, name_fr, name_en, description_fr, description_en,
               notes_fr, notes_en, frequency_min, frequency_max)
            values ($1, true, null, 'Modèle', 'Template', 'Pour éprouver.', 'For testing.',
                    'Pourquoi ces choix.', 'Why these choices.', 3, 3)
            returning id
            """,
            connexion
        );
        poser.Parameters.AddWithValue($"modele-{suffixe}");
        var programme = (Guid)(await poser.ExecuteScalarAsync())!;

        await using var jour = new NpgsqlCommand(
            "insert into public.program_days (program_id, label_fr, label_en, position) "
                + "values ($1, 'Séance A', 'Session A', 1) returning id",
            connexion
        );
        jour.Parameters.AddWithValue(programme);
        var seance = (Guid)(await jour.ExecuteScalarAsync())!;

        if (exercice is { } pose)
        {
            await using var poserLExercice = new NpgsqlCommand(
                """
                insert into public.program_exercises
                  (program_day_id, exercise_id, position, target_sets,
                   target_reps_min, target_reps_max, target_rir, rest_seconds)
                values ($1, $2, 1, 3, 8, 12, 2, 90)
                """,
                connexion
            );
            poserLExercice.Parameters.AddWithValue(seance);
            poserLExercice.Parameters.AddWithValue(pose);
            await poserLExercice.ExecuteNonQueryAsync();
        }

        return programme;
    }

    private async Task<long> CompterSeancesAsync(Guid programme)
    {
        await using var connexion = new NpgsqlConnection(baseDeDonnees.ChaineMigrations);
        await connexion.OpenAsync();
        await using var commande = new NpgsqlCommand(
            "select count(*) from public.program_days where program_id = $1",
            connexion
        );
        commande.Parameters.AddWithValue(programme);
        return (long)(await commande.ExecuteScalarAsync())!;
    }

    private async Task<Guid> CompteAsync()
    {
        await using var hote = HarnaisHttp.Hote(baseDeDonnees);
        using var portee = hote.Services.CreateScope();
        var utilisateurs = portee.ServiceProvider.GetRequiredService<UserManager<Utilisateur>>();

        var email =
            "programmes-"
            + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture)
            + "@exemple.test";
        var compte = new Utilisateur
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
        };

        var resultat = await utilisateurs.CreateAsync(compte, _motDePasse);
        Assert.True(
            resultat.Succeeded,
            "le harnais n'a pas pu créer l'utilisateur : "
                + string.Join(", ", resultat.Errors.Select(e => e.Code))
        );

        return compte.Id;
    }
}
