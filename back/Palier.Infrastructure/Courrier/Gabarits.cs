namespace Palier.Infrastructure.Courrier;

/// <summary>Les courriels que ce lot sait envoyer.</summary>
public enum Courriel
{
    /// <summary>Vérification d'adresse — à l'inscription.</summary>
    Verification,

    /// <summary>Bienvenue — une fois l'adresse vérifiée.</summary>
    Bienvenue,

    /// <summary>Réinitialisation de mot de passe — sur demande.</summary>
    Reinitialisation,

    /// <summary>Confirmation de suppression de compte — sur demande.</summary>
    Suppression,
}

/// <summary>Un gabarit rendu : sujet, corps texte, corps HTML.</summary>
public sealed record Gabarit(string Sujet, string Texte, string Html);

/// <summary>
/// Les gabarits, en français et en anglais.
/// </summary>
/// <remarks>
/// <para>
/// <b>Ton sobre, aucune relance culpabilisante</b> — <c>docs/14-contenu.md</c>
/// § 4. Les cinq courriels d'abonnement et d'export n'y sont pas : ils
/// dépendent de fonctions qui n'existent pas encore, et les écrire ici
/// produirait des gabarits que rien n'envoie.
/// </para>
///
/// <para>
/// <b>Une langue inconnue retombe sur le français</b> plutôt que de lever : le
/// produit est français d'abord, et une préférence mal renseignée ne doit pas
/// empêcher un courriel de partir.
/// </para>
///
/// <para>
/// <b>Le <c>{0}</c> est le lien ou le code</b>, posé par l'envoyeur. Les
/// gabarits ne composent aucune URL : la base des liens vient de la
/// configuration, et le chemin de l'appelant.
/// </para>
/// </remarks>
public static class Gabarits
{
    private const string _marque = "Palier";

    public static Gabarit Pour(Courriel courriel, string? langue)
    {
        var anglais =
            langue is not null && langue.StartsWith("en", StringComparison.OrdinalIgnoreCase);

        return (courriel, anglais) switch
        {
            (Courriel.Verification, false) => new(
                _marque + " — confirmez votre adresse",
                "Bonjour,\n\nPour activer votre compte, ouvrez ce lien :\n{0}\n\n"
                    + "Si vous n'êtes pas à l'origine de cette inscription, ignorez ce "
                    + "message.\n\n"
                    + _marque,
                "<p>Bonjour,</p><p>Pour activer votre compte, ouvrez ce lien :<br>"
                    + "<a href=\"{0}\">{0}</a></p><p>Si vous n'êtes pas à l'origine de cette "
                    + "inscription, ignorez ce message.</p><p>"
                    + _marque
                    + "</p>"
            ),
            (Courriel.Verification, true) => new(
                _marque + " — confirm your address",
                "Hello,\n\nTo activate your account, open this link:\n{0}\n\n"
                    + "If you did not sign up, ignore this message.\n\n"
                    + _marque,
                "<p>Hello,</p><p>To activate your account, open this link:<br>"
                    + "<a href=\"{0}\">{0}</a></p><p>If you did not sign up, ignore this "
                    + "message.</p><p>"
                    + _marque
                    + "</p>"
            ),
            (Courriel.Bienvenue, false) => new(
                _marque + " — votre compte est actif",
                "Bonjour,\n\nVotre adresse est confirmée. Vous pouvez maintenant suivre vos "
                    + "séances et vos apports.\n\n{0}\n\n"
                    + _marque,
                "<p>Bonjour,</p><p>Votre adresse est confirmée. Vous pouvez maintenant suivre "
                    + "vos séances et vos apports.</p><p><a href=\"{0}\">{0}</a></p><p>"
                    + _marque
                    + "</p>"
            ),
            (Courriel.Bienvenue, true) => new(
                _marque + " — your account is active",
                "Hello,\n\nYour address is confirmed. You can now track your sessions and "
                    + "your intake.\n\n{0}\n\n"
                    + _marque,
                "<p>Hello,</p><p>Your address is confirmed. You can now track your sessions "
                    + "and your intake.</p><p><a href=\"{0}\">{0}</a></p><p>"
                    + _marque
                    + "</p>"
            ),
            (Courriel.Reinitialisation, false) => new(
                _marque + " — réinitialiser votre mot de passe",
                "Bonjour,\n\nPour choisir un nouveau mot de passe, ouvrez ce lien :\n{0}\n\n"
                    + "Il expire dans une heure. Si vous n'avez rien demandé, ignorez ce "
                    + "message : votre mot de passe actuel reste valable.\n\n"
                    + _marque,
                "<p>Bonjour,</p><p>Pour choisir un nouveau mot de passe, ouvrez ce lien :<br>"
                    + "<a href=\"{0}\">{0}</a></p><p>Il expire dans une heure. Si vous n'avez "
                    + "rien demandé, ignorez ce message : votre mot de passe actuel reste "
                    + "valable.</p><p>"
                    + _marque
                    + "</p>"
            ),
            (Courriel.Reinitialisation, true) => new(
                _marque + " — reset your password",
                "Hello,\n\nTo choose a new password, open this link:\n{0}\n\nIt expires in "
                    + "one hour. If you did not ask for this, ignore this message: your "
                    + "current password stays valid.\n\n"
                    + _marque,
                "<p>Hello,</p><p>To choose a new password, open this link:<br>"
                    + "<a href=\"{0}\">{0}</a></p><p>It expires in one hour. If you did not ask "
                    + "for this, ignore this message: your current password stays valid.</p><p>"
                    + _marque
                    + "</p>"
            ),
            (Courriel.Suppression, false) => new(
                _marque + " — confirmer la suppression de votre compte",
                "Bonjour,\n\nVous avez demandé la suppression de votre compte. Pour la "
                    + "confirmer, ouvrez ce lien :\n{0}\n\nVos données seront effacées et "
                    + "ne pourront pas être récupérées.\n\nSi vous n'avez rien demandé, "
                    + "ignorez ce message.\n\n"
                    + _marque,
                "<p>Bonjour,</p><p>Vous avez demandé la suppression de votre compte. Pour la "
                    + "confirmer, ouvrez ce lien :<br><a href=\"{0}\">{0}</a></p><p><strong>Vos "
                    + "données seront effacées et ne pourront pas être récupérées.</strong></p>"
                    + "<p>Si vous n'avez rien demandé, ignorez ce message.</p><p>"
                    + _marque
                    + "</p>"
            ),
            (Courriel.Suppression, true) => new(
                _marque + " — confirm your account deletion",
                "Hello,\n\nYou asked to delete your account. To confirm, open this link:\n{0}"
                    + "\n\nYour data will be erased and cannot be recovered.\n\nIf you did "
                    + "not ask for this, ignore this message.\n\n"
                    + _marque,
                "<p>Hello,</p><p>You asked to delete your account. To confirm, open this link:"
                    + "<br><a href=\"{0}\">{0}</a></p><p><strong>Your data will be erased and "
                    + "cannot be recovered.</strong></p><p>If you did not ask for this, ignore "
                    + "this message.</p><p>"
                    + _marque
                    + "</p>"
            ),
            _ => throw new ArgumentOutOfRangeException(nameof(courriel)),
        };
    }
}
