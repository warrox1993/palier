namespace Palier.Api.Auth;

/// <summary>Ce que Google dit de la personne qui revient.</summary>
/// <remarks>
/// <b>C'est une entrée hostile</b>, au sens de <c>CLAUDE.md</c> § 4 : « toute
/// donnée qui vient de l'extérieur est hostile jusqu'à preuve du contraire », et
/// « la réponse d'une API tierce » y figure nommément. Les trois champs sont
/// donc nullables, et <see cref="DecisionDeGoogle" /> refuse ce qui manque.
/// </remarks>
internal sealed record ConstatDeGoogle(string? Sujet, string? Email, bool EmailVerifie);

/// <summary>Ce que le produit fait du retour.</summary>
internal enum SuiteDeGoogle
{
    /// <summary>Rien ne permet de continuer.</summary>
    Refuser,

    /// <summary>La liaison existe déjà : on ouvre la session.</summary>
    Connecter,

    /// <summary>Personne ne porte cette adresse : on crée le compte.</summary>
    Creer,

    /// <summary>Un compte existe : on PROPOSE la liaison, on ne la fait pas.</summary>
    ProposerLaLiaison,
}

/// <summary>
/// La décision au retour de Google — exigence 1 de <c>docs/09-comptes.md</c>
/// § 1, et l'amorce de l'exigence 7.
/// </summary>
/// <remarks>
/// <para>
/// <b>Elle est pure, et c'est délibéré.</b> Le va-et-vient OAuth est du câblage
/// de framework ; ce qui peut donner un compte à quelqu'un qui n'y a pas droit
/// tient dans ces quinze lignes, et elles s'éprouvent sans réseau.
/// </para>
///
/// <para>
/// <b>La liaison ne se fait JAMAIS automatiquement.</b> Lier sur la seule
/// égalité des adresses est une prise de contrôle de compte : quiconque crée un
/// compte Google portant l'adresse d'un utilisateur entrerait chez lui. Le
/// produit propose, et la preuve de possession se donne ailleurs.
/// </para>
/// </remarks>
internal static class DecisionDeGoogle
{
    public static SuiteDeGoogle Trancher(
        ConstatDeGoogle constat,
        bool connexionExiste,
        bool compteEmailExiste
    )
    {
        ArgumentNullException.ThrowIfNull(constat);

        // Le constat incomplet est jugé EN PREMIER, avant même la connexion
        // existante. Un jeton sans sujet ni adresse ne doit rien ouvrir, et le
        // court-circuit par le premier cas serait exactement le défaut qu'une
        // épreuve dédiée garde.
        if (constat.Sujet is not { Length: > 0 } || constat.Email is not { Length: > 0 })
        {
            return SuiteDeGoogle.Refuser;
        }

        // La liaison a déjà été établie ET prouvée. On n'exige pas
        // `email_verified` ici : refuser déconnecterait un utilisateur légitime
        // parce que son administrateur Workspace a changé un réglage. La
        // vérification protège la CRÉATION du lien, pas son usage.
        if (connexionExiste)
        {
            return SuiteDeGoogle.Connecter;
        }

        // `email_verified` peut valoir false sur un compte Workspace mal
        // configuré — ce n'est pas théorique. Sans cette garde, on créerait un
        // compte sur une adresse préemptée, ou on amorcerait la prise d'un
        // compte existant.
        if (!constat.EmailVerifie)
        {
            return SuiteDeGoogle.Refuser;
        }

        return compteEmailExiste ? SuiteDeGoogle.ProposerLaLiaison : SuiteDeGoogle.Creer;
    }
}
