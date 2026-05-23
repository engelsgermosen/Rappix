using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Rappix.BuildingBlocks.Core.Time;
using Rappix.Tracking.Application.Abstractions;
using Rappix.Tracking.Infrastructure.Persistence;

namespace Rappix.Tracking.Infrastructure;

/// <summary>Registro de servicios de la capa de infraestructura.</summary>
public static class DependencyInjection
{
    /// <summary>
    /// Registra el DbContext, los repositorios (read + write) y el reloj. Sin Redis: Tracking no
    /// usa <c>IdempotencyMiddleware</c> (solo GET REST + Hub SignalR), no usa Geo, sin SignalR
    /// backplane en Fase 7. MassTransit + outbox + AddConfigureEndpointsCallback se anaden en el
    /// commit 6 junto con los consumers.
    /// </summary>
    public static IServiceCollection AddTrackingInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        string connectionString = configuration.GetConnectionString("TrackingDb")
            ?? throw new InvalidOperationException("Falta la cadena de conexion 'TrackingDb'.");

        services.AddDbContext<TrackingDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<IUnitOfWork>(provider => provider.GetRequiredService<TrackingDbContext>());

        services.AddScoped<IOrderTrackingReadRepository, OrderTrackingReadRepository>();
        services.AddScoped<IOrderTrackingRepository, OrderTrackingRepository>();
        services.AddScoped<ICourierActiveOrderRepository, CourierActiveOrderRepository>();

        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();

        return services;
    }
}
