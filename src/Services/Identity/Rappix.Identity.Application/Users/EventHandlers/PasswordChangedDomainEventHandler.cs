using MassTransit;
using MediatR;
using Rappix.Contracts.Identity;
using Rappix.Identity.Application.Abstractions;
using Rappix.Identity.Domain.Users.Events;

namespace Rappix.Identity.Application.Users.EventHandlers;

/// <summary>Publica el evento de integracion PasswordChanged cuando un usuario cambia su contrasena.</summary>
internal sealed class PasswordChangedDomainEventHandler(IPublishEndpoint publishEndpoint)
    : INotificationHandler<DomainEventNotification<PasswordChangedDomainEvent>>
{
    public Task Handle(DomainEventNotification<PasswordChangedDomainEvent> notification, CancellationToken cancellationToken)
    {
        PasswordChangedDomainEvent domainEvent = notification.DomainEvent;

        return publishEndpoint.Publish(
            new PasswordChangedIntegrationEvent
            {
                UserId = domainEvent.UserId.Value,
                Email = domainEvent.Email,
            },
            cancellationToken);
    }
}
