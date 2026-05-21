using MassTransit;
using MediatR;
using Rappix.Contracts.Identity;
using Rappix.Identity.Application.Abstractions;
using Rappix.Identity.Domain.Users.Events;

namespace Rappix.Identity.Application.Users.EventHandlers;

/// <summary>Publica el evento de integracion UserRegistered cuando se registra un usuario.</summary>
internal sealed class UserRegisteredDomainEventHandler(IPublishEndpoint publishEndpoint)
    : INotificationHandler<DomainEventNotification<UserRegisteredDomainEvent>>
{
    public Task Handle(DomainEventNotification<UserRegisteredDomainEvent> notification, CancellationToken cancellationToken)
    {
        UserRegisteredDomainEvent domainEvent = notification.DomainEvent;

        return publishEndpoint.Publish(
            new UserRegisteredIntegrationEvent
            {
                UserId = domainEvent.UserId.Value,
                Email = domainEvent.Email,
                FirstName = domainEvent.FirstName,
                LastName = domainEvent.LastName,
                UserType = domainEvent.UserType.ToString(),
                EmailConfirmed = domainEvent.EmailConfirmed,
            },
            cancellationToken);
    }
}
