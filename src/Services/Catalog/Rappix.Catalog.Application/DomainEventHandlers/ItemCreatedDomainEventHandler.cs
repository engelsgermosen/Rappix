using MassTransit;
using MediatR;
using Rappix.Catalog.Application.Abstractions;
using Rappix.Catalog.Domain.Items.Events;
using Rappix.Contracts.Catalog;

namespace Rappix.Catalog.Application.DomainEventHandlers;

/// <summary>Publica el evento de integracion ItemCreated cuando se crea un item.</summary>
internal sealed class ItemCreatedDomainEventHandler(IPublishEndpoint publishEndpoint)
    : INotificationHandler<DomainEventNotification<ItemCreatedDomainEvent>>
{
    public Task Handle(DomainEventNotification<ItemCreatedDomainEvent> notification, CancellationToken cancellationToken)
    {
        ItemCreatedDomainEvent domainEvent = notification.DomainEvent;

        return publishEndpoint.Publish(
            new ItemCreatedIntegrationEvent
            {
                MerchantId = domainEvent.MerchantId,
                ItemId = domainEvent.ItemId.Value,
                Name = domainEvent.Name,
                BasePriceAmount = domainEvent.PriceAmount,
                Currency = domainEvent.Currency,
            },
            cancellationToken);
    }
}
