using MassTransit;
using MediatR;
using Rappix.Contracts.Orders;
using Rappix.Orders.Application.Abstractions;
using Rappix.Orders.Domain.Orders.Events;

namespace Rappix.Orders.Application.DomainEventHandlers;

/// <summary>
/// Publica OrderSubmittedIntegrationEvent (por el outbox) cuando se crea un pedido. Ese evento ARRANCA la
/// saga (se correlaciona por OrderId). Se publica en la misma transaccion que la insercion del pedido.
/// </summary>
internal sealed class OrderSubmittedDomainEventHandler(IPublishEndpoint publishEndpoint)
    : INotificationHandler<DomainEventNotification<OrderSubmittedDomainEvent>>
{
    public Task Handle(DomainEventNotification<OrderSubmittedDomainEvent> notification, CancellationToken cancellationToken)
    {
        OrderSubmittedDomainEvent domainEvent = notification.DomainEvent;

        return publishEndpoint.Publish(
            new OrderSubmittedIntegrationEvent
            {
                OrderId = domainEvent.OrderId.Value,
                CustomerUserId = domainEvent.CustomerUserId,
                MerchantId = domainEvent.MerchantId,
                QuoteId = domainEvent.QuoteId,
                TotalAmount = domainEvent.TotalAmount,
                Currency = domainEvent.Currency,
                DeliveryAddress = domainEvent.DeliveryAddress,
                DeliveryReference = domainEvent.DeliveryReference,
                DeliveryLatitude = domainEvent.DeliveryLatitude,
                DeliveryLongitude = domainEvent.DeliveryLongitude,
                PickupLatitude = domainEvent.PickupLatitude,
                PickupLongitude = domainEvent.PickupLongitude,
                MerchantName = domainEvent.MerchantName,
                // Proyeccion del snapshot de dominio al snapshot de contrato (mismos campos, distinto namespace).
                Lines = [.. domainEvent.Lines.Select(line => new OrderLineSnapshot(line.ItemName, line.Quantity))],
            },
            cancellationToken);
    }
}
