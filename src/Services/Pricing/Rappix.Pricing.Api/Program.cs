using System.Text;
using System.Text.Json.Serialization;
using Asp.Versioning;
using Asp.Versioning.Builder;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.IdentityModel.Tokens;
using Rappix.BuildingBlocks.Observability.Extensions;
using Rappix.BuildingBlocks.WebApi.Errors;
using Rappix.BuildingBlocks.WebApi.Middleware;
using Rappix.Pricing.Api.Authentication;
using Rappix.Pricing.Api.Endpoints;
using Rappix.Pricing.Api.Grpc;
using Rappix.Pricing.Api.OpenApi;
using Rappix.Pricing.Application;
using Rappix.Pricing.Application.Configuration;
using Rappix.Pricing.Infrastructure;
using Scalar.AspNetCore;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Host.UseRappixLogging("pricing");

// Dos endpoints Kestrel cleartext: 8080 = REST (HTTP/1.1), 8081 = gRPC (HTTP/2 h2c). HTTP/2 sin TLS
// no negocia con HTTP/1.1 en el mismo puerto, por eso van separados (ADR-0003). No usar ASPNETCORE_URLS.
builder.WebHost.ConfigureKestrel(kestrel =>
{
    kestrel.ListenAnyIP(8080, listen => listen.Protocols = HttpProtocols.Http1);
    kestrel.ListenAnyIP(8081, listen => listen.Protocols = HttpProtocols.Http2);
});

// Opciones tipadas.
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
builder.Services.Configure<PricingOptions>(builder.Configuration.GetSection(PricingOptions.SectionName));

// Enums por nombre en JSON (Vertical, DiscountType) para una API legible.
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// Capas de aplicacion e infraestructura.
builder.Services.AddPricingApplication();
builder.Services.AddPricingInfrastructure(builder.Configuration);

// gRPC interno (puerto 8081): PricingService.
builder.Services.AddGrpc();

// Manejo global de excepciones (ProblemDetails RFC 7807).
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

// Autenticacion JWT: valida los tokens emitidos por Identity (misma clave/issuer/audience).
JwtOptions jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
if (string.IsNullOrWhiteSpace(jwt.SigningKey))
{
    throw new InvalidOperationException(
        "Jwt:SigningKey no esta configurado. Defina la misma clave que Identity en appsettings.Development.json o variables de entorno.");
}

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = "sub",
        };
    });

builder.Services.AddAuthorizationBuilder()
    .AddPolicy("RequireCustomer", policy => policy.RequireClaim("userType", "Customer"))
    .AddPolicy("RequireAdmin", policy => policy.RequireClaim("userType", "Admin"));

// Versionado de API por segmento de URL.
builder.Services
    .AddApiVersioning(options =>
    {
        options.DefaultApiVersion = new ApiVersion(1, 0);
        options.AssumeDefaultVersionWhenUnspecified = true;
        options.ReportApiVersions = true;
        options.ApiVersionReader = new UrlSegmentApiVersionReader();
    })
    .AddApiExplorer(options =>
    {
        options.GroupNameFormat = "'v'VVV";
        options.SubstituteApiVersionInUrl = true;
    });

// OpenAPI nativo de .NET 10 (Microsoft.OpenApi 2.0) + Scalar para la UI. No se usa Swashbuckle (ADR-0002).
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi("v1", options =>
{
    options.AddDocumentTransformer((document, context, cancellationToken) =>
    {
        document.Info = new()
        {
            Title = "Rappix Pricing API",
            Version = "v1",
            Description = "Motor de tarifas: cotiza el precio total de un pedido (subtotal, surge, descuentos, fees, ITBIS, propina) y emite cotizaciones persistidas con expiracion.",
        };
        return Task.CompletedTask;
    });
    options.AddDocumentTransformer<BearerSecuritySchemeTransformer>();
});

WebApplication app = builder.Build();

// Aplica las migraciones EF Core al arrancar, antes de cualquier hosted service. Se omite en pruebas:
// los WebApplicationFactory migran explicitamente en su InitializeAsync (entorno "Testing").
if (!app.Environment.IsEnvironment("Testing"))
{
    await using var migrationScope = app.Services.CreateAsyncScope();
    await Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.MigrateAsync(
        migrationScope.ServiceProvider.GetRequiredService<Rappix.Pricing.Infrastructure.Persistence.PricingDbContext>().Database);
}

app.UseExceptionHandler();
app.UseCorrelationId();
app.UseRappixRequestLogging();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();                 // documento en /openapi/v1.json
    app.MapScalarApiReference();      // UI en /scalar/v1
}

app.UseAuthentication();
app.UseAuthorization();

// La idempotencia solo aplica al REST; se excluye del puerto gRPC (8081).
app.UseWhen(
    context => context.Connection.LocalPort != 8081,
    branch => branch.UseIdempotency());

// gRPC interno.
app.MapGrpcService<PricingGrpcServiceImpl>();

ApiVersionSet versionSet = app.NewApiVersionSet()
    .HasApiVersion(new ApiVersion(1, 0))
    .ReportApiVersions()
    .Build();

RouteGroupBuilder apiV1 = app.MapGroup("/api/v{version:apiVersion}").WithApiVersionSet(versionSet);
apiV1.MapPricingEndpoints();
apiV1.MapAdminPricingEndpoints();

app.MapGet("/health", () => Results.Ok(new { service = "pricing", status = "ok" }));

app.Run();

/// <summary>Punto de entrada expuesto como partial para WebApplicationFactory en pruebas.</summary>
public partial class Program;
