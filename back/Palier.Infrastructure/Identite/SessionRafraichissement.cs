namespace Palier.Infrastructure.Identite;

// CA1819 « les propriétés ne doivent pas retourner de tableaux » est éteinte
// pour ce fichier et pour lui seul. `TokenHash` est un `bytea` PostgreSQL de
// 32 octets, et le tableau est le type que Npgsql y fait correspondre sans
// convertisseur. Le motif de la règle — « un appelant peut modifier le tableau
// qu'on lui rend » — vise une API publiée ; ce type est une ligne de table, et
// EF Core doit pouvoir écrire dedans. La borne est ici, dans le fichier
// concerné, plutôt que dans `.editorconfig` où elle couvrirait des fichiers que
// personne n'a relus (D21).
#pragma warning disable CA1819

/// <summary>
/// Une session de rafraîchissement. Le jeton lui-même n'y figure jamais : seule
/// son empreinte SHA-256 est conservée.
/// </summary>
/// <remarks>
/// Les noms suivent le schéma, en anglais, comme <c>Entites.cs</c> — traduire
/// ici ouvrirait une occasion de diverger du seul document qui fait autorité sur
/// les colonnes.
///
/// <para>
/// <b>Pourquoi SHA-256 et non PBKDF2.</b> Un jeton de rafraîchissement est une
/// valeur aléatoire de 256 bits, pas un mot de passe : il n'y a rien à deviner,
/// donc rien à ralentir. Y appliquer les 210 000 itérations du hachage de mot
/// de passe coûterait environ 220 ms à chaque rafraîchissement pour une
/// protection nulle.
/// </para>
///
/// <para>
/// <b><see cref="FamilyId" /> et <see cref="ConsumedAt" /> portent la détection
/// de vol.</b> Toute la chaîne issue d'une même connexion partage sa famille ;
/// un jeton déjà consommé qui se représente signifie que deux porteurs
/// détiennent la même chaîne, et la famille entière tombe. C'est la pratique
/// courante recommandée par la RFC 9700, publiée en janvier 2025.
/// </para>
/// </remarks>
public sealed class SessionRafraichissement
{
    public Guid Id { get; set; }

    /// <summary>Vers <c>AspNetUsers("Id")</c>, avec <c>on delete cascade</c> — article 17.</summary>
    public Guid OwnerId { get; set; }

    /// <summary>SHA-256 du jeton, 32 octets. Le jeton lui-même n'est stocké nulle part.</summary>
    public required byte[] TokenHash { get; set; }

    /// <summary>Partagée par toute la chaîne issue d'une même connexion.</summary>
    public Guid FamilyId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }

    /// <summary>Posé à la rotation. C'est lui qui rend la détection de réemploi possible.</summary>
    public DateTimeOffset? ConsumedAt { get; set; }

    /// <summary>Posé par une déconnexion, ou par la révocation d'une famille.</summary>
    public DateTimeOffset? RevokedAt { get; set; }

    /// <summary>
    /// Le jeton suivant de la chaîne. Il sert la fenêtre de grâce : un rejeu dans
    /// les trente secondes rend ce jeton-là au lieu de révoquer la famille.
    /// </summary>
    public Guid? ReplacedById { get; set; }

    /// <summary>Le user-agent, pour la liste des sessions actives.</summary>
    public string? Device { get; set; }

    public DateTimeOffset LastSeenAt { get; set; }
}
