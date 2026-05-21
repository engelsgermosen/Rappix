using MassTransit;
using MediatR;
using Rappix.Contracts.Merchants;
using Rappix.Merchants.Application.Abstractions;
using Rappix.Merchants.Domain.Merchants.Events;

namespace Rappix.Merchants.Application.DomainEventHandlers;

/// <summary>Publica el evento de integracion MerchantApproved cuando un admin aprueba un merchant.</summary>
internal sealed class MerchantApprovedDomainEventHandler(IPublishEndpoint publishEndpoint)
    : INotificationHandler<DomainEventNotification<MerchantApprovedDomainEvent>>
{
    public Task Handle(DomainEventNotification<MerchantApprovedDomainEvent> notification, CancellationToken cancellationToken)
    {
        MerchantApprovedDomainEvent domainEvent = notification.DomainEvent;

        return publishEndpoint.Publish(
            new MerchantApprovedIntegrationEvent
            {
                MerchantId = domainEvent.MerchantId.Value,
                OwnerUserId = domainEvent.OwnerUserId,
                Name = domainEvent.Name,
                Slug = domainEvent.Slug,
                VerticalType = domainEvent.VerticalType.ToString(),
            },
            cancellationToken);
    }
}
