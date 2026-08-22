using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Palier.Api;
using Palier.Api.Auth;
using Palier.Infrastructure.Identite;

namespace Palier.Database.Tests;

/// <summary>
/// L'inscription et la connexion, sur le moteur réel et par la composition
/// RÉELLE de l'API.
/// </summary>
/// <remarks>
/// <para>
/// <b>Ce que ces épreuves refusent avant tout : l'énumération de comptes.</b>
/// Une réponse qui distingue « cette adresse est inconnue » de « ce mot de
/// passe est faux » transforme le formulaire de connexion en annuaire. Sur un
/// produit de santé, l'annuaire lui-même est la donnée sensible : savoir que
/// quelqu'un a un compte ici, c'est savoir qu'il suit un entraînement.
/// </para>
///
/// <para>
/// Les services viennent de <see cref="Composition.Composer" />, jamais d'un
/// conteneur remonté pour l'occasion — un conteneur d'épreuve éprouverait la
/// copie, et c'est le défaut que ce dépôt ferme partout ailleurs.
/// </para>
/// </remarks>
[Collection(BaseFixture.Collection)]
public sealed class PointsDEntreeTests(BaseFixture baseDeDonnees)
{
    private const string _motDePasseSolide = "brouette-hivernale-38-oscille";

    // ================================================================
    // Inscription
    // ================================================================

    [Fact]
    public async Task Une_inscription_valide_cree_le_compte()
    {
        await using var hote = Hote();
        using var portee = hote.Services.CreateScope();
        var utilisateurs = portee.ServiceProvider.GetRequiredService<UserManager<Utilisateur>>();
        var email = EmailNeuf();

        var (code, corps) = await HarnaisHttp.ExecuterAsync(portee.ServiceProvider,
            PointsDEntree.InscrireAsync(
                new DemandeDIdentifiants(email, _motDePasseSolide),
                utilisateurs,
                CancellationToken.None
            )
        );

        Assert.Equal(StatusCodes.Status202Accepted, code);
        Assert.Equal("InscriptionEnregistree", HarnaisHttp.Code(corps));
        Assert.NotNull(await utilisateurs.FindByEmailAsync(email));
    }

    [Fact]
    public async Task Un_email_DEJA_PRIS_rend_EXACTEMENT_la_meme_reponse()
    {
        // Le cœur de l'épreuve : octet pour octet, la même réponse. Un code
        // distinct, un statut distinct, ou même un champ en plus, et l'adresse
        // devient interrogeable.
        await using var hote = Hote();
        using var portee = hote.Services.CreateScope();
        var utilisateurs = portee.ServiceProvider.GetRequiredService<UserManager<Utilisateur>>();
        var email = EmailNeuf();

        var premiere = await HarnaisHttp.ExecuterAsync(portee.ServiceProvider,
            PointsDEntree.InscrireAsync(
                new DemandeDIdentifiants(email, _motDePasseSolide),
                utilisateurs,
                CancellationToken.None
            )
        );
        var seconde = await HarnaisHttp.ExecuterAsync(portee.ServiceProvider,
            PointsDEntree.InscrireAsync(
                new DemandeDIdentifiants(email, _motDePasseSolide),
                utilisateurs,
                CancellationToken.None
            )
        );

        Assert.Equal(premiere.Code, seconde.Code);
        Assert.Equal(premiere.Corps, seconde.Corps);
    }

    [Fact]
    public async Task Le_MOT_DE_PASSE_est_juge_AVANT_l_unicite_de_l_adresse()
    {
        // Ce n'est pas un détail d'ordre : c'est ce qui égalise le TEMPS de
        // réponse. Identity valide et hache le mot de passe avant de contrôler
        // l'unicité ; une inscription sur une adresse déjà prise paie donc le
        // même PBKDF2 à 210 000 itérations qu'une inscription neuve.
        //
        // L'épreuve est d'ORDRE, pas de chronomètre : une mesure de durée
        // rougirait au hasard sur une machine chargée, et un test qui rougit au
        // hasard finit désactivé.
        await using var hote = Hote();
        using var portee = hote.Services.CreateScope();
        var utilisateurs = portee.ServiceProvider.GetRequiredService<UserManager<Utilisateur>>();
        var email = EmailNeuf();

        await HarnaisHttp.ExecuterAsync(portee.ServiceProvider,
            PointsDEntree.InscrireAsync(
                new DemandeDIdentifiants(email, _motDePasseSolide),
                utilisateurs,
                CancellationToken.None
            )
        );

        // Adresse DÉJÀ PRISE et mot de passe trop court : si l'unicité passait
        // en premier, on obtiendrait la réponse générique.
        var (code, corps) = await HarnaisHttp.ExecuterAsync(portee.ServiceProvider,
            PointsDEntree.InscrireAsync(
                new DemandeDIdentifiants(email, "court"),
                utilisateurs,
                CancellationToken.None
            )
        );

        Assert.Equal(StatusCodes.Status400BadRequest, code);
        Assert.Equal("PasswordTooShort", HarnaisHttp.Code(corps));
    }

    [Fact]
    public async Task Un_mot_de_passe_COMPROMIS_est_refuse_et_DIT_pourquoi()
    {
        // Contrepartie de l'énumération : ce qui porte sur l'entrée de
        // l'utilisateur lui est rendu. Taire la raison d'un mot de passe refusé
        // ne protège personne et laisse l'utilisateur devant un formulaire muet.
        await using var hote = Hote();
        using var portee = hote.Services.CreateScope();

        var (code, corps) = await HarnaisHttp.ExecuterAsync(portee.ServiceProvider,
            PointsDEntree.InscrireAsync(
                new DemandeDIdentifiants(EmailNeuf(), "azertyuiop"),
                portee.ServiceProvider.GetRequiredService<UserManager<Utilisateur>>(),
                CancellationToken.None
            )
        );

        Assert.Equal(StatusCodes.Status400BadRequest, code);
        Assert.Equal("MotDePasseCompromis", HarnaisHttp.Code(corps));
    }

    [Fact]
    public async Task NEUF_signes_ne_suffisent_pas()
    {
        await using var hote = Hote();
        using var portee = hote.Services.CreateScope();

        var (code, corps) = await HarnaisHttp.ExecuterAsync(
            portee.ServiceProvider,
            PointsDEntree.InscrireAsync(
                new DemandeDIdentifiants(EmailNeuf(), "brouette9"),
                portee.ServiceProvider.GetRequiredService<UserManager<Utilisateur>>(),
                CancellationToken.None
            )
        );

        Assert.Equal(StatusCodes.Status400BadRequest, code);
        Assert.Equal("PasswordTooShort", HarnaisHttp.Code(corps));
    }

    [Fact]
    public async Task Une_phrase_SANS_MAJUSCULE_NI_CHIFFRE_est_acceptee()
    {
        // Le mordant de la décision qui retire les règles de composition. NIST
        // SP 800-63B rév. 4 : « Verifiers and CSPs SHALL NOT impose other
        // composition rules ». Sans cette épreuve, remettre les défauts
        // d'Identity ne ferait rougir personne — et une phrase de passe longue,
        // qui est précisément ce qu'on veut encourager, serait refusée.
        await using var hote = Hote();
        using var portee = hote.Services.CreateScope();

        var (code, corps) = await HarnaisHttp.ExecuterAsync(
            portee.ServiceProvider,
            PointsDEntree.InscrireAsync(
                new DemandeDIdentifiants(EmailNeuf(), "grenouille verte sur le toit"),
                portee.ServiceProvider.GetRequiredService<UserManager<Utilisateur>>(),
                CancellationToken.None
            )
        );

        Assert.Equal(StatusCodes.Status202Accepted, code);
        Assert.Equal("InscriptionEnregistree", HarnaisHttp.Code(corps));
    }

    [Fact]
    public async Task DEUX_comptes_ne_peuvent_pas_partager_une_ADRESSE()
    {
        // Le contrôle passe SOUS les points d'entrée, au niveau du magasin :
        // c'est là que l'invariant doit tenir, pas seulement là où nos routes
        // se trouvent poser le nom d'utilisateur égal à l'adresse.
        //
        // `FindByEmailAsync` — dont dépend toute la connexion — n'a de sens que
        // si l'adresse identifie UN compte.
        await using var hote = Hote();
        using var portee = hote.Services.CreateScope();
        var utilisateurs = portee.ServiceProvider.GetRequiredService<UserManager<Utilisateur>>();
        var email = EmailNeuf();

        var premier = await utilisateurs.CreateAsync(
            new Utilisateur { UserName = "premier-" + email, Email = email },
            _motDePasseSolide
        );
        Assert.True(premier.Succeeded);

        var second = await utilisateurs.CreateAsync(
            new Utilisateur { UserName = "second-" + email, Email = email },
            _motDePasseSolide
        );

        Assert.False(
            second.Succeeded,
            "deux comptes portent la même adresse : `FindByEmailAsync` en choisira un "
                + "au hasard, et la connexion devient non déterministe."
        );
        Assert.Contains(second.Errors, e => e.Code == "DuplicateEmail");
    }

    // ================================================================
    // Connexion
    // ================================================================

    [Fact]
    public async Task Une_connexion_valide_rend_un_JETON_et_POSE_le_cookie()
    {
        await using var hote = Hote();
        using var portee = hote.Services.CreateScope();
        var email = await InscritAsync(portee.ServiceProvider);
        var contexte = HarnaisHttp.Contexte(portee.ServiceProvider);

        var (code, corps) = await HarnaisHttp.ExecuterAsync(portee.ServiceProvider,
            Connexion(portee.ServiceProvider, contexte, email, _motDePasseSolide),
            contexte
        );

        Assert.Equal(StatusCodes.Status200OK, code);

        var jeton = JsonDocument.Parse(corps).RootElement.GetProperty("jetonDAcces").GetString();
        Assert.False(string.IsNullOrWhiteSpace(jeton));

        var cookie = Assert.Single(contexte.Response.Headers.SetCookie!);
        Assert.NotNull(cookie);
        Assert.StartsWith(CookieDeRafraichissement.Nom + "=", cookie, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Le_cookie_est_HttpOnly_Secure_Strict_et_de_CHEMIN_RESTREINT()
    {
        // On lit l'EN-TÊTE RÉELLEMENT ÉMIS, pas l'objet CookieOptions qui l'a
        // produit. Un drapeau posé mais perdu à la sérialisation se lirait comme
        // une protection.
        await using var hote = Hote();
        using var portee = hote.Services.CreateScope();
        var email = await InscritAsync(portee.ServiceProvider);
        var contexte = HarnaisHttp.Contexte(portee.ServiceProvider);

        await HarnaisHttp.ExecuterAsync(portee.ServiceProvider,
            Connexion(portee.ServiceProvider, contexte, email, _motDePasseSolide),
            contexte
        );

        var cookie = Assert.Single(contexte.Response.Headers.SetCookie!);

        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            "path=" + CookieDeRafraichissement.Chemin,
            cookie,
            StringComparison.OrdinalIgnoreCase
        );
    }

    [Fact]
    public async Task Le_jeton_de_rafraichissement_n_est_PAS_dans_le_corps()
    {
        // S'il y était, un script de la page pourrait le lire — et `HttpOnly`
        // sur le cookie ne servirait plus à rien.
        await using var hote = Hote();
        using var portee = hote.Services.CreateScope();
        var email = await InscritAsync(portee.ServiceProvider);
        var contexte = HarnaisHttp.Contexte(portee.ServiceProvider);

        var (_, corps) = await HarnaisHttp.ExecuterAsync(portee.ServiceProvider,
            Connexion(portee.ServiceProvider, contexte, email, _motDePasseSolide),
            contexte
        );

        var cookie = Assert.Single(contexte.Response.Headers.SetCookie!);
        Assert.NotNull(cookie);
        var valeur = cookie[(cookie.IndexOf('=', StringComparison.Ordinal) + 1)..].Split(';')[0];

        Assert.False(string.IsNullOrWhiteSpace(valeur));
        Assert.DoesNotContain(valeur, corps, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Un_email_INCONNU_et_un_MAUVAIS_mot_de_passe_rendent_la_MEME_reponse()
    {
        // L'épreuve qui compte le plus de tout ce fichier.
        await using var hote = Hote();
        using var portee = hote.Services.CreateScope();
        var email = await InscritAsync(portee.ServiceProvider);

        var contexteInconnu = HarnaisHttp.Contexte(portee.ServiceProvider);
        var inconnu = await HarnaisHttp.ExecuterAsync(portee.ServiceProvider,
            Connexion(portee.ServiceProvider, contexteInconnu, EmailNeuf(), _motDePasseSolide),
            contexteInconnu
        );

        var contexteMauvais = HarnaisHttp.Contexte(portee.ServiceProvider);
        var mauvais = await HarnaisHttp.ExecuterAsync(portee.ServiceProvider,
            Connexion(
                portee.ServiceProvider,
                contexteMauvais,
                email,
                "un-tout-autre-mot-de-passe-42"
            ),
            contexteMauvais
        );

        Assert.Equal(StatusCodes.Status401Unauthorized, inconnu.Code);
        Assert.Equal(inconnu.Code, mauvais.Code);
        Assert.Equal(inconnu.Corps, mauvais.Corps);
        Assert.Equal("IdentifiantsInvalides", HarnaisHttp.Code(inconnu.Corps));

        // Et AUCUN cookie dans les deux cas : un Set-Cookie émis sur un échec
        // distinguerait les deux chemins aussi sûrement qu'un message.
        Assert.Equal(0, contexteInconnu.Response.Headers.SetCookie.Count);
        Assert.Equal(0, contexteMauvais.Response.Headers.SetCookie.Count);
    }

    [Fact]
    public async Task Un_email_INCONNU_paie_QUAND_MEME_le_hachage()
    {
        // Sans cela, l'égalité des messages ne sert à rien : la réponse pour une
        // adresse inconnue reviendrait 100 ms plus tôt, et le canal temporel
        // rendrait l'annuaire de nouveau interrogeable.
        //
        // Épreuve d'EFFET, pas de chronomètre : on compte les vérifications
        // faites par le hacheur.
        await using var hote = Hote();
        using var portee = hote.Services.CreateScope();
        var contexte = HarnaisHttp.Contexte(portee.ServiceProvider);
        var compteur = new HacheurComptant(
            portee.ServiceProvider.GetRequiredService<IPasswordHasher<Utilisateur>>()
        );

        await HarnaisHttp.ExecuterAsync(portee.ServiceProvider,
            PointsDEntree.ConnecterAsync(
                new DemandeDIdentifiants(EmailNeuf(), _motDePasseSolide),
                portee.ServiceProvider.GetRequiredService<UserManager<Utilisateur>>(),
                portee.ServiceProvider.GetRequiredService<MagasinDeSessions>(),
                portee.ServiceProvider.GetRequiredService<SignataireDeJetons>(),
                portee.ServiceProvider.GetRequiredService<GardienDeVerrouillage>(),
                compteur,
                TimeProvider.System,
                contexte,
                CancellationToken.None
            ),
            contexte
        );

        Assert.True(
            compteur.OperationsCouteuses > 0,
            "aucun PBKDF2 n'a eu lieu pour une adresse inconnue : la réponse revient plus "
                + "tôt que pour une adresse connue, et le temps révèle ce que le message tait."
        );
    }

    [Fact]
    public async Task Une_adresse_INCONNUE_et_un_MAUVAIS_mot_de_passe_mettent_le_MEME_TEMPS()
    {
        // L'égalité des messages ne suffit pas : le temps parle aussi. Une
        // adresse connue dont le mot de passe est faux paie en plus
        // l'enregistrement de l'échec — une transaction avec verrou de ligne
        // depuis que le compteur est sérialisé. Chronométrer séparait les deux
        // populations et rendait l'annuaire interrogeable malgré des corps
        // identiques à l'octet près.
        //
        // Cette épreuve MESURE, elle ne relit pas : un canal temporel ne se
        // ferme pas par lecture de code.
        await using var hote = Hote();
        using var portee = hote.Services.CreateScope();
        var connue = await InscritAsync(portee.ServiceProvider);

        // Un tour à blanc : la première connexion paie la mise en route d'EF et
        // du pool, et fausserait la comparaison.
        await MesurerAsync(portee.ServiceProvider, connue, "un-tout-autre-mot-de-passe-42");

        var inconnue = new List<double>();
        var mauvaise = new List<double>();
        for (var i = 0; i < 3; i++)
        {
            inconnue.Add(
                await MesurerAsync(portee.ServiceProvider, EmailNeuf(), _motDePasseSolide)
            );
            mauvaise.Add(
                await MesurerAsync(portee.ServiceProvider, connue, "un-tout-autre-mot-de-passe-42")
            );
        }

        var ecart = Math.Abs(inconnue.Average() - mauvaise.Average());

        // LE SEUIL EST LITTÉRAL, et l'assertion qui suit dit pourquoi.
        //
        // Le dériver du budget rendrait cette épreuve vraie par construction :
        // réduire le budget réduirait la tolérance dans la même proportion, et
        // l'épreuve rougirait pour la mauvaise raison. C'est exactement le
        // défaut trouvé sur la fenêtre de verrouillage du lot 4 — une épreuve
        // dont l'entrée dérive du réglage qu'elle éprouve ne mord jamais où il
        // faut.
        const double toleranceEnMs = 30;

        Assert.True(
            PointsDEntree.BudgetDeRefus.TotalMilliseconds > toleranceEnMs * 3,
            $"le budget vaut {PointsDEntree.BudgetDeRefus.TotalMilliseconds:F0} ms pour une "
                + $"tolérance de {toleranceEnMs:F0} ms : il n'a plus de marge pour masquer quoi "
                + "que ce soit, et cette épreuve ne prouve plus rien."
        );

        // Mesuré sans égalisation, sur cette machine : 164 ms pour une adresse
        // inconnue contre 159 ms pour un mot de passe faux — cinq millisecondes
        // d'écart sur cent soixante. Petit, mais stable et moyennable, donc
        // exploitable par qui sonde en masse.
        Assert.True(
            ecart < toleranceEnMs,
            $"adresse inconnue {inconnue.Average():F0} ms contre mauvais mot de passe "
                + $"{mauvaise.Average():F0} ms — écart de {ecart:F0} ms. Le temps de réponse "
                + "distingue une adresse qui a un compte d'une adresse qui n'en a pas."
        );

        // ET LE BUDGET EST TENU SUR LES DEUX BRANCHES. C'est cette assertion
        // qui porte l'épreuve, pas celle de l'écart.
        //
        // Mesuré : sans égalisation, l'écart naturel n'est que de cinq
        // millisecondes — déjà sous la tolérance ci-dessus. Une épreuve qui ne
        // regarderait que l'écart resterait donc VERTE sans égalisation, et ne
        // garderait rien. Ce qui distingue « égalisé » de « non égalisé », c'est
        // que les deux branches reviennent au budget et non à leur coût propre.
        foreach (var (nom, mesures) in new[] { ("inconnue", inconnue), ("mauvaise", mauvaise) })
        {
            Assert.True(
                mesures.Average() >= PointsDEntree.BudgetDeRefus.TotalMilliseconds * 0.9,
                $"la branche « {nom} » revient en {mesures.Average():F0} ms, sous le budget de "
                    + $"{PointsDEntree.BudgetDeRefus.TotalMilliseconds:F0} ms : l'égalisation "
                    + "n'est pas appliquée, et chaque branche paie son coût propre — donc le "
                    + "temps la trahit."
            );
        }
    }

    /// <summary>Le temps qu'une connexion refusée met à revenir, en millisecondes.</summary>
    private static async Task<double> MesurerAsync(
        IServiceProvider services,
        string email,
        string motDePasse
    )
    {
        using var portee = services.GetRequiredService<IServiceScopeFactory>().CreateScope();
        var contexte = HarnaisHttp.Contexte(portee.ServiceProvider);

        var depart = System.Diagnostics.Stopwatch.GetTimestamp();
        var (code, _) = await HarnaisHttp.ExecuterAsync(
            portee.ServiceProvider,
            PointsDEntree.ConnecterAsync(
                new DemandeDIdentifiants(email, motDePasse),
                portee.ServiceProvider.GetRequiredService<UserManager<Utilisateur>>(),
                portee.ServiceProvider.GetRequiredService<MagasinDeSessions>(),
                portee.ServiceProvider.GetRequiredService<SignataireDeJetons>(),
                portee.ServiceProvider.GetRequiredService<GardienDeVerrouillage>(),
                portee.ServiceProvider.GetRequiredService<IPasswordHasher<Utilisateur>>(),
                TimeProvider.System,
                contexte,
                CancellationToken.None
            ),
            contexte
        );

        Assert.Equal(StatusCodes.Status401Unauthorized, code);
        return System.Diagnostics.Stopwatch.GetElapsedTime(depart).TotalMilliseconds;
    }

    // ================================================================
    // Le harnais
    // ================================================================

    /// <summary>
    /// Un hacheur qui délègue tout et compte les opérations COÛTEUSES.
    /// </summary>
    /// <remarks>
    /// Hacher et vérifier font le même PBKDF2 — même fonction, mêmes 210 000
    /// itérations, même coût. Compter les deux sous un seul nombre est donc
    /// exact : ce qu'on mesure est « un PBKDF2 complet a-t-il eu lieu », et
    /// c'est cela seul qui égalise le temps de réponse.
    /// </remarks>
    private sealed class HacheurComptant(IPasswordHasher<Utilisateur> reel)
        : IPasswordHasher<Utilisateur>
    {
        public int OperationsCouteuses { get; private set; }

        public string HashPassword(Utilisateur user, string password)
        {
            OperationsCouteuses++;
            return reel.HashPassword(user, password);
        }

        public PasswordVerificationResult VerifyHashedPassword(
            Utilisateur user,
            string hashedPassword,
            string providedPassword
        )
        {
            OperationsCouteuses++;
            return reel.VerifyHashedPassword(user, hashedPassword, providedPassword);
        }
    }

    private static Task<IResult> Connexion(
        IServiceProvider services,
        HttpContext contexte,
        string email,
        string motDePasse
    ) =>
        PointsDEntree.ConnecterAsync(
            new DemandeDIdentifiants(email, motDePasse),
            services.GetRequiredService<UserManager<Utilisateur>>(),
            services.GetRequiredService<MagasinDeSessions>(),
            services.GetRequiredService<SignataireDeJetons>(),
            services.GetRequiredService<GardienDeVerrouillage>(),
            services.GetRequiredService<IPasswordHasher<Utilisateur>>(),
            TimeProvider.System,
            contexte,
            CancellationToken.None
        );

    private static async Task<string> InscritAsync(IServiceProvider services)
    {
        var email = EmailNeuf();
        var utilisateurs = services.GetRequiredService<UserManager<Utilisateur>>();
        var resultat = await utilisateurs.CreateAsync(
            new Utilisateur { UserName = email, Email = email },
            _motDePasseSolide
        );

        Assert.True(
            resultat.Succeeded,
            "le harnais n'a pas pu créer l'utilisateur : "
                + string.Join(", ", resultat.Errors.Select(e => e.Code))
        );
        return email;
    }

    private static string EmailNeuf() =>
        "epreuve-"
        + Guid.NewGuid().ToString("N", System.Globalization.CultureInfo.InvariantCulture)
        + "@exemple.test";

    private WebApplication Hote() => HarnaisHttp.Hote(baseDeDonnees);
}
