# syntax=docker/dockerfile:1
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props ./

COPY src/BuildingBlocks/Rappix.BuildingBlocks.Core/*.csproj src/BuildingBlocks/Rappix.BuildingBlocks.Core/
COPY src/BuildingBlocks/Rappix.BuildingBlocks.Messaging/*.csproj src/BuildingBlocks/Rappix.BuildingBlocks.Messaging/
COPY src/BuildingBlocks/Rappix.BuildingBlocks.Observability/*.csproj src/BuildingBlocks/Rappix.BuildingBlocks.Observability/
COPY src/BuildingBlocks/Rappix.BuildingBlocks.WebApi/*.csproj src/BuildingBlocks/Rappix.BuildingBlocks.WebApi/
COPY src/Contracts/Rappix.Contracts/*.csproj src/Contracts/Rappix.Contracts/
COPY src/Services/Orders/Rappix.Orders.Domain/*.csproj src/Services/Orders/Rappix.Orders.Domain/
COPY src/Services/Orders/Rappix.Orders.Application/*.csproj src/Services/Orders/Rappix.Orders.Application/
COPY src/Services/Orders/Rappix.Orders.Infrastructure/*.csproj src/Services/Orders/Rappix.Orders.Infrastructure/
COPY src/Services/Orders/Rappix.Orders.Api/*.csproj src/Services/Orders/Rappix.Orders.Api/

RUN dotnet restore src/Services/Orders/Rappix.Orders.Api/Rappix.Orders.Api.csproj

COPY src/ src/

RUN dotnet publish src/Services/Orders/Rappix.Orders.Api/Rappix.Orders.Api.csproj \
    -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish ./

USER $APP_UID

# 8080 REST (HTTP/1) y 8081 HTTP/2 (gRPC interno futuro) se configuran en codigo via ConfigureKestrel (ADR-0003).
EXPOSE 8080
EXPOSE 8081

ENTRYPOINT ["dotnet", "Rappix.Orders.Api.dll"]
