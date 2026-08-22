namespace Palier.Infrastructure.Coffre;

/// <summary>
/// Une case vide au démarrage, remplie une fois, lue ensuite.
///
/// POURQUOI UN PORTEUR, ET PAS LE TROUSSEAU DIRECTEMENT. Le trousseau n'existe
/// qu'après <c>AmorcageDuTrousseau.StartAsync</c> ;
/// <c>GestionnaireDUtilisateurs</c> est résolu par requête, donc plus tard.
/// Enregistrer le trousseau lui-même en singleton obligerait le conteneur à le
/// construire au premier besoin — c'est-à-dire à parler au coffre PENDANT une
/// requête d'utilisateur, ce que la conception écarte explicitement.
/// </summary>
public sealed class PorteurDeTrousseau
{
    private TrousseauDeChiffrement? _trousseau;

    /// <summary>
    /// Le refus n'est pas décoratif : il transforme une erreur d'ordonnancement,
    /// qui se manifesterait autrement par un déchiffrement silencieusement
    /// impossible, en un message qui dit sa cause.
    /// </summary>
    public TrousseauDeChiffrement Trousseau =>
        _trousseau
        ?? throw new InvalidOperationException(
            "Le trousseau est lu avant son chargement. `AmorcageDuTrousseau` "
                + "doit s'exécuter avant que le serveur accepte une requête."
        );

    public void Poser(TrousseauDeChiffrement trousseau) => _trousseau = trousseau;
}
