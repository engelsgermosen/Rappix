using System.Text;
using Asp.Versioning;
using Asp.Versioning.Builder;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Rappix.BuildingBlocks.Observability.Extensions;
using Rappix.BuildingBlocks.WebApi.Errors;
using Rappix.BuildingBlocks.WebApi.Middleware;
using Rappix.Identity.Api.Authentication;
using Rappix.Identity.Api.Endpoints;
using Rappix.Identity.Api.OpenApi;
using Rappix.Identity.Application;
using Rappix.Identity.Application.Authentication;
using Rappix.Identity.Application.Configuration;
using Rappix.Identity.Infrastructure;
using Rappix.Identity.Infrastructure.Mail;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Host.UseRappixLogging("identity");

// Opciones tipadas.
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
builder.Services.Configure<AppOptions>(builder.Configuration.GetSection(AppOptions.SectionName));
builder.Services.Configure<SendGridOptions>(builder.Configuration.GetSection(SendGridOptions.SectionName));

// Capas de aplicacion e infraestructura.
builder.Services.AddIdentityApplication();
builder.Services.AddIdentityInfrastructure(builder.Configuration);

// Servicio de autenticacion externa (acoplado a HTTP, vive en la capa Api).
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IExternalAuthService, HttpExternalAuthService>();

// Manejo global de excepciones (ProblemDetails RFC 7807).
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

// Autenticacion: JWT (por defecto) + cookie externa + Google (si esta configurado).
JwtOptions jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
if (string.IsNullOrWhiteSpace(jwt.SigningKey))
{
    throw new InvalidOperationException(
        "Jwt:SigningKey no esta configurado. Defina una clave de al menos 256 bits en appsettings.Development.json o variables de entorno.");
}

AuthenticationBuilder authentication = builder.Services
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
    })
    .AddCookie(ExternalAuthDefaults.Scheme, options =>
    {
        options.Cookie.Name = "rappix.external";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.ExpireTimeSpan = TimeSpan.FromMinutes(10);
    });

string? googleClientId = builder.Configuration["Google:ClientId"];
string? googleClientSecret = builder.Configuration["Google:ClientSecret"];
if (!string.IsNullOrWhiteSpace(googleClientId) && !string.IsNullOrWhiteSpace(googleClientSecret))
{
    authentication.AddGoogle(options =>
    {
        options.ClientId = googleClientId;
        options.ClientSecret = googleClientSecret;
        options.SignInScheme = ExternalAuthDefaults.Scheme;
        options.CallbackPath = builder.Configuration["Google:CallbackPath"] ?? "/signin-google";
        options.SaveTokens = false;
    });
}

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

// OpenAPI nativo de .NET 10 (Microsoft.OpenApi 2.0) para generar el documento;
// la UI la sirve Swashbuckle.SwaggerUI apuntando al JSON nativo. Se usa el generador
// nativo porque Swashbuckle 7.2 no es compatible con Microsoft.OpenApi 2.0 que .NET 10
// arrastra via Microsoft.AspNetCore.OpenApi (referencia transitiva de BuildingBlocks.WebApi).
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi("v1", options => options.AddDocumentTransformer<BearerSecuritySchemeTransformer>());

WebApplication app = builder.Build();

app.UseExceptionHandler();
app.UseCorrelationId();
app.UseRappixRequestLogging();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/openapi/v1.json", "Rappix Identity v1"));
}

app.UseAuthentication();
app.UseAuthorization();
app.UseIdempotency();

ApiVersionSet versionSet = app.NewApiVersionSet()
    .HasApiVersion(new ApiVersion(1, 0))
    .ReportApiVersions()
    .Build();

RouteGroupBuilder apiV1 = app.MapGroup("/api/v{version:apiVersion}").WithApiVersionSet(versionSet);
apiV1.MapAuthEndpoints();
apiV1.MapUserEndpoints();

app.Run();

/// <summary>Punto de entrada expuesto como partial para WebApplicationFactory en pruebas.</summary>
public partial class Program;
