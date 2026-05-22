using MediatR;
using Rappix.BuildingBlocks.Core.Domain;

namespace Rappix.Orders.Application.Abstractions;

/// <summary>Envuelve un evento de dominio como notificacion de MediatR para que el DbContext lo despache.</summary>
public sealed class DomainEventNotification<TDomainEvent>(TDomainEvent domainEvent) : INotification
    where TDomainEvent : IDomainEvent
{
    /// <summary>Evento de dominio envuelto.</summary>
    public TDomainEvent DomainEvent { get; } = domainEvent;
}
