using MassTransit;
using Microsoft.Extensions.Logging;
using Rappix.Contracts.Orders;
using Rappix.Tracking.Application.Abstractions;
using Rappix.Tracking.Domain.OrderTrackings;

namespace Rappix.Tracking.Infrastructure.Messaging;

/// <summary>
/// Aplica <see cref="OrderAcceptedIntegrationEvent"/> al read model: transiciona a
/// <see cref="TrackingStatus.MerchantAccepted"/> y empuja el cambio al cliente. La operacion es
/// idempotente (ApplyMerchantAccepted es no-op si el status ya esta avanzado).
/// </summary>
internal sealed partial class OrderAcceptedConsumer(
    IOrderTrackingRepository repository,
    IUnitOfWork unitOfWork,
    IClientNotifier notifier,
    ILogger<OrderAcceptedConsumer> logger) : IConsumer<OrderAcceptedIntegrationEvent>
{
    public async Task Consume(ConsumeContext<OrderAcceptedIntegrationEvent> context)
    {
        OrderAcceptedIntegrationEvent message = context.Message;
        CancellationToken ct = context.CancellationToken;

        OrderTracking? tracking = await repository.GetByOrderIdAsync(message.OrderId, ct);
        if (tracking is null)
        {
            // No deberia pasar (OrderSubmitted llega antes que OrderAccepted en la saga), pero si
            // por alguna razon esta carrera ocurre — log warn y skip; el cliente puede consultar
            // GET endpoint mas tarde o reintentar Subscribe.
            LogTrackingNotFound(message.OrderId);
            return;
        }

        tracking.ApplyMerchantAccepted(message.AcceptedAtUtc);
        await unitOfWork.SaveChangesAsync(ct);

        await notifier.PushStatus(
            message.OrderId,
            TrackingStatus.MerchantAccepted.ToString(),
            message.AcceptedAtUtc,
            reason: null,
            ct);
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Warning,
        Message = "OrderAccepted recibido para orderId={OrderId} sin OrderTracking previo; skip.")]
    private partial void LogTrackingNotFound(Guid orderId);
}
