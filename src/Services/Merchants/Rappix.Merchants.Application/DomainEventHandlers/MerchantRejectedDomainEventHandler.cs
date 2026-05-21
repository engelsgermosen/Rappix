using MassTransit;
using MediatR;
using Rappix.Contracts.Merchants;
using Rappix.Merchants.Application.Abstractions;
using Rappix.Merchants.Domain.Merchants.Events;

namespace Rappix.Merchants.Application.DomainEventHandlers;

/// <summary>Publica el evento de integracion MerchantRejected cuando un admin rechaza la aprobacion.</summary>
internal sealed class MerchantRejectedDomainEventHandler(IPublishEndpoint publishEndpoint)
    : INotificationHandler<DomainEventNotification<MerchantRejectedDomainEvent>>
{
    public Task Handle(DomainEventNotification<MerchantRejectedDomainEvent> notification, CancellationToken cancellationToken)
    {
        MerchantRejectedDomainEvent domainEvent = notification.DomainEvent;

        return publishEndpoint.Publish(
            new MerchantRejectedIntegrationEvent
            {
                MerchantId = domainEvent.MerchantId.Value,
                OwnerUserId = domainEvent.OwnerUserId,
                Reason = domainEvent.Reason,
            },
            cancellationToken);
    }
}
