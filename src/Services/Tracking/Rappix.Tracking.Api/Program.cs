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
using Rappix.Tracking.Api.Authentication;
using Rappix.Tracking.Api.Endpoints;
using Rappix.Tracking.Api.Hubs;
using Rappix.Tracking.Api.OpenApi;
using Rappix.Tracking.Application;
using Rappix.Tracking.Application.Abstractions;
using Rappix.Tracking.Infrastructure;
using Scalar.AspNetCore;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Host.UseRappixLogging("tracking");

// Dos endpoints Kestrel cleartext: 8080 = REST + SignalR (HTTP/1.1, WebSockets se montan sobre el mismo
// listener), 8081 = HTTP/2 cleartext reservado para un futuro TrackingGrpc streaming (no se expone
// servicio en Fase 7). HTTP/2 sin TLS no negocia con HTTP/1.1 en el mismo puerto (ADR-0003). No usar
// ASPNETCORE_URLS.
builder.WebHost.ConfigureKestrel(kestrel =>
{
    kestrel.ListenAnyIP(8080, listen => listen.Protocols = HttpProtocols.Http1);
    kestrel.ListenAnyIP(8081, listen => listen.Protocols = HttpProtocols.Http2);
});

// Opciones tipadas.
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));

// Enums por nombre en JSON (TrackingStatus se serializara como string en respuestas REST).
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// Capas de aplicacion e infraestructura. Las DI seran completadas en commits posteriores (Domain en
// commit 2, persistence en commit 3, SignalR en commit 4, consumers en commit 6-8).
builder.Services.AddTrackingApplication();
builder.Services.AddTrackingInfrastructure(builder.Configuration);

// Manejo global de excepciones (ProblemDetails RFC 7807).
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

// Autenticacion JWT: valida los tokens emitidos por Identity (misma clave/issuer/audience).
// El handshake del Hub SignalR se autentica con el mismo bearer; el evento OnMessageReceived que
// pesca ?access_token=... para /hubs/tracking se anadira en commit 4.
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

        // SignalR sobre WebSockets: el handshake en navegador NO permite header Authorization en
        // el upgrade, asi que el cliente envia el bearer como ?access_token=... en la query. Se
        // pesca SOLO en paths /hubs/tracking (mitigacion de leaks por logs/referer/proxy en
        // endpoints REST).
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = ctx =>
            {
                string? accessToken = ctx.Request.Query["access_token"];
                PathString path = ctx.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs/tracking"))
                {
                    ctx.Token = accessToken;
                }
                return Task.CompletedTask;
            },
        };
    });

// Cualquier usuario autenticado consulta SUS pedidos (la authorization fina por ownership se valida
// dentro de los handlers: JWT.sub vs OrderTracking.CustomerUserId).
builder.Services.AddAuthorization();

// SignalR sobre el listener HTTP/1.1 (puerto 8080) — comparte el mismo Kestrel que el REST.
// Sin backplane Redis en Fase 7 (single instance; AddStackExchangeRedis se anadira en una fase
// futura cuando se escale a multiples replicas — el paquete ya esta en CPM listo).
builder.Services.AddSignalR();

// Implementacion SignalR de IClientNotifier. Singleton porque IHubContext es thread-safe; los
// consumers MassTransit (scoped) lo inyectan sin scope mismatch (singleton-into-scoped es valido).
builder.Services.AddSingleton<IClientNotifier, SignalRClientNotifier>();

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
            Title = "Rappix Tracking API",
            Version = "v1",
            Description = "Tracking en vivo del pedido + ubicacion del courier asignado, via SignalR (push) y REST (snapshot fallback). Proyecta eventos de Orders y Dispatch a un read model autorizado por ownership.",
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
        migrationScope.ServiceProvider.GetRequiredService<Rappix.Tracking.Infrastructure.Persistence.TrackingDbContext>().Database);
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

// NO se llama app.UseIdempotency() — Tracking expone solo GET REST + Hub SignalR (sin POST/PUT/PATCH
// mutantes que requieran dedup por header Idempotency-Key). Por eso tampoco se registra IDistributedCache
// en AddTrackingInfrastructure y el servicio queda independiente de Redis.

ApiVersionSet versionSet = app.NewApiVersionSet()
    .HasApiVersion(new ApiVersion(1, 0))
    .ReportApiVersions()
    .Build();

// Grupo /api/v1 para los endpoints REST.
RouteGroupBuilder apiV1 = app.MapGroup("/api/v{version:apiVersion}").WithApiVersionSet(versionSet);
apiV1.MapTrackingEndpoints();

// Hub SignalR /hubs/tracking. El [Authorize] del hub rechaza el handshake sin JWT valido (401).
// Subscribe(orderId) hace el check de ownership: JWT.sub == OrderTracking.CustomerUserId; mismo
// mensaje "no autorizado" para 404/403 (no se revela existencia).
app.MapHub<TrackingHub>("/hubs/tracking");

app.MapGet("/health", () => Results.Ok(new { service = "tracking", status = "ok" }));

app.Run();

/// <summary>Punto de entrada expuesto como partial para WebApplicationFactory en pruebas.</summary>
public partial class Program;
