using MassTransit;
using Microsoft.Extensions.Logging;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.BuildingBlocks.Core.Time;
using Rappix.Contracts.Dispatch;
using Rappix.Contracts.Orders;
using Rappix.Dispatch.Application.Abstractions;
using Rappix.Dispatch.Domain.Couriers;

namespace Rappix.Dispatch.Infrastructure.Messaging;

/// <summary>
/// Libera al courier asignado al pedido cuando el pedido alcanza un estado TERMINAL. Consume los 3
/// terminales (no solo OrderCompleted) para cubrir: entrega normal, cancelacion tras asignar
/// (CompensatingStock en la saga) y fallo tras asignar.
/// </summary>
/// <remarks>
/// Por que <see cref="OrderDeliveredIntegrationEvent"/> y no <see cref="OrderCompletedIntegrationEvent"/>:
/// OrderDelivered es el anclaje de negocio ("el courier dejo el pedido"); OrderCompleted es solo el
/// estado terminal de orquestacion de la saga. Acoplar Dispatch a OrderCompleted hace que un futuro
/// estado intermedio (p. ej. ventana de rating) deje al courier Busy durante minutos extra.
/// Idempotente en doble capa: el inbox EF de MassTransit garantiza once-only por mensaje, y
/// <see cref="ReleaseByOrderAsync"/> es no-op si no hay asignacion activa (o ya esta liberada).
/// </remarks>
internal sealed partial class OrderTerminalEventsConsumer(
    ICourierRepository couriers,
    ICourierAssignmentRepository assignments,
    IRedisGeoIndex geo,
    IUnitOfWork unitOfWork,
    IDateTimeProvider clock,
    ILogger<OrderTerminalEventsConsumer> logger)
    : IConsumer<OrderDeliveredIntegrationEvent>,
      IConsumer<OrderCancelledIntegrationEvent>,
      IConsumer<OrderFailedIntegrationEvent>
{
    public Task Consume(ConsumeContext<OrderDeliveredIntegrationEvent> context) =>
        ReleaseByOrderAsync(context.Message.OrderId, "delivered", context.CancellationToken);

    public Task Consume(ConsumeContext<OrderCancelledIntegrationEvent> context) =>
        ReleaseByOrderAsync(context.Message.OrderId, "cancelled", context.CancellationToken);

    public Task Consume(ConsumeContext<OrderFailedIntegrationEvent> context) =>
        ReleaseByOrderAsync(context.Message.OrderId, "failed", context.CancellationToken);

    private async Task ReleaseByOrderAsync(Guid orderId, string reason, CancellationToken cancellationToken)
    {
        CourierAssignment? assignment = await assignments.GetActiveByOrderAsync(orderId, cancellationToken);
        if (assignment is null)
        {
            // No-op: el pedido nunca se asigno (cancelado antes de AwaitingCourier) o ya se libero.
            LogNoActive(logger, orderId, reason);
            return;
        }

        CourierProfile? courier = await couriers.GetByIdAsync(assignment.CourierId, cancellationToken);
        if (courier is null)
        {
            // Asignacion sin courier: estado imposible normalmente; rompemos la asignacion y seguimos.
            LogOrphanAssignment(logger, orderId, assignment.CourierId.Value);
            assignment.Release(clock.UtcNow, reason);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return;
        }

        DateTime now = clock.UtcNow;
        Result release = courier.Release(now);
        if (release.IsFailure)
        {
            // No deberia ocurrir (Release es idempotente y no falla); log y seguimos.
            LogReleaseFailed(logger, orderId, courier.Id.Value, release.Error.Code);
        }

        assignment.Release(now, reason);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        // De vuelta a Redis si tiene LastLocation conocida (vuelve a ser candidato a matching).
        // Fuera de la transaccion EF: si falla Redis, log y continua (el proximo report reconcilia).
        if (courier.Status == CourierStatus.Online && courier.LastLocation is not null)
        {
            await geo.AddOrUpdateAsync(courier.Id, courier.LastLocation.Latitude, courier.LastLocation.Longitude, cancellationToken);
        }

        LogReleased(logger, orderId, courier.Id.Value, reason);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Pedido {OrderId} alcanzo estado terminal '{Reason}' sin asignacion activa: no-op.")]
    private static partial void LogNoActive(ILogger logger, Guid orderId, string reason);

    [LoggerMessage(Level = LogLevel.Information, Message = "Courier {CourierId} liberado por pedido {OrderId} (razon: {Reason}).")]
    private static partial void LogReleased(ILogger logger, Guid orderId, Guid courierId, string reason);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Asignacion activa para el pedido {OrderId} apunta a un courier inexistente ({CourierId}): se cierra la asignacion sin tocar el courier.")]
    private static partial void LogOrphanAssignment(ILogger logger, Guid orderId, Guid courierId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Courier {CourierId} para el pedido {OrderId} no pudo liberarse (codigo {ErrorCode}); la asignacion se cierra igual.")]
    private static partial void LogReleaseFailed(ILogger logger, Guid orderId, Guid courierId, string errorCode);
}
