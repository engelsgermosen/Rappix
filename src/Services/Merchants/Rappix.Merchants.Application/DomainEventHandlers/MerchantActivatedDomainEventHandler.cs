using MassTransit;
using MediatR;
using Rappix.Contracts.Merchants;
using Rappix.Merchants.Application.Abstractions;
using Rappix.Merchants.Domain.Merchants.Events;

namespace Rappix.Merchants.Application.DomainEventHandlers;

/// <summary>Publica el evento de integracion MerchantActivated cuando un merchant suspendido se reactiva.</summary>
internal sealed class MerchantActivatedDomainEventHandler(IPublishEndpoint publishEndpoint)
    : INotificationHandler<DomainEventNotification<MerchantActivatedDomainEvent>>
{
    public Task Handle(DomainEventNotification<MerchantActivatedDomainEvent> notification, CancellationToken cancellationToken)
    {
        MerchantActivatedDomainEvent domainEvent = notification.DomainEvent;

        return publishEndpoint.Publish(
            new MerchantActivatedIntegrationEvent
            {
                MerchantId = domainEvent.MerchantId.Value,
                OwnerUserId = domainEvent.OwnerUserId,
            },
            cancellationToken);
    }
}
