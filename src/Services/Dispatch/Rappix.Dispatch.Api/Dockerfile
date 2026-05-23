# syntax=docker/dockerfile:1
# Build context: la raiz del repo (ver docker-compose: context = .)

# ── Build ──────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Props de nivel de solucion (Central Package Management) para cachear el restore.
COPY Directory.Build.props Directory.Packages.props ./

# csproj primero: maximiza el cacheo de capas del restore. Dispatch usa los building blocks Core,
# Messaging, Observability y WebApi (no Storage/Imaging: no maneja archivos).
COPY src/BuildingBlocks/Rappix.BuildingBlocks.Core/*.csproj src/BuildingBlocks/Rappix.BuildingBlocks.Core/
COPY src/BuildingBlocks/Rappix.BuildingBlocks.Messaging/*.csproj src/BuildingBlocks/Rappix.BuildingBlocks.Messaging/
COPY src/BuildingBlocks/Rappix.BuildingBlocks.Observability/*.csproj src/BuildingBlocks/Rappix.BuildingBlocks.Observability/
COPY src/BuildingBlocks/Rappix.BuildingBlocks.WebApi/*.csproj src/BuildingBlocks/Rappix.BuildingBlocks.WebApi/
COPY src/Contracts/Rappix.Contracts/*.csproj src/Contracts/Rappix.Contracts/
COPY src/Services/Dispatch/Rappix.Dispatch.Domain/*.csproj src/Services/Dispatch/Rappix.Dispatch.Domain/
COPY src/Services/Dispatch/Rappix.Dispatch.Application/*.csproj src/Services/Dispatch/Rappix.Dispatch.Application/
COPY src/Services/Dispatch/Rappix.Dispatch.Infrastructure/*.csproj src/Services/Dispatch/Rappix.Dispatch.Infrastructure/
COPY src/Services/Dispatch/Rappix.Dispatch.Api/*.csproj src/Services/Dispatch/Rappix.Dispatch.Api/

RUN dotnet restore src/Services/Dispatch/Rappix.Dispatch.Api/Rappix.Dispatch.Api.csproj

# Resto del codigo fuente.
COPY src/ src/

RUN dotnet publish src/Services/Dispatch/Rappix.Dispatch.Api/Rappix.Dispatch.Api.csproj \
    -c Release -o /app/publish --no-restore /p:UseAppHost=false

# ── Runtime ────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish ./

# Usuario no-root provisto por las imagenes .NET (UID 1654).
USER $APP_UID

# No se define ASPNETCORE_URLS: los dos endpoints (8080 REST/Http1, 8081 HTTP/2 h2c) se
# configuran en codigo via ConfigureKestrel. ASPNETCORE_URLS los sobreescribiria (ADR-0003).
EXPOSE 8080
EXPOSE 8081

ENTRYPOINT ["dotnet", "Rappix.Dispatch.Api.dll"]
