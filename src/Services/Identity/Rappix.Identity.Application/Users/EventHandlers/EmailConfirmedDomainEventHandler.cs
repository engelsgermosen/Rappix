using MassTransit;
using MediatR;
using Rappix.Contracts.Identity;
using Rappix.Identity.Application.Abstractions;
using Rappix.Identity.Domain.Users.Events;

namespace Rappix.Identity.Application.Users.EventHandlers;

/// <summary>Publica el evento de integracion UserEmailConfirmed cuando un usuario confirma su email.</summary>
internal sealed class EmailConfirmedDomainEventHandler(IPublishEndpoint publishEndpoint)
    : INotificationHandler<DomainEventNotification<EmailConfirmedDomainEvent>>
{
    public Task Handle(DomainEventNotification<EmailConfirmedDomainEvent> notification, CancellationToken cancellationToken)
    {
        EmailConfirmedDomainEvent domainEvent = notification.DomainEvent;

        return publishEndpoint.Publish(
            new UserEmailConfirmedIntegrationEvent
            {
                UserId = domainEvent.UserId.Value,
                Email = domainEvent.Email,
            },
            cancellationToken);
    }
}
