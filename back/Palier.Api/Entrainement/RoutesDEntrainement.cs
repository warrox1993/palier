using Palier.Api.Auth;

namespace Palier.Api.Entrainement;

/// <summary>
/// Le groupe de routes de l'entraînement.
/// </summary>
/// <remarks>
/// <para>
/// Le type ne s'appelle pas <c>Entrainement</c> : ce serait le nom de son
/// propre espace de noms, et le compilateur en fait une ambiguïté au premier
/// usage qualifié.
/// </para>
///
/// <para>
/// <b><c>RequireAuthorization</c> est posé SUR LE GROUPE</b>, une seule fois,
/// et non route par route. Une route ajoutée demain hérite de la politique sans
/// que personne y pense — alors qu'un attribut oublié sur une route parmi douze
/// ne se voit dans aucune revue.
/// </para>
/// </remarks>
internal static class RoutesDEntrainement
{
    /// <summary>Le préfixe, versionné dès la première route.</summary>
    private const string _prefixe = "/api/v1";

    public static void Router(WebApplication application)
    {
        ArgumentNullException.ThrowIfNull(application);

        var groupe = application
            .MapGroup(_prefixe)
            .RequireAuthorization(PolitiquesDAutorisation.Entrainement);

        Seances.Router(groupe);
        Series.Router(groupe);
        PoidsCorporel.Router(groupe);
        Mesures.Router(groupe);
    }
}
