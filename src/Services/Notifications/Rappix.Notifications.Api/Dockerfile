# syntax=docker/dockerfile:1
# Build context: la raiz del repo (ver docker-compose: context = .)

# ── Build ──────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Props de nivel de solucion (Central Package Management) para cachear el restore.
COPY Directory.Build.props Directory.Packages.props ./

# csproj primero: maximiza el cacheo de capas del restore. Notifications usa los building blocks Core,
# Messaging, Observability y WebApi (no Storage/Imaging: no maneja archivos).
COPY src/BuildingBlocks/Rappix.BuildingBlocks.Core/*.csproj src/BuildingBlocks/Rappix.BuildingBlocks.Core/
COPY src/BuildingBlocks/Rappix.BuildingBlocks.Messaging/*.csproj src/BuildingBlocks/Rappix.BuildingBlocks.Messaging/
COPY src/BuildingBlocks/Rappix.BuildingBlocks.Observability/*.csproj src/BuildingBlocks/Rappix.BuildingBlocks.Observability/
COPY src/BuildingBlocks/Rappix.BuildingBlocks.WebApi/*.csproj src/BuildingBlocks/Rappix.BuildingBlocks.WebApi/
COPY src/Contracts/Rappix.Contracts/*.csproj src/Contracts/Rappix.Contracts/
COPY src/Services/Notifications/Rappix.Notifications.Domain/*.csproj src/Services/Notifications/Rappix.Notifications.Domain/
COPY src/Services/Notifications/Rappix.Notifications.Application/*.csproj src/Services/Notifications/Rappix.Notifications.Application/
COPY src/Services/Notifications/Rappix.Notifications.Infrastructure/*.csproj src/Services/Notifications/Rappix.Notifications.Infrastructure/
COPY src/Services/Notifications/Rappix.Notifications.Api/*.csproj src/Services/Notifications/Rappix.Notifications.Api/

RUN dotnet restore src/Services/Notifications/Rappix.Notifications.Api/Rappix.Notifications.Api.csproj

# Resto del codigo fuente.
COPY src/ src/

RUN dotnet publish src/Services/Notifications/Rappix.Notifications.Api/Rappix.Notifications.Api.csproj \
    -c Release -o /app/publish --no-restore /p:UseAppHost=false

# ── Runtime ────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish ./

# Usuario no-root provisto por las imagenes .NET (UID 1654).
USER $APP_UID

# Solo se expone 8080 (REST). Notifications no tiene gRPC ni HTTP/2 cleartext en Fase 9 — si en el
# futuro se reserva el puerto HTTP/2, anadir ListenAnyIP(8081, Http2) en Program.cs + EXPOSE 8081.
EXPOSE 8080

ENTRYPOINT ["dotnet", "Rappix.Notifications.Api.dll"]
