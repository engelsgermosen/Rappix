using MassTransit;
using MediatR;
using Rappix.Contracts.Dispatch;
using Rappix.Dispatch.Application.Abstractions;
using Rappix.Dispatch.Domain.Couriers.Events;

namespace Rappix.Dispatch.Application.DomainEventHandlers;

/// <summary>
/// Publica CourierLocationUpdatedIntegrationEvent (via outbox) cuando el courier reporta posicion.
/// Fase 7 (Tracking) lo consumira para hacer streaming al cliente.
/// </summary>
internal sealed class CourierLocationReportedDomainEventHandler(IPublishEndpoint publishEndpoint)
    : INotificationHandler<DomainEventNotification<CourierLocationReportedDomainEvent>>
{
    public Task Handle(DomainEventNotification<CourierLocationReportedDomainEvent> notification, CancellationToken cancellationToken)
    {
        CourierLocationReportedDomainEvent domainEvent = notification.DomainEvent;

        return publishEndpoint.Publish(
            new CourierLocationUpdatedIntegrationEvent
            {
                CourierId = domainEvent.CourierId.Value,
                Latitude = domainEvent.Latitude,
                Longitude = domainEvent.Longitude,
                ReportedAtUtc = domainEvent.ReportedAtUtc,
            },
            cancellationToken);
    }
}
