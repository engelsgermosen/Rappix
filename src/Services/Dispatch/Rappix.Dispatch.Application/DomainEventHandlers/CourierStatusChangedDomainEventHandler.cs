using MassTransit;
using MediatR;
using Rappix.Contracts.Dispatch;
using Rappix.Dispatch.Application.Abstractions;
using Rappix.Dispatch.Domain.Couriers.Events;

namespace Rappix.Dispatch.Application.DomainEventHandlers;

/// <summary>
/// Publica CourierAvailabilityChangedIntegrationEvent (via outbox, en la misma transaccion del
/// SaveChanges) cuando cambia el estado del courier. Fase 7 (Tracking) lo consumira.
/// </summary>
internal sealed class CourierStatusChangedDomainEventHandler(IPublishEndpoint publishEndpoint)
    : INotificationHandler<DomainEventNotification<CourierStatusChangedDomainEvent>>
{
    public Task Handle(DomainEventNotification<CourierStatusChangedDomainEvent> notification, CancellationToken cancellationToken)
    {
        CourierStatusChangedDomainEvent domainEvent = notification.DomainEvent;

        return publishEndpoint.Publish(
            new CourierAvailabilityChangedIntegrationEvent
            {
                CourierId = domainEvent.CourierId.Value,
                Status = domainEvent.ToStatus.ToString(),
                ChangedAtUtc = domainEvent.ChangedAtUtc,
            },
            cancellationToken);
    }
}
