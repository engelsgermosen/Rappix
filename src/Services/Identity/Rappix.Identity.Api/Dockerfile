# syntax=docker/dockerfile:1
# Build context: la raiz del repo (ver docker-compose: context = .)

# ── Build ──────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Props de nivel de solucion (Central Package Management) para cachear el restore.
COPY Directory.Build.props Directory.Packages.props ./

# csproj primero: maximiza el cacheo de capas del restore.
COPY src/BuildingBlocks/Rappix.BuildingBlocks.Core/*.csproj src/BuildingBlocks/Rappix.BuildingBlocks.Core/
COPY src/BuildingBlocks/Rappix.BuildingBlocks.Messaging/*.csproj src/BuildingBlocks/Rappix.BuildingBlocks.Messaging/
COPY src/BuildingBlocks/Rappix.BuildingBlocks.Observability/*.csproj src/BuildingBlocks/Rappix.BuildingBlocks.Observability/
COPY src/BuildingBlocks/Rappix.BuildingBlocks.WebApi/*.csproj src/BuildingBlocks/Rappix.BuildingBlocks.WebApi/
COPY src/Contracts/Rappix.Contracts/*.csproj src/Contracts/Rappix.Contracts/
COPY src/Services/Identity/Rappix.Identity.Domain/*.csproj src/Services/Identity/Rappix.Identity.Domain/
COPY src/Services/Identity/Rappix.Identity.Application/*.csproj src/Services/Identity/Rappix.Identity.Application/
COPY src/Services/Identity/Rappix.Identity.Infrastructure/*.csproj src/Services/Identity/Rappix.Identity.Infrastructure/
COPY src/Services/Identity/Rappix.Identity.Api/*.csproj src/Services/Identity/Rappix.Identity.Api/

RUN dotnet restore src/Services/Identity/Rappix.Identity.Api/Rappix.Identity.Api.csproj

# Resto del codigo fuente.
COPY src/ src/

RUN dotnet publish src/Services/Identity/Rappix.Identity.Api/Rappix.Identity.Api.csproj \
    -c Release -o /app/publish --no-restore /p:UseAppHost=false

# ── Runtime ────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish ./

# Usuario no-root provisto por las imagenes .NET (UID 1654).
USER $APP_UID

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "Rappix.Identity.Api.dll"]
