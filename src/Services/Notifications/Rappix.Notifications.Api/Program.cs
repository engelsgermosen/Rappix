using System.Text;
using System.Text.Json.Serialization;
using Asp.Versioning;
using Asp.Versioning.Builder;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Rappix.BuildingBlocks.Observability.Extensions;
using Rappix.BuildingBlocks.WebApi.Errors;
using Rappix.BuildingBlocks.WebApi.Middleware;
using Rappix.Notifications.Api.Authentication;
using Rappix.Notifications.Api.OpenApi;
using Rappix.Notifications.Application;
using Rappix.Notifications.Application.Configuration;
using Rappix.Notifications.Infrastructure;
using Scalar.AspNetCore;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Host.UseRappixLogging("notifications");

// Notifications expone solo REST en 8080 (sin HTTP/2 ni gRPC en Fase 9 — el puerto 5019 mapeado en
// docker-compose queda reservado para una futura adicion sin recompilar el binario). Tampoco hay
// SignalR Hub (a diferencia de Tracking).

// Opciones tipadas.
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
builder.Services.Configure<NotificationsOptions>(builder.Configuration.GetSection(NotificationsOptions.SectionName));

// Enums por nombre en JSON (NotificationStatus, NotificationType y RecipientRole se serializan como
// string en respuestas REST — el contrato HTTP queda legible para los clientes).
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// Capas de aplicacion (MediatR + NotifyHandler) e infraestructura (DbContext + repos + canal +
// MassTransit con outbox callback). El switch del canal Fake|SendGrid sucede dentro de
// AddNotificationsInfrastructure segun Notifications:Channel.
builder.Services.AddNotificationsApplication();
builder.Services.AddNotificationsInfrastructure(builder.Configuration);

// Manejo global de excepciones (ProblemDetails RFC 7807).
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

// Autenticacion JWT: valida los tokens emitidos por Identity (misma clave/issuer/audience). No hay
// endpoints mutantes en Fase 9; el JWT existe para que el /health pueda discriminar tokens validos
// y para que un futuro /api/v1/notifications/me (historial del usuario) ya tenga el plumbing.
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

builder.Services.AddAuthorization();

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
            Title = "Rappix Notifications API",
            Version = "v1",
            Description = "Servicio de notificaciones: escucha eventos del pedido (Orders/Dispatch/Payments) y envia emails al cliente, merchant y courier via canal conmutable (Fake/SendGrid).",
        };
        return Task.CompletedTask;
    });
    options.AddDocumentTransformer<BearerSecuritySchemeTransformer>();
});

WebApplication app = builder.Build();

// Aplica las migraciones EF Core al arrancar (crea schema notifications + las 7 tablas + el unique
// partial index UX_Notification_BusinessKey), antes de cualquier hosted service (importante: MassTransit
// + el outbox EF necesitan las tablas InboxState/OutboxState/OutboxMessage al primer Start). Se omite
// en pruebas: los WebApplicationFactory migran explicitamente en su InitializeAsync (entorno "Testing").
if (!app.Environment.IsEnvironment("Testing"))
{
    await using var migrationScope = app.Services.CreateAsyncScope();
    await Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.MigrateAsync(
        migrationScope.ServiceProvider.GetRequiredService<Rappix.Notifications.Infrastructure.Persistence.NotificationsDbContext>().Database);
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

// NO se llama app.UseIdempotency() — Notifications no expone POST/PUT/PATCH mutantes. Mismo razonamiento
// que Tracking: no requiere IDistributedCache, no requiere Redis. Toda la idempotencia vive en
// (1) inbox EF de MassTransit (dedup por MessageId del broker), (2) el unique partial index
// UX_Notification_BusinessKey a nivel BD, (3) el side-effect guard transaccional en NotifyHandler.

ApiVersionSet versionSet = app.NewApiVersionSet()
    .HasApiVersion(new ApiVersion(1, 0))
    .ReportApiVersions()
    .Build();

// Grupo /api/v1 reservado para futuro endpoint /me (historial del usuario autenticado). Por ahora
// queda vacio — la verificacion del smoke E2E es SQL directo a la tabla Notifications.
RouteGroupBuilder apiV1 = app.MapGroup("/api/v{version:apiVersion}").WithApiVersionSet(versionSet);
_ = apiV1; // hint al compilador para que no marque la variable como sin uso.

app.MapGet("/health", () => Results.Ok(new { service = "notifications", status = "ok" }));

await app.RunAsync();

/// <summary>Punto de entrada expuesto como partial para WebApplicationFactory en pruebas.</summary>
public partial class Program;
