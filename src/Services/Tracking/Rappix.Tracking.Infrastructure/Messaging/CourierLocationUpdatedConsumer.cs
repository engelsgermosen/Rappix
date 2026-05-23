using MassTransit;
using Microsoft.Extensions.Logging;
using Rappix.Contracts.Dispatch;
using Rappix.Tracking.Application.Abstractions;
using Rappix.Tracking.Domain.CourierActiveOrders;
using Rappix.Tracking.Domain.OrderTrackings;

namespace Rappix.Tracking.Infrastructure.Messaging;

/// <summary>
/// El consumer del hot path: recibe <see cref="CourierLocationUpdatedIntegrationEvent"/> (cada POST
/// /me/location del courier en Dispatch publica uno), resuelve <c>CourierId -> OrderId</c> via PK
/// lookup en <c>CourierActiveOrder</c>, aplica el location update al read model y empuja al cliente.
/// </summary>
/// <remarks>
/// Tres caminos felices/silenciosos:
/// <list type="bullet">
/// <item>Sin mapping (courier no asignado): salida silenciosa (log debug). El courier puede estar
///   reportando ubicacion en idle (Online, sin pedido). No es un error.</item>
/// <item>Mapping pero sin OrderTracking (inconsistencia rara, log warn): salida limpia.</item>
/// <item>Location stale por timestamp (ApplyLocation devuelve false): NO save ni push. Evita pintar
///   saltos al cliente cuando los eventos llegan fuera de orden.</item>
/// </list>
/// </remarks>
internal sealed partial class CourierLocationUpdatedConsumer(
    IOrderTrackingRepository trackingRepo,
    ICourierActiveOrderRepository mappingRepo,
    IUnitOfWork unitOfWork,
    IClientNotifier notifier,
    ILogger<CourierLocationUpdatedConsumer> logger) : IConsumer<CourierLocationUpdatedIntegrationEvent>
{
    public async Task Consume(ConsumeContext<CourierLocationUpdatedIntegrationEvent> context)
    {
        CourierLocationUpdatedIntegrationEvent message = context.Message;
        CancellationToken ct = context.CancellationToken;

        CourierActiveOrder? mapping = await mappingRepo.GetByCourierIdAsync(message.CourierId, ct);
        if (mapping is null)
        {
            // Courier reportando ubicacion en idle (sin pedido asignado) — caso normal.
            LogNoMapping(message.CourierId);
            return;
        }

        OrderTracking? tracking = await trackingRepo.GetByOrderIdAsync(mapping.OrderId, ct);
        if (tracking is null)
        {
            // Inconsistencia (mapping sin tracking): no deberia pasar; loggear y salir.
            LogMappingWithoutTracking(message.CourierId, mapping.OrderId);
            return;
        }

        bool applied = tracking.ApplyLocation(
            message.CourierId,
            message.Latitude,
            message.Longitude,
            message.ReportedAtUtc);

        if (!applied)
        {
            // Stale por timestamp o courier no-coincidente: sin save ni push.
            LogStaleLocation(mapping.OrderId, message.CourierId, message.ReportedAtUtc);
            return;
        }

        await unitOfWork.SaveChangesAsync(ct);

        await notifier.PushLocation(
            mapping.OrderId,
            message.Latitude,
            message.Longitude,
            message.ReportedAtUtc,
            message.CourierId,
            ct);
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Debug,
        Message = "CourierLocation ignorada: courier {CourierId} sin asignacion activa.")]
    private partial void LogNoMapping(Guid courierId);

    [LoggerMessage(EventId = 2, Level = LogLevel.Warning,
        Message = "Mapping courier->order presente (courierId={CourierId}, orderId={OrderId}) pero OrderTracking no existe.")]
    private partial void LogMappingWithoutTracking(Guid courierId, Guid orderId);

    [LoggerMessage(EventId = 3, Level = LogLevel.Debug,
        Message = "CourierLocation descartada por stale (orderId={OrderId}, courierId={CourierId}, reportedAtUtc={ReportedAtUtc}).")]
    private partial void LogStaleLocation(Guid orderId, Guid courierId, DateTime reportedAtUtc);
}
