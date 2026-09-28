# L'image de l'API — D83. Deux étapes : le SDK construit, le runtime ASP.NET
# exécute. Rien de ce qui sert à construire n'arrive dans l'image finale.
#
# Le contexte de construction est la RACINE du dépôt : `global.json`,
# `Directory.Build.props` et `.editorconfig` y vivent, et ils décident de la
# compilation — analyseurs en erreur compris. Construire sans eux produirait un
# binaire que la CI n'a jamais vu. `.dockerignore` ne laisse passer que ces
# fichiers, les quatre projets de production et le référentiel.
#
# Les images Microsoft sont désignées par leur étiquette de version majeure, et
# non par empreinte : elles portent les correctifs de sécurité du runtime, et
# Dependabot (écosystème `docker`) propose les montées.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS construction
WORKDIR /src

# La restauration d'abord, sur les seuls fichiers de projet : cette couche ne se
# reconstruit que si une dépendance change, pas à chaque ligne de code.
COPY global.json Directory.Build.props .editorconfig ./
COPY back/Palier.Domain/Palier.Domain.csproj back/Palier.Domain/
COPY back/Palier.Application/Palier.Application.csproj back/Palier.Application/
COPY back/Palier.Infrastructure/Palier.Infrastructure.csproj back/Palier.Infrastructure/
COPY back/Palier.Api/Palier.Api.csproj back/Palier.Api/
RUN dotnet restore back/Palier.Api/Palier.Api.csproj

COPY back/ back/
RUN dotnet publish back/Palier.Api/Palier.Api.csproj \
      --configuration Release \
      --no-restore \
      --output /publication \
      -p:UseAppHost=false

# Les fichiers que `preparer-la-base` applique après les migrations (D81).
COPY db/referentiel/ /publication/referentiel/

# Lisibles par tous, modifiables par personne d'autre que root. Un `COPY`
# conserve les droits du poste qui construit : sous un masque de création 007,
# `appsettings.json` arrivait en 0660 et l'utilisateur de l'API ne pouvait plus
# le lire — mesuré le 28/09/2026, « Access to the path is denied » au démarrage.
RUN chmod -R a+rX,go-w /publication

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS execution
WORKDIR /app

COPY --from=construction /publication ./

ENV PALIER_REFERENTIEL=/app/referentiel \
    ASPNETCORE_HTTP_PORTS=8080

# L'utilisateur non privilégié que l'image fournit (`app`, UID 1654). Le
# processus ne peut ni écrire dans /app ni ouvrir un port inférieur à 1024 :
# c'est pour cela qu'il écoute sur 8080.
USER $APP_UID
EXPOSE 8080

ENTRYPOINT ["dotnet", "Palier.Api.dll"]
