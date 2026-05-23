using MediatR;
using Rappix.BuildingBlocks.Core.Domain;

namespace Rappix.Notifications.Infrastructure.Persistence;

/// <summary>
/// Envoltorio MediatR para domain events. Permite enviar el evento por <see cref="IPublisher"/>
/// sin que el aggregate (que solo conoce <see cref="IDomainEvent"/>) tenga que implementar
/// <see cref="INotification"/>. Hoy Notifications no emite eventos pero se mantiene el wiring por
/// consistencia con el resto del repo.
/// </summary>
public sealed record DomainEventNotification<TDomainEvent>(TDomainEvent DomainEvent) : INotification
    where TDomainEvent : IDomainEvent;
