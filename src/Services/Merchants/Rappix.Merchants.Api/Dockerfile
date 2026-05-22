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
COPY src/BuildingBlocks/Rappix.BuildingBlocks.Storage/*.csproj src/BuildingBlocks/Rappix.BuildingBlocks.Storage/
COPY src/BuildingBlocks/Rappix.BuildingBlocks.Imaging/*.csproj src/BuildingBlocks/Rappix.BuildingBlocks.Imaging/
COPY src/Contracts/Rappix.Contracts/*.csproj src/Contracts/Rappix.Contracts/
COPY src/Services/Merchants/Rappix.Merchants.Domain/*.csproj src/Services/Merchants/Rappix.Merchants.Domain/
COPY src/Services/Merchants/Rappix.Merchants.Application/*.csproj src/Services/Merchants/Rappix.Merchants.Application/
COPY src/Services/Merchants/Rappix.Merchants.Infrastructure/*.csproj src/Services/Merchants/Rappix.Merchants.Infrastructure/
COPY src/Services/Merchants/Rappix.Merchants.Api/*.csproj src/Services/Merchants/Rappix.Merchants.Api/

RUN dotnet restore src/Services/Merchants/Rappix.Merchants.Api/Rappix.Merchants.Api.csproj

# Resto del codigo fuente.
COPY src/ src/

RUN dotnet publish src/Services/Merchants/Rappix.Merchants.Api/Rappix.Merchants.Api.csproj \
    -c Release -o /app/publish --no-restore /p:UseAppHost=false

# ── Runtime ────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish ./

# Usuario no-root provisto por las imagenes .NET (UID 1654).
USER $APP_UID

# No se define ASPNETCORE_URLS: los dos endpoints (8080 REST/Http1, 8081 gRPC/Http2 h2c) se
# configuran en codigo via ConfigureKestrel. ASPNETCORE_URLS los sobreescribiria (ADR-0003).
EXPOSE 8080
EXPOSE 8081

ENTRYPOINT ["dotnet", "Rappix.Merchants.Api.dll"]
