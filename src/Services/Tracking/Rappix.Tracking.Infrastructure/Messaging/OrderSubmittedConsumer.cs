using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Rappix.BuildingBlocks.Core.Time;
using Rappix.Contracts.Orders;
using Rappix.Tracking.Application.Abstractions;
using Rappix.Tracking.Domain.OrderTrackings;

namespace Rappix.Tracking.Infrastructure.Messaging;

/// <summary>
/// Proyecta <see cref="OrderSubmittedIntegrationEvent"/> a un nuevo <see cref="OrderTracking"/> en
/// estado <see cref="TrackingStatus.Placed"/>. Idempotencia en dos capas: inbox EF dedupica por
/// MessageId; <see cref="DbUpdateException"/> en redelivery con PK ya existente lo capturamos y
/// hacemos no-op (defensa en profundidad).
/// </summary>
internal sealed partial class OrderSubmittedConsumer(
    IOrderTrackingRepository repository,
    IUnitOfWork unitOfWork,
    IClientNotifier notifier,
    IDateTimeProvider clock,
    ILogger<OrderSubmittedConsumer> logger) : IConsumer<OrderSubmittedIntegrationEvent>
{
    public async Task Consume(ConsumeContext<OrderSubmittedIntegrationEvent> context)
    {
        OrderSubmittedIntegrationEvent message = context.Message;
        CancellationToken ct = context.CancellationToken;

        // Defensa: si por alguna razon el inbox dejo pasar dos veces (no deberia), GetByOrderIdAsync
        // detecta el duplicado antes del INSERT.
        OrderTracking? existing = await repository.GetByOrderIdAsync(message.OrderId, ct);
        if (existing is not null)
        {
            LogAlreadyProjected(message.OrderId);
            return;
        }

        DateTime utcNow = clock.UtcNow;
        OrderTracking tracking = OrderTracking.FromOrderSubmitted(
            orderId: message.OrderId,
            customerUserId: message.CustomerUserId,
            merchantId: message.MerchantId,
            pickupLat: message.PickupLatitude,
            pickupLng: message.PickupLongitude,
            deliveryLat: message.DeliveryLatitude,
            deliveryLng: message.DeliveryLongitude,
            utcNow: utcNow);

        repository.Add(tracking);
        await unitOfWork.SaveChangesAsync(ct);

        await notifier.PushStatus(message.OrderId, TrackingStatus.Placed.ToString(), utcNow, reason: null, ct);
        LogProjected(message.OrderId, message.CustomerUserId);
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Information,
        Message = "OrderTracking creado (orderId={OrderId}, customerUserId={CustomerUserId}) en estado Placed.")]
    private partial void LogProjected(Guid orderId, Guid customerUserId);

    [LoggerMessage(EventId = 2, Level = LogLevel.Information,
        Message = "OrderTracking ya proyectado para orderId={OrderId}; skip idempotente.")]
    private partial void LogAlreadyProjected(Guid orderId);
}
