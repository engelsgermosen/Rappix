using MassTransit;
using MediatR;
using Rappix.Catalog.Application.Abstractions;
using Rappix.Catalog.Domain.Items;
using Rappix.Catalog.Domain.Items.Events;
using Rappix.Contracts.Catalog;

namespace Rappix.Catalog.Application.DomainEventHandlers;

/// <summary>Publica el evento de integracion StockDepleted cuando el stock de un item llega a cero.</summary>
internal sealed class StockDepletedDomainEventHandler(IPublishEndpoint publishEndpoint, IItemRepository items)
    : INotificationHandler<DomainEventNotification<StockDepletedDomainEvent>>
{
    public async Task Handle(DomainEventNotification<StockDepletedDomainEvent> notification, CancellationToken cancellationToken)
    {
        StockDepletedDomainEvent domainEvent = notification.DomainEvent;

        // El nombre vive en el agregado Item (separado del stock); se consulta para enriquecer el evento.
        Item? item = await items.GetByIdAsync(domainEvent.ItemId, cancellationToken);

        await publishEndpoint.Publish(
            new StockDepletedIntegrationEvent
            {
                MerchantId = domainEvent.MerchantId,
                ItemId = domainEvent.ItemId.Value,
                Name = item?.Name ?? string.Empty,
            },
            cancellationToken);
    }
}
