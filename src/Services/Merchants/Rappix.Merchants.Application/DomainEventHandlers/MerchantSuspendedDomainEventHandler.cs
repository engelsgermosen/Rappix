using MassTransit;
using MediatR;
using Rappix.Contracts.Merchants;
using Rappix.Merchants.Application.Abstractions;
using Rappix.Merchants.Domain.Merchants.Events;

namespace Rappix.Merchants.Application.DomainEventHandlers;

/// <summary>Publica el evento de integracion MerchantSuspended cuando un admin suspende un merchant.</summary>
internal sealed class MerchantSuspendedDomainEventHandler(IPublishEndpoint publishEndpoint)
    : INotificationHandler<DomainEventNotification<MerchantSuspendedDomainEvent>>
{
    public Task Handle(DomainEventNotification<MerchantSuspendedDomainEvent> notification, CancellationToken cancellationToken)
    {
        MerchantSuspendedDomainEvent domainEvent = notification.DomainEvent;

        return publishEndpoint.Publish(
            new MerchantSuspendedIntegrationEvent
            {
                MerchantId = domainEvent.MerchantId.Value,
                OwnerUserId = domainEvent.OwnerUserId,
                Reason = domainEvent.Reason,
            },
            cancellationToken);
    }
}
