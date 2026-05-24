using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using WireMock.Server;

namespace Rappix.Gateway.Tests;

/// <summary>
/// Fabrica del API Gateway para tests de integracion. Arranca un <see cref="WireMockServer"/>
/// por cada uno de los 9 clusters YARP en puertos efimeros, reescribe los destinos via
/// in-memory configuration, e inyecta una <c>Jwt:SigningKey</c> de prueba.
/// </summary>
/// <remarks>
/// Patron: los <see cref="WireMockServer"/> son <c>readonly</c> inicializados en construccion
/// (antes de <see cref="ConfigureWebHost"/>) para que sus URLs esten disponibles cuando se
/// evalua <see cref="IConfigurationBuilder"/>. Lazy: el host se construye en la primera
/// <see cref="WebApplicationFactory{TEntryPoint}.CreateClient()"/>.
/// </remarks>
public class GatewayApiFactory : WebApplicationFactory<Program>
{
    /// <summary>Issuer emitido por Identity y validado por el gateway en los tests.</summary>
    public const string JwtIssuer = "https://localhost:5001";

    /// <summary>Audience validada por el gateway.</summary>
    public const string JwtAudience = "rappix";

    /// <summary>Clave HMAC SHA-256 compartida en los tests (>= 32 bytes).</summary>
    public const string JwtSigningKey = "rappix-gateway-tests-signing-key-please-change-32bytes-min";

    /// <summary>
    /// Static ctor: setea las env vars de JWT ANTES de que <c>WebApplication.CreateBuilder</c>
    /// las lea. El <c>ConfigureAppConfiguration</c> del WebApplicationFactory no sirve para JWT
    /// porque Program.cs evalua <c>builder.Configuration.GetSection("Jwt")</c> en tiempo de
    /// configuracion (antes de Build()), momento en el que las fuentes del WebApplicationFactory
    /// aun no han sido aplicadas. Las env vars, en cambio, son leidas por el DefaultBuilder durante
    /// <c>CreateBuilder</c>.
    /// </summary>
    static GatewayApiFactory()
    {
        Environment.SetEnvironmentVariable("Jwt__Issuer", JwtIssuer);
        Environment.SetEnvironmentVariable("Jwt__Audience", JwtAudience);
        Environment.SetEnvironmentVariable("Jwt__SigningKey", JwtSigningKey);
    }

    /// <summary>Mock del cluster <c>identity</c> (Identity API).</summary>
    public WireMockServer Identity { get; } = WireMockServer.Start();

    /// <summary>Mock del cluster <c>merchants</c>.</summary>
    public WireMockServer Merchants { get; } = WireMockServer.Start();

    /// <summary>Mock del cluster <c>catalog</c>.</summary>
    public WireMockServer Catalog { get; } = WireMockServer.Start();

    /// <summary>Mock del cluster <c>pricing</c>.</summary>
    public WireMockServer Pricing { get; } = WireMockServer.Start();

    /// <summary>Mock del cluster <c>orders</c>.</summary>
    public WireMockServer Orders { get; } = WireMockServer.Start();

    /// <summary>Mock del cluster <c>dispatch</c>.</summary>
    public WireMockServer Dispatch { get; } = WireMockServer.Start();

    /// <summary>Mock del cluster <c>tracking</c> (incluye hub SignalR — el handshake llega aqui).</summary>
    public WireMockServer Tracking { get; } = WireMockServer.Start();

    /// <summary>Mock del cluster <c>payments</c> (incluye webhook Stripe).</summary>
    public WireMockServer Payments { get; } = WireMockServer.Start();

    /// <summary>Mock del cluster <c>notifications</c> (sin endpoints REST en Fase 9 — solo /health).</summary>
    public WireMockServer Notifications { get; } = WireMockServer.Start();

    /// <summary>Reset de los 9 WireMocks (limpia stubs + LogEntries entre tests).</summary>
    public void ResetAll()
    {
        Identity.Reset(); Merchants.Reset(); Catalog.Reset(); Pricing.Reset();
        Orders.Reset(); Dispatch.Reset(); Tracking.Reset(); Payments.Reset(); Notifications.Reset();
    }

    /// <summary>
    /// Genera un JWT firmado con la <see cref="JwtSigningKey"/> de prueba, con los claims que
    /// emitiria Identity (<c>sub</c>, <c>userType</c>, <c>email</c>, <c>jti</c>).
    /// </summary>
    public static string CreateJwt(Guid? userId = null, string userType = "Customer")
        => CreateJwt(JwtSigningKey, userId ?? Guid.NewGuid(), userType);

    /// <summary>Genera un JWT firmado con una clave arbitraria (para el caso negativo de firma invalida).</summary>
    public static string CreateJwt(string signingKey, Guid userId, string userType = "Customer")
    {
        JsonWebTokenHandler handler = new();
        SigningCredentials credentials = new(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
            SecurityAlgorithms.HmacSha256);
        SecurityTokenDescriptor descriptor = new()
        {
            Issuer = JwtIssuer,
            Audience = JwtAudience,
            NotBefore = DateTime.UtcNow.AddSeconds(-30),
            Expires = DateTime.UtcNow.AddHours(1),
            SigningCredentials = credentials,
            Claims = new Dictionary<string, object>
            {
                ["sub"] = userId.ToString(),
                ["userType"] = userType,
                ["email"] = "test@example.com",
                ["jti"] = Guid.NewGuid().ToString(),
            },
        };
        return handler.CreateToken(descriptor);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((ctx, cfg) =>
        {
            cfg.AddInMemoryCollection(new Dictionary<string, string?>
            {
                // Override de los 9 destinos YARP para que apunten a los WireMockServer en lugar de los puertos reales.
                // YARP lee la config en runtime (MapReverseProxy), por lo que el AddInMemoryCollection si funciona.
                // El JWT setup, en cambio, lee la config en tiempo de configuracion (antes de Build()), por eso
                // las env vars de Jwt__* se setean en el static ctor — ver arriba.
                ["ReverseProxy:Clusters:identity:Destinations:default:Address"]      = Identity.Url + "/",
                ["ReverseProxy:Clusters:merchants:Destinations:default:Address"]     = Merchants.Url + "/",
                ["ReverseProxy:Clusters:catalog:Destinations:default:Address"]       = Catalog.Url + "/",
                ["ReverseProxy:Clusters:pricing:Destinations:default:Address"]       = Pricing.Url + "/",
                ["ReverseProxy:Clusters:orders:Destinations:default:Address"]        = Orders.Url + "/",
                ["ReverseProxy:Clusters:dispatch:Destinations:default:Address"]      = Dispatch.Url + "/",
                ["ReverseProxy:Clusters:tracking:Destinations:default:Address"]      = Tracking.Url + "/",
                ["ReverseProxy:Clusters:payments:Destinations:default:Address"]      = Payments.Url + "/",
                ["ReverseProxy:Clusters:notifications:Destinations:default:Address"] = Notifications.Url + "/",
            });
        });
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Identity.Stop();      Identity.Dispose();
            Merchants.Stop();     Merchants.Dispose();
            Catalog.Stop();       Catalog.Dispose();
            Pricing.Stop();       Pricing.Dispose();
            Orders.Stop();        Orders.Dispose();
            Dispatch.Stop();      Dispatch.Dispose();
            Tracking.Stop();      Tracking.Dispose();
            Payments.Stop();      Payments.Dispose();
            Notifications.Stop(); Notifications.Dispose();
        }
        base.Dispose(disposing);
    }
}
