using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Rappix.Dispatch.Application.Abstractions;
using Rappix.Dispatch.Domain.Couriers;

namespace Rappix.Dispatch.Infrastructure.Redis;

/// <summary>
/// Hosted service que rehidrata el geo set de Redis (<c>dispatch:couriers:geo</c>) desde la BD al
/// arrancar el servicio: si Redis se vacia (volume flush, restart sin persistencia) los couriers
/// Online con LastLocation conocida vuelven al set automaticamente para ser candidatos al matching.
/// </summary>
/// <remarks>
/// Orden critico: corre DESPUES de aplicar las migraciones (Program.cs lo hace antes de Build) y
/// ANTES del bus (los consumers no necesitan el geo set para funcionar, pero el primer CourierRequested
/// que llegue tras un flush de Redis encontraria un set vacio y devolveria CourierUnavailable falso
/// si la rehidratacion no termino).
/// </remarks>
internal sealed partial class RedisGeoRehydrationService(
    IServiceScopeFactory scopeFactory,
    ILogger<RedisGeoRehydrationService> logger)
    : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using IServiceScope scope = scopeFactory.CreateScope();
        ICourierRepository couriers = scope.ServiceProvider.GetRequiredService<ICourierRepository>();
        IRedisGeoIndex geo = scope.ServiceProvider.GetRequiredService<IRedisGeoIndex>();

        try
        {
            IReadOnlyList<CourierProfile> online = await couriers.ListOnlineWithLocationAsync(cancellationToken);
            foreach (CourierProfile courier in online)
            {
                // LastLocation no es null por la condicion del repositorio.
                LastLocation location = courier.LastLocation!;
                await geo.AddOrUpdateAsync(courier.Id, location.Latitude, location.Longitude, cancellationToken);
            }

            LogRehydrated(logger, online.Count);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Mejor arrancar con un geo set parcial que crashear: el proximo report de cada courier reconcilia.
            LogFailed(logger, ex);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    [LoggerMessage(Level = LogLevel.Information, Message = "Redis Geo rehidratado: {Count} couriers Online con LastLocation conocida.")]
    private static partial void LogRehydrated(ILogger logger, int count);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Fallo la rehidratacion de Redis Geo al arranque. El primer reporte de cada courier reconciliara.")]
    private static partial void LogFailed(ILogger logger, Exception exception);
}
