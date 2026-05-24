# syntax=docker/dockerfile:1
# Build context: la raiz del repo (ver docker-compose: context = .)

# ── Build ──────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Props de nivel de solucion (Central Package Management) para cachear el restore.
COPY Directory.Build.props Directory.Packages.props ./

# csproj primero: maximiza el cacheo de capas del restore. El gateway solo usa los building
# blocks Observability (Serilog→Seq) y WebApi (ExceptionHandler, CorrelationId, middleware).
COPY src/BuildingBlocks/Rappix.BuildingBlocks.Core/*.csproj src/BuildingBlocks/Rappix.BuildingBlocks.Core/
COPY src/BuildingBlocks/Rappix.BuildingBlocks.Observability/*.csproj src/BuildingBlocks/Rappix.BuildingBlocks.Observability/
COPY src/BuildingBlocks/Rappix.BuildingBlocks.WebApi/*.csproj src/BuildingBlocks/Rappix.BuildingBlocks.WebApi/
COPY src/ApiGateway/Rappix.Gateway.Api/*.csproj src/ApiGateway/Rappix.Gateway.Api/

RUN dotnet restore src/ApiGateway/Rappix.Gateway.Api/Rappix.Gateway.Api.csproj

# Resto del codigo fuente.
COPY src/ src/

RUN dotnet publish src/ApiGateway/Rappix.Gateway.Api/Rappix.Gateway.Api.csproj \
    -c Release -o /app/publish --no-restore /p:UseAppHost=false

# ── Runtime ────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish ./

# Usuario no-root provisto por las imagenes .NET (UID 1654).
USER $APP_UID

# Solo se expone 8080 (REST + WebSocket upgrade para el hub de Tracking). El gateway no expone
# gRPC ni HTTP/2 cleartext propios — los clusters destino que sirven gRPC (Merchants/Catalog/Pricing)
# usan sus puertos 8081 en la red interna y no estan expuestos via el gateway (decision Fase 10:
# gRPC se mantiene service-to-service intra-cluster).
EXPOSE 8080

ENTRYPOINT ["dotnet", "Rappix.Gateway.Api.dll"]
