using Palier.Application.Pipeline;

namespace Palier.Application.Tests.Pipeline;

/// <summary>
/// Cette exception existe depuis le lot 2 et n'était couverte par rien : le
/// projet applicatif n'avait pas de suite de tests, donc pas de seuil, donc
/// aucune détection de code mort public. Le seuil posé au lot 4 l'a signalée
/// dès sa première exécution.
///
/// <para>
/// Les deux constructeurs sans nom de cas d'usage sont imposés par CA1032, qui
/// exige les constructeurs standards d'une exception. Les éprouver coûte six
/// lignes ; les laisser non couverts aurait laissé le seuil rouge, et un seuil
/// qu'on abaisse pour le faire taire ne mord plus jamais.
/// </para>
/// </summary>
public sealed class IdentiteAbsenteExceptionTests
{
    [Fact]
    public void Le_message_nomme_le_cas_d_usage_et_dit_que_rien_n_a_ete_lu()
    {
        var exception = new IdentiteAbsenteException("EnregistrerUneSeance");

        Assert.Equal("EnregistrerUneSeance", exception.NomDuCasDUsage);
        Assert.Contains("EnregistrerUneSeance", exception.Message, StringComparison.Ordinal);

        // La seconde moitié du message compte autant que la première : elle dit
        // qu'aucune transaction n'a été ouverte, donc qu'il n'y a rien à défaire.
        Assert.Contains("Aucune transaction", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Le_constructeur_sans_argument_reste_utilisable()
    {
        var exception = new IdentiteAbsenteException();

        Assert.Equal("(inconnu)", exception.NomDuCasDUsage);
        Assert.False(string.IsNullOrWhiteSpace(exception.Message));
    }

    [Fact]
    public void Le_constructeur_a_exception_interne_la_conserve()
    {
        var cause = new InvalidOperationException("la cause");

        var exception = new IdentiteAbsenteException("message", cause);

        Assert.Same(cause, exception.InnerException);
        Assert.Equal("(inconnu)", exception.NomDuCasDUsage);
    }
}
