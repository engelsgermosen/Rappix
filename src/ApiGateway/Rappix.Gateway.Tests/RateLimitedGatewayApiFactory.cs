using Microsoft.Extensions.Hosting;

namespace Rappix.Gateway.Tests;

/// <summary>
/// Fixture que arranca el gateway con limites de rate muy bajos para que los
/// <c>RateLimitTests</c> puedan disparar 429 en menos de 10 requests sin esperar 1 minuto.
/// </summary>
/// <remarks>
/// Patron: <see cref="CreateHost"/> setea las env vars de rate limit ANTES de construir el host
/// (que las lee en tiempo de configuracion en Program.cs) y las revierte INMEDIATAMENTE
/// despues. Combinado con <c>[assembly: CollectionBehavior(DisableTestParallelization = true)]</c>
/// (ver AssemblyInfo.cs), esto garantiza que ningun otro host construya con los limites bajos.
/// </remarks>
public sealed class RateLimitedGatewayApiFactory : GatewayApiFactory
{
    /// <summary>Permits para anonimos (por IP, fixed window).</summary>
    public const int AnonymousPermitLimit = 3;

    /// <summary>Permits para autenticados (por sub, sliding window).</summary>
    public const int AuthenticatedPermitLimit = 5;

    /// <summary>Window en segundos (compartido por ambas politicas en tests).</summary>
    public const int WindowSeconds = 10;

    protected override IHost CreateHost(IHostBuilder builder)
    {
        Environment.SetEnvironmentVariable("RateLimit__Anonymous__PermitLimit",       AnonymousPermitLimit.ToString());
        Environment.SetEnvironmentVariable("RateLimit__Anonymous__WindowSeconds",     WindowSeconds.ToString());
        Environment.SetEnvironmentVariable("RateLimit__Authenticated__PermitLimit",   AuthenticatedPermitLimit.ToString());
        Environment.SetEnvironmentVariable("RateLimit__Authenticated__WindowSeconds", WindowSeconds.ToString());
        try
        {
            return base.CreateHost(builder);
        }
        finally
        {
            Environment.SetEnvironmentVariable("RateLimit__Anonymous__PermitLimit",       null);
            Environment.SetEnvironmentVariable("RateLimit__Anonymous__WindowSeconds",     null);
            Environment.SetEnvironmentVariable("RateLimit__Authenticated__PermitLimit",   null);
            Environment.SetEnvironmentVariable("RateLimit__Authenticated__WindowSeconds", null);
        }
    }
}
