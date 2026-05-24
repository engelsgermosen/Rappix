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
using Rappix.BuildingBlocks.WebApi.Persistence;
using Rappix.Payments.Api.Authentication;
using Rappix.Payments.Api.Endpoints;
using Rappix.Payments.Api.OpenApi;
using Rappix.Payments.Application;
using Rappix.Payments.Application.Configuration;
using Rappix.Payments.Infrastructure;
using Scalar.AspNetCore;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Host.UseRappixLogging("payments");

// Dos endpoints Kestrel cleartext: 8080 = REST + webhook (HTTP/1.1), 8081 = HTTP/2 cleartext reservado
// para un futuro PaymentsGrpc (no se expone servicio en Fase 8). HTTP/2 sin TLS no negocia con HTTP/1.1
// en el mismo puerto (ADR-0003). No usar ASPNETCORE_URLS — sobreescribiria estos endpoints.
builder.WebHost.ConfigureKestrel(kestrel =>
{
    kestrel.ListenAnyIP(8080, listen => listen.Protocols = HttpProtocols.Http1);
    kestrel.ListenAnyIP(8081, listen => listen.Protocols = HttpProtocols.Http2);
});

// Opciones tipadas.
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
builder.Services.Configure<PaymentsOptions>(builder.Configuration.GetSection(PaymentsOptions.SectionName));

// Enums por nombre en JSON (PaymentStatus se serializara como string en respuestas REST futuras).
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// Capas de aplicacion e infraestructura.
builder.Services.AddPaymentsApplication();
builder.Services.AddPaymentsInfrastructure(builder.Configuration);

// Manejo global de excepciones (ProblemDetails RFC 7807).
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

// Autenticacion JWT: valida tokens emitidos por Identity (misma clave/issuer/audience). El webhook
// de Stripe es [AllowAnonymous] — se autentica por firma, no por JWT.
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

// Cualquier usuario autenticado puede acceder a endpoints REST futuros (no hay ninguno en Fase 8;
// el GET admin se difiere a Fase 9+ por decision del usuario). Sin politicas custom.
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

// OpenAPI nativo de .NET 10 (Microsoft.OpenApi 2.0) + Scalar para la UI (ADR-0002, sin Swashbuckle).
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi("v1", options =>
{
    options.AddDocumentTransformer((document, context, cancellationToken) =>
    {
        document.Info = new()
        {
            Title = "Rappix Payments API",
            Version = "v1",
            Description = "Cobra al cliente via Stripe (o Fake conmutable). Modelo hold+capture: " +
                          "autoriza al confirmar el pedido (saga AwaitingPayment), captura al " +
                          "OrderDelivered. Compensacion void pre-captura, NeedsReview post-captura " +
                          "(nunca auto-refund — humano decide). Webhook firmado en /webhooks/stripe.",
        };
        return Task.CompletedTask;
    });
    options.AddDocumentTransformer<BearerSecuritySchemeTransformer>();
});

WebApplication app = builder.Build();

// Aplica las migraciones EF Core al arrancar, antes de cualquier hosted service. Con retry+backoff
// (1-2-4-8-16-32s, ~63s totales) por si Postgres aun esta inicializando: distingue 57P03/SocketException
// (reintenta) de errores reales de migracion (propaga sin reintento). No-op en entorno "Testing" — los
// WebApplicationFactory migran explicitamente en su InitializeAsync.
await app.MigrateDbContextWithRetryAsync<Rappix.Payments.Infrastructure.Persistence.PaymentsDbContext>();

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

// NO se llama app.UseIdempotency() — Payments expone solo el webhook (anonimo, firmado) y no tiene
// POSTs mutantes que requieran dedup por Idempotency-Key. La idempotencia de los consumers se cubre
// con inbox EF + aggregate idempotente + Stripe Idempotency-Key (los 3 niveles, ADR-0009 #5).
// Por eso Payments no requiere IDistributedCache ni Redis.

ApiVersionSet versionSet = app.NewApiVersionSet()
    .HasApiVersion(new ApiVersion(1, 0))
    .ReportApiVersions()
    .Build();

// Grupo /api/v1 para el webhook (y futuros endpoints REST).
RouteGroupBuilder apiV1 = app.MapGroup("/api/v{version:apiVersion}").WithApiVersionSet(versionSet);
apiV1.MapStripeWebhook();

// El GET /api/v1/payments/{orderId} admin se DIFIERE a Fase 9+ (decision del usuario en plan-mode).

app.MapGet("/health", () => Results.Ok(new { service = "payments", status = "ok" }));

app.Run();

/// <summary>Punto de entrada expuesto como partial para WebApplicationFactory en pruebas.</summary>
public partial class Program;
