using MassTransit;
using Microsoft.Extensions.Logging;
using Rappix.BuildingBlocks.Core.Time;
using Rappix.Contracts.Dispatch;
using Rappix.Tracking.Application.Abstractions;
using Rappix.Tracking.Domain.CourierActiveOrders;
using Rappix.Tracking.Domain.OrderTrackings;

namespace Rappix.Tracking.Infrastructure.Messaging;

/// <summary>
/// Consume <see cref="CourierAssignedIntegrationEvent"/> (Dispatch claim atomico exitoso). Avanza el
/// read model (<c>OrderTracking.CurrentStatus = CourierAssigned</c>, captura <c>LastCourierId</c>) y
/// crea/actualiza el mapping <c>CourierActiveOrder</c> que <see cref="CourierLocationUpdatedConsumer"/>
/// usa para resolver "que pedido es de este courier" en un PK lookup.
/// </summary>
/// <remarks>
/// Carrera cubierta: si el mismo courier ya tiene una fila <c>CourierActiveOrder</c> (por una
/// asignacion previa cuyo terminal aun no se ha consumido), se actualiza la asignacion en vez de
/// fallar por PK duplicada — el invariante 1↔1 de Dispatch garantiza que solo una asignacion esta
/// vigente, pero la proyeccion no puede asumir el orden de los terminales vs el siguiente
/// CourierAssigned.
/// </remarks>
internal sealed partial class CourierAssignedConsumer(
    IOrderTrackingRepository trackingRepo,
    ICourierActiveOrderRepository mappingRepo,
    IUnitOfWork unitOfWork,
    IClientNotifier notifier,
    IDateTimeProvider clock,
    ILogger<CourierAssignedConsumer> logger) : IConsumer<CourierAssignedIntegrationEvent>
{
    public async Task Consume(ConsumeContext<CourierAssignedIntegrationEvent> context)
    {
        CourierAssignedIntegrationEvent message = context.Message;
        CancellationToken ct = context.CancellationToken;
        DateTime utcNow = clock.UtcNow;

        OrderTracking? tracking = await trackingRepo.GetByOrderIdAsync(message.OrderId, ct);
        if (tracking is null)
        {
            // Carrera improbable: el OrderSubmitted+CourierAssigned llegan en este orden por la saga.
            // Si esto pasa, log warn y salimos; un GET REST posterior reflejaria el estado correcto
            // cuando ambos eventos esten procesados.
            LogTrackingNotFound(message.OrderId);
            return;
        }

        tracking.ApplyCourierAssigned(message.CourierId, utcNow);

        // Mapping courier->order: upsert para cubrir reasignacion (mismo CourierId, distinto OrderId).
        CourierActiveOrder? existing = await mappingRepo.GetByCourierIdAsync(message.CourierId, ct);
        if (existing is null)
        {
            mappingRepo.Add(CourierActiveOrder.Create(message.CourierId, message.OrderId, utcNow));
        }
        else
        {
            existing.UpdateAssignment(message.OrderId, utcNow);
        }

        await unitOfWork.SaveChangesAsync(ct);

        await notifier.PushStatus(
            message.OrderId,
            TrackingStatus.CourierAssigned.ToString(),
            utcNow,
            reason: null,
            ct);
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Warning,
        Message = "CourierAssigned recibido para orderId={OrderId} sin OrderTracking previo; skip.")]
    private partial void LogTrackingNotFound(Guid orderId);
}
