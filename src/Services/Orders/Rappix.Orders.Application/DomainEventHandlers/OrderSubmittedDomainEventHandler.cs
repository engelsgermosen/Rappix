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
                DeliveryLatitude = domainEvent.DeliveryLatitude,
                DeliveryLongitude = domainEvent.DeliveryLongitude,
            },
            cancellationToken);
    }
}
