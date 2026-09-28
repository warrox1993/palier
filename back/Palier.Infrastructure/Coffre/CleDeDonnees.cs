namespace Palier.Infrastructure.Coffre;

/// <summary>
/// L'enveloppe d'une clé de données, telle qu'OKMS la rend : un JWE compact
/// que seul le coffre sait déballer.
///
/// RIEN ICI N'EST SECRET. Sans le compte de service, une enveloppe est du
/// bruit — c'est ce qui autorise la politique <c>using (true)</c> en lecture,
/// et c'est aussi ce qui donne au design ses deux compromissions
/// indépendantes : le serveur seul ne suffit pas, la base seule non plus.
///
/// L'enveloppe porte sa propre version de clé maîtresse (<c>x-key-ver</c>,
/// mesuré le 22/08/2026 dans l'en-tête du JWE). Une rotation côté OVH reste
/// donc déchiffrable sans que rien ici ne change.
/// </summary>
public sealed class CleDeDonnees
{
    public Guid Id { get; init; }

    public required string Enveloppe { get; init; }

    public DateTimeOffset CreeeLe { get; init; }
}
