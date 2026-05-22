using MassTransit;
using MediatR;
using Rappix.Pricing.Application.Abstractions;
using Rappix.Pricing.Domain.Quotes.Events;
using Rappix.Contracts.Pricing;

namespace Rappix.Pricing.Application.DomainEventHandlers;

/// <summary>Publica el evento de integracion QuoteCreated cuando se crea una cotizacion.</summary>
internal sealed class QuoteCreatedDomainEventHandler(IPublishEndpoint publishEndpoint)
    : INotificationHandler<DomainEventNotification<QuoteCreatedDomainEvent>>
{
    public Task Handle(DomainEventNotification<QuoteCreatedDomainEvent> notification, CancellationToken cancellationToken)
    {
        QuoteCreatedDomainEvent domainEvent = notification.DomainEvent;

        return publishEndpoint.Publish(
            new QuoteCreatedIntegrationEvent
            {
                QuoteId = domainEvent.QuoteId.Value,
                CustomerUserId = domainEvent.CustomerUserId,
                MerchantId = domainEvent.MerchantId,
                Total = domainEvent.Total,
                Currency = domainEvent.Currency,
                ExpiresAtUtc = domainEvent.ExpiresAtUtc,
            },
            cancellationToken);
    }
}
