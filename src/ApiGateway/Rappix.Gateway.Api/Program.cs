using System.Globalization;
using System.Text;
using System.Threading.RateLimiting;
using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using Rappix.BuildingBlocks.Observability.Extensions;
using Rappix.BuildingBlocks.WebApi.Errors;
using Rappix.BuildingBlocks.WebApi.Middleware;
using Rappix.Gateway.Api.Authentication;
using Rappix.Gateway.Api.Health;
using Rappix.Gateway.Api.OpenApi;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Host.UseRappixLogging("gateway");

// Manejo global de excepciones (ProblemDetails RFC 7807) — patron identico a los 9 servicios.
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

// Autenticacion JWT: replica EXACTA del bloque usado por los 9 servicios (defensa en
// profundidad: ambos lados validan). Token issued por Identity (issuer/audience/key compartidos).
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
JwtOptions jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
if (string.IsNullOrWhiteSpace(jwt.SigningKey))
{
    throw new InvalidOperationException(
        "Jwt:SigningKey no esta configurado. Defina la misma clave que Identity en variables de entorno (IDENTITY_JWT_SIGNINGKEY).");
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
            ClockSkew = TimeSpan.FromSeconds(30),         // matchea a los 9 servicios
            NameClaimType = "sub",
        };
        // NO se configura OnMessageReceived para /hubs/tracking — el handshake del hub
        // pasa como Anonymous (route tracking-hub) y Tracking valida el access_token (defensa en profundidad).
    });

// FallbackPolicy = la policy de YARP cuando la route NO declara AuthorizationPolicy explicita.
// Las rutas marcadas "AuthorizationPolicy": "Anonymous" en appsettings.json saltan este check.
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

// CORS: DevelopmentCors (any origin, sin credentials) cuando ASPNETCORE_ENVIRONMENT=Development;
// ProductionCors (orígenes desde config, con credentials) en cualquier otro environment.
// Fail-closed en prod: si Cors:AllowedOrigins esta vacio, WithOrigins([]) rechaza todo.
const string DevelopmentCors = "DevelopmentCors";
const string ProductionCors  = "ProductionCors";
string[] allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

builder.Services.AddCors(options =>
{
    options.AddPolicy(DevelopmentCors, p => p
        .AllowAnyOrigin()
        .AllowAnyHeader()
        .AllowAnyMethod());

    options.AddPolicy(ProductionCors, p => p
        .WithOrigins(allowedOrigins)
        .AllowCredentials()
        .WithMethods("GET", "POST", "PUT", "DELETE", "PATCH", "OPTIONS")
        .WithHeaders("Authorization", "Content-Type", "X-Correlation-Id", "Idempotency-Key", "If-Match", "If-None-Match")
        .WithExposedHeaders("X-Correlation-Id", "Retry-After", "Location"));
});

// Rate limiting global (ASP.NET Core 10 shared framework). Dos politicas + 1 exencion:
//   - anonymous: fixed window por IP (default 100 req/min).
//   - authenticated: sliding window por "sub" claim (default 300 req/min).
//   - webhook Stripe (POST /payments/webhooks/stripe): NoLimiter (las llamadas vienen de Stripe).
// Limites configurables via RateLimit:Anonymous:* y RateLimit:Authenticated:* (override en tests con
// Window=5s para no esperar 1 minuto). Ver ADR-0011 seccion 3.3.
int anonymousPermitLimit  = builder.Configuration.GetValue("RateLimit:Anonymous:PermitLimit", 100);
int anonymousWindowSec    = builder.Configuration.GetValue("RateLimit:Anonymous:WindowSeconds", 60);
int authenticatedPermitLimit = builder.Configuration.GetValue("RateLimit:Authenticated:PermitLimit", 300);
int authenticatedWindowSec   = builder.Configuration.GetValue("RateLimit:Authenticated:WindowSeconds", 60);

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (ctx, ct) =>
    {
        if (ctx.Lease.TryGetMetadata(MetadataName.RetryAfter, out TimeSpan retryAfter))
        {
            ctx.HttpContext.Response.Headers.RetryAfter =
                ((int)retryAfter.TotalSeconds).ToString(CultureInfo.InvariantCulture);
        }
        ctx.HttpContext.Response.ContentType = "application/problem+json";
        await ctx.HttpContext.Response.WriteAsync(
            """{"title":"Too Many Requests","status":429}""", ct);
    };

    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(http =>
    {
        // Webhook Stripe: exento (las llamadas vienen de IPs de Stripe).
        if (http.Request.Path.StartsWithSegments("/payments/webhooks/stripe"))
        {
            return RateLimitPartition.GetNoLimiter("stripe-webhook");
        }

        // Autenticado: sliding window por "sub" claim.
        string? sub = http.User.FindFirst("sub")?.Value;
        if (!string.IsNullOrEmpty(sub))
        {
            return RateLimitPartition.GetSlidingWindowLimiter(
                partitionKey: $"u:{sub}",
                factory: _ => new SlidingWindowRateLimiterOptions
                {
                    PermitLimit = authenticatedPermitLimit,
                    Window = TimeSpan.FromSeconds(authenticatedWindowSec),
                    SegmentsPerWindow = 6,
                    QueueLimit = 0,
                    AutoReplenishment = true,
                });
        }

        // Anonimo: fixed window por IP.
        string ip = http.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: $"ip:{ip}",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = anonymousPermitLimit,
                Window = TimeSpan.FromSeconds(anonymousWindowSec),
                QueueLimit = 0,
                AutoReplenishment = true,
            });
    });
});

// YARP reverse proxy: rutas + clusters viven en appsettings.json (seccion "ReverseProxy").
// Cada cluster apunta a http://<service>-api:8080/ en Docker, o http://localhost:500X/ en Development.
builder.Services
    .AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

// Health aggregator: itera los 9 clusters y registra un DownstreamUrlHealthCheck que lee el
// address en runtime via IConfiguration (no en startup). Esto permite que los tests overrideen
// los destinos via in-memory config. failureStatus=Degraded → el gateway NO cae si un downstream
// esta caido (devuelve 200 con entry marcado). El liveness propio (/health/live) sigue verde.
builder.Services.AddHttpClient();
IHealthChecksBuilder healthChecks = builder.Services.AddHealthChecks();
foreach (IConfigurationSection cluster in builder.Configuration.GetSection("ReverseProxy:Clusters").GetChildren())
{
    string clusterName = cluster.Key;
    healthChecks.AddTypeActivatedCheck<DownstreamUrlHealthCheck>(
        name: clusterName,
        failureStatus: HealthStatus.Degraded,
        tags: ["downstream"],
        args: [clusterName]);
}

WebApplication app = builder.Build();

app.UseExceptionHandler();
app.UseCorrelationId();
app.UseRappixRequestLogging();

// CORS antes de autenticacion: la preflight OPTIONS no lleva Authorization y debe responder
// con Allow-Origin para que el browser pueda enviar la request real con credenciales.
app.UseCors(app.Environment.IsDevelopment() ? DevelopmentCors : ProductionCors);

app.UseAuthentication();
app.UseAuthorization();

// Rate limiter despues de auth: el partitioner lee el claim "sub" para particionar por usuario.
app.UseRateLimiter();

// WebSockets: ANTES de MapReverseProxy. YARP detecta Upgrade: websocket y hace passthrough
// transparente al cluster tracking (HttpRequest.Version=1.1 / VersionPolicy=RequestVersionExact).
// El handshake del hub viene con ?access_token=... en query — la ruta tracking-hub es Anonymous
// en YARP; Tracking valida el JWT en su OnMessageReceived (defensa en profundidad). Ver ADR-0011 §3.5.
app.UseWebSockets();

// Landing HTML con links a los 9 Scalar UIs (ver ADR-0011, decision 3.7).
// AllowAnonymous explicito para que la FallbackPolicy no lo bloquee.
app.MapApiDocsLanding();

// Punto unico de proxy a los 9 servicios. Routes resueltas por path; clusters definen el destino.
app.MapReverseProxy();

// /health agregado de los 9 servicios (JSON con array de entries via UIResponseWriter).
// 200 Healthy/Degraded, 503 Unhealthy. Las dos rutas con AllowAnonymous porque la FallbackPolicy
// requiere autenticacion por default.
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse,
}).AllowAnonymous();

// /health/live = liveness propio del gateway (no toca downstreams) — para Docker healthcheck.
app.MapGet("/health/live", () => Results.Ok(new { service = "gateway", status = "ok" }))
   .AllowAnonymous();

await app.RunAsync();

/// <summary>Punto de entrada expuesto como partial para WebApplicationFactory en pruebas.</summary>
public partial class Program;
