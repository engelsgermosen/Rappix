using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Rappix.BuildingBlocks.Core.Time;
using Rappix.BuildingBlocks.Messaging.Extensions;
using Rappix.Tracking.Application.Abstractions;
using Rappix.Tracking.Infrastructure.Messaging;
using Rappix.Tracking.Infrastructure.Persistence;

namespace Rappix.Tracking.Infrastructure;

/// <summary>Registro de servicios de la capa de infraestructura.</summary>
public static class DependencyInjection
{
    /// <summary>
    /// Registra el DbContext, los repositorios (read + write), el reloj y MassTransit + outbox + el
    /// AddConfigureEndpointsCallback. Sin Redis: Tracking no usa <c>IdempotencyMiddleware</c> (solo
    /// GET REST + Hub SignalR), no usa Geo, sin SignalR backplane en Fase 7.
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

        AddMessaging(services, configuration);

        return services;
    }

    private static void AddMessaging(IServiceCollection services, IConfiguration configuration) =>
        services.AddRappixMessaging(configuration, serviceName: "tracking", configure: bus =>
        {
            // Consumers de eventos de Orders (saga publica OrderSubmitted + OrderAccepted).
            bus.AddConsumer<OrderSubmittedConsumer>();
            bus.AddConsumer<OrderAcceptedConsumer>();

            // Consumers de Dispatch: asignacion + stream de ubicaciones.
            bus.AddConsumer<CourierAssignedConsumer>();
            bus.AddConsumer<CourierLocationUpdatedConsumer>();

            // Consumers terminales (multi-IConsumer sobre los 4 eventos: OrderDelivered, OrderCompleted,
            // OrderCancelled, OrderFailed). Cierra el tracking + limpia el mapping courier->order.
            bus.AddConsumer<OrderTerminalEventsConsumer>();

            bus.AddEntityFrameworkOutbox<TrackingDbContext>(outbox =>
            {
                outbox.UsePostgres();
                outbox.UseBusOutbox();
            });

            // CRITICO (leccion de Orders aplicada desde el primer consumer): aplica el filtro de
            // outbox EF a CADA endpoint — envuelve cada consume en transaccion + SaveChanges + dedup
            // de inbox. Tracking no publica al bus en Fase 7, pero el inbox dedupica por MessageId,
            // lo cual ES necesario.
            bus.AddConfigureEndpointsCallback((context, _, cfg) => cfg.UseEntityFrameworkOutbox<TrackingDbContext>(context));
        });
}
