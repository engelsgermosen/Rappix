using MassTransit;
using Microsoft.Extensions.Logging;
using Rappix.BuildingBlocks.Core.Time;
using Rappix.Contracts.Dispatch;
using Rappix.Contracts.Orders;
using Rappix.Tracking.Application.Abstractions;
using Rappix.Tracking.Domain.OrderTrackings;

namespace Rappix.Tracking.Infrastructure.Messaging;

/// <summary>
/// Cierra el tracking cuando el pedido alcanza un estado TERMINAL. Implementa 4 <c>IConsumer</c>
/// porque consumimos eventos de ambos servicios:
/// <list type="bullet">
/// <item><see cref="OrderDeliveredIntegrationEvent"/> (Dispatch, ancla de negocio "courier dejo el
///   pedido") y <see cref="OrderCompletedIntegrationEvent"/> (Orders, cierre de saga) mapean a
///   <see cref="TrackingStatus.Delivered"/>. Llegan en orden corto (microsegundos); el segundo es
///   no-op por idempotencia.</item>
/// <item><see cref="OrderCancelledIntegrationEvent"/> y <see cref="OrderFailedIntegrationEvent"/>
///   (Orders, tras compensacion) mapean a Cancelled/Failed con razon textual.</item>
/// </list>
/// En todos los casos: DELETE de la fila <c>CourierActiveOrder</c> por <c>OrderId</c> (puede no
/// existir si el pedido se cancelo pre-asignacion — el repo es no-op en ese caso).
/// </summary>
internal sealed partial class OrderTerminalEventsConsumer(
    IOrderTrackingRepository trackingRepo,
    ICourierActiveOrderRepository mappingRepo,
    IUnitOfWork unitOfWork,
    IClientNotifier notifier,
    IDateTimeProvider clock,
    ILogger<OrderTerminalEventsConsumer> logger)
    : IConsumer<OrderDeliveredIntegrationEvent>,
      IConsumer<OrderCompletedIntegrationEvent>,
      IConsumer<OrderCancelledIntegrationEvent>,
      IConsumer<OrderFailedIntegrationEvent>
{
    public Task Consume(ConsumeContext<OrderDeliveredIntegrationEvent> context) =>
        ApplyTerminalAsync(
            context.Message.OrderId,
            TrackingStatus.Delivered,
            reason: null,
            context.Message.DeliveredAtUtc,
            context.CancellationToken);

    public Task Consume(ConsumeContext<OrderCompletedIntegrationEvent> context) =>
        // No-op por idempotencia si OrderDelivered ya transiciono a Delivered.
        ApplyTerminalAsync(
            context.Message.OrderId,
            TrackingStatus.Delivered,
            reason: null,
            context.Message.CompletedAtUtc,
            context.CancellationToken);

    public Task Consume(ConsumeContext<OrderCancelledIntegrationEvent> context) =>
        ApplyTerminalAsync(
            context.Message.OrderId,
            TrackingStatus.Cancelled,
            reason: context.Message.Reason,
            context.Message.CancelledAtUtc,
            context.CancellationToken);

    public Task Consume(ConsumeContext<OrderFailedIntegrationEvent> context) =>
        ApplyTerminalAsync(
            context.Message.OrderId,
            TrackingStatus.Failed,
            reason: context.Message.Reason,
            context.Message.FailedAtUtc,
            context.CancellationToken);

    private async Task ApplyTerminalAsync(
        Guid orderId,
        TrackingStatus terminal,
        string? reason,
        DateTime occurredAtUtc,
        CancellationToken cancellationToken)
    {
        OrderTracking? tracking = await trackingRepo.GetByOrderIdAsync(orderId, cancellationToken);
        if (tracking is null)
        {
            // No-op: el tracking no fue proyectado (cancelacion muy temprana, race raro).
            LogTrackingNotFound(orderId, terminal.ToString());
            return;
        }

        // Si ya esta en un terminal, los Apply* son no-op (idempotencia del aggregate). Aun asi
        // ejecutamos el flujo: el RemoveByOrderIdAsync limpia la fila de mapping si quedo huerfana.
        bool wasTerminalBefore = tracking.CurrentStatus is TrackingStatus.Delivered or TrackingStatus.Cancelled or TrackingStatus.Failed;

        DateTime utcNow = clock.UtcNow;
        switch (terminal)
        {
            case TrackingStatus.Delivered:
                tracking.ApplyDelivered(occurredAtUtc);
                break;
            case TrackingStatus.Cancelled:
                tracking.ApplyCancelled(reason ?? string.Empty, occurredAtUtc);
                break;
            case TrackingStatus.Failed:
                tracking.ApplyFailed(reason ?? string.Empty, occurredAtUtc);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(terminal), terminal, "Estado no terminal.");
        }

        // Limpia el mapping courier->order. No-op si no existe (pedido cancelado pre-asignacion).
        await mappingRepo.RemoveByOrderIdAsync(orderId, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        string terminalName = terminal.ToString();
        string reasonText = reason ?? string.Empty;

        if (!wasTerminalBefore)
        {
            await notifier.PushStatus(orderId, terminalName, occurredAtUtc, reason, cancellationToken);
        }
        else
        {
            LogIdempotentTerminal(orderId, terminalName);
        }

        LogTerminalApplied(orderId, terminalName, reasonText);
        _ = utcNow; // referenciado para futuros logs UTC del consumer; sin uso semantico actual.
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Warning,
        Message = "Terminal recibido para orderId={OrderId} sin OrderTracking previo (status={Status}); skip.")]
    private partial void LogTrackingNotFound(Guid orderId, string status);

    [LoggerMessage(EventId = 2, Level = LogLevel.Information,
        Message = "Terminal idempotente para orderId={OrderId} (status={Status}); no se reempuja al cliente.")]
    private partial void LogIdempotentTerminal(Guid orderId, string status);

    [LoggerMessage(EventId = 3, Level = LogLevel.Information,
        Message = "Tracking cerrado para orderId={OrderId} con status={Status} (razon={Reason}).")]
    private partial void LogTerminalApplied(Guid orderId, string status, string reason);
}
