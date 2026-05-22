using MassTransit;
using MediatR;
using Rappix.Pricing.Application.Abstractions;
using Rappix.Pricing.Domain.Coupons.Events;
using Rappix.Contracts.Pricing;

namespace Rappix.Pricing.Application.DomainEventHandlers;

/// <summary>Publica el evento de integracion CouponRedeemed cuando un cupon se redime al consumir una cotizacion.</summary>
internal sealed class CouponRedeemedDomainEventHandler(IPublishEndpoint publishEndpoint)
    : INotificationHandler<DomainEventNotification<CouponRedeemedDomainEvent>>
{
    public Task Handle(DomainEventNotification<CouponRedeemedDomainEvent> notification, CancellationToken cancellationToken)
    {
        CouponRedeemedDomainEvent domainEvent = notification.DomainEvent;

        return publishEndpoint.Publish(
            new CouponRedeemedIntegrationEvent
            {
                CouponId = domainEvent.CouponId.Value,
                Code = domainEvent.Code,
                CustomerUserId = domainEvent.CustomerUserId,
                QuoteId = domainEvent.QuoteId.Value,
                DiscountAmount = domainEvent.DiscountAmount,
            },
            cancellationToken);
    }
}
