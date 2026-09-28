using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using Scalar.AspNetCore;

namespace Palier.Api.Socle;

/// <summary>
/// Le document OpenAPI et l'interface Scalar — D82.
/// </summary>
/// <remarks>
/// <para>
/// <b>Exposés en Development seulement.</b> Le document décrit chaque route,
/// chaque corps attendu et chaque politique : c'est une carte, et
/// <c>CLAUDE.md</c> § 4 interdit de rendre à l'extérieur « ce qui l'aiderait à
/// attaquer ». En production, les deux routes n'existent pas, et une épreuve le
/// vérifie sur l'application composée.
/// </para>
///
/// <para>
/// Le jeton est décrit comme il voyage : dans l'en-tête <c>Authorization</c>, et
/// nulle part ailleurs — <c>SaveToken</c> est à faux, la chaîne de requête n'est
/// jamais lue. Une route ANONYME ne porte pas l'exigence : l'inscription et la
/// connexion afficheraient sinon un cadenas qui n'existe pas.
/// </para>
/// </remarks>
internal static class DocumentationOpenApi
{
    /// <summary>Le nom du schéma de sécurité, tel que Scalar le présélectionne.</summary>
    internal const string Schema = "Bearer";

    /// <summary>Le chemin du document, celui que <c>MapOpenApi</c> sert par défaut.</summary>
    internal const string CheminDuDocument = "/openapi/v1.json";

    /// <summary>Le chemin de l'interface.</summary>
    internal const string CheminDeLInterface = "/scalar";

    /// <summary>Enregistre la génération du document. Sans effet tant que rien ne le sert.</summary>
    public static void Composer(IServiceCollection services) =>
        services.AddOpenApi(options =>
        {
            options.AddDocumentTransformer(DecrireLeDocumentAsync);
            options.AddOperationTransformer(ExigerLeJetonAsync);
        });

    /// <summary>
    /// Attache le document et l'interface — en Development, et seulement là.
    /// </summary>
    public static void Router(WebApplication application)
    {
        ArgumentNullException.ThrowIfNull(application);

        if (!application.Environment.IsDevelopment())
        {
            return;
        }

        application.MapOpenApi();
        application.MapScalarApiReference(
            CheminDeLInterface,
            options => options.WithTitle("Palier API").AddPreferredSecuritySchemes(Schema)
        );
    }

    private static Task DecrireLeDocumentAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext contexte,
        CancellationToken jeton
    )
    {
        document.Info = new OpenApiInfo
        {
            Title = "Palier API",
            Version = "v1",
            Description =
                "API de suivi d'entraînement en musculation. Elle informe, elle ne prescrit "
                + "jamais.\n\nPour essayer une route protégée : `POST /api/v1/auth/inscription`, "
                + "puis `POST /api/v1/auth/verifier-l-adresse` avec le compte et le code du "
                + "courriel reçu (Mailpit dans la démonstration), puis "
                + "`POST /api/v1/auth/connexion`. Le champ `jetonDAcces` de la réponse se colle "
                + "dans l'authentification « Bearer ».",
        };

        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes = new Dictionary<string, IOpenApiSecurityScheme>
        {
            [Schema] = new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                In = ParameterLocation.Header,
                BearerFormat = "JWT",
                Description =
                    "Jeton d'accès court, signé HS256. Le jeton de rafraîchissement voyage "
                    + "à part, dans un cookie HttpOnly.",
            },
        };

        return Task.CompletedTask;
    }

    private static Task ExigerLeJetonAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext contexte,
        CancellationToken jeton
    )
    {
        var metadonnees = contexte.Description.ActionDescriptor.EndpointMetadata;

        // Les deux marqueurs que `RequireAuthorization` et `AllowAnonymous`
        // posent. L'anonyme gagne, comme dans l'intergiciel d'autorisation.
        var isProtegee =
            metadonnees.OfType<IAuthorizeData>().Any()
            && !metadonnees.OfType<IAllowAnonymous>().Any();

        if (isProtegee)
        {
            operation.Security ??= [];
            operation.Security.Add(
                new OpenApiSecurityRequirement
                {
                    [new OpenApiSecuritySchemeReference(Schema, contexte.Document)] = [],
                }
            );
        }

        return Task.CompletedTask;
    }
}
