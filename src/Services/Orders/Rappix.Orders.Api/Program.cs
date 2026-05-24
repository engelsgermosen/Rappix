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
using Rappix.Orders.Api.Authentication;
using Rappix.Orders.Api.Endpoints;
using Rappix.Orders.Api.OpenApi;
using Rappix.Orders.Application;
using Rappix.Orders.Application.Configuration;
using Rappix.Orders.Infrastructure;
using Scalar.AspNetCore;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Host.UseRappixLogging("orders");

// Dos endpoints Kestrel cleartext: 8080 = REST (HTTP/1.1), 8081 = HTTP/2 (reservado para gRPC interno
// futuro, p. ej. OrderService.GetOrderStatus). Puertos separados por ADR-0003.
builder.WebHost.ConfigureKestrel(kestrel =>
{
    kestrel.ListenAnyIP(8080, listen => listen.Protocols = HttpProtocols.Http1);
    kestrel.ListenAnyIP(8081, listen => listen.Protocols = HttpProtocols.Http2);
});

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
builder.Services.Configure<OrdersOptions>(builder.Configuration.GetSection(OrdersOptions.SectionName));

// Serializa los enums por nombre (Status, Vertical) en lugar de numerico.
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddOrdersApplication();
builder.Services.AddOrdersInfrastructure(builder.Configuration);

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

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
    .AddPolicy("RequireMerchant", policy => policy.RequireClaim("userType", "Merchant"));

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

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi("v1", options =>
{
    options.AddDocumentTransformer((document, context, cancellationToken) =>
    {
        document.Info = new()
        {
            Title = "Rappix Orders API",
            Version = "v1",
            Description = "Pedidos: arma el pedido (snapshot del quote) y orquesta el flujo distribuido con una saga (MassTransit State Machine) con compensaciones.",
        };
        return Task.CompletedTask;
    });
    options.AddDocumentTransformer<BearerSecuritySchemeTransformer>();
});

WebApplication app = builder.Build();

// Aplica las migraciones EF Core al arrancar, antes de los hosted services (Quartz + el bus MassTransit,
// que necesitan el esquema: qrtz_* + inbox/outbox). Con retry+backoff (1-2-4-8-16-32s, ~63s totales) por
// si Postgres aun esta inicializando: distingue 57P03/SocketException (reintenta) de errores reales de
// migracion (propaga sin reintento). No-op en entorno "Testing".
await app.MigrateDbContextWithRetryAsync<Rappix.Orders.Infrastructure.Persistence.OrdersDbContext>();

app.UseExceptionHandler();
app.UseCorrelationId();
app.UseRappixRequestLogging();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseAuthentication();
app.UseAuthorization();

// La idempotencia (Idempotency-Key) aplica al REST; se excluye del puerto HTTP/2 (8081).
app.UseWhen(
    context => context.Connection.LocalPort != 8081,
    branch => branch.UseIdempotency());

ApiVersionSet versionSet = app.NewApiVersionSet()
    .HasApiVersion(new ApiVersion(1, 0))
    .ReportApiVersions()
    .Build();

RouteGroupBuilder apiV1 = app.MapGroup("/api/v{version:apiVersion}").WithApiVersionSet(versionSet);
apiV1.MapOrderEndpoints();
apiV1.MapMerchantOrderEndpoints();
apiV1.MapOrderSeamEndpoints();

app.MapGet("/health", () => Results.Ok(new { service = "orders", status = "ok" }));

app.Run();

/// <summary>Punto de entrada expuesto como partial para WebApplicationFactory en pruebas.</summary>
public partial class Program;
