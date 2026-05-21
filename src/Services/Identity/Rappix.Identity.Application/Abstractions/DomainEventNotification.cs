using MediatR;
using Rappix.BuildingBlocks.Core.Domain;

namespace Rappix.Identity.Application.Abstractions;

/// <summary>
/// Envuelve un evento de dominio como notificacion de MediatR, manteniendo la capa de dominio
/// libre de dependencias de MediatR. El interceptor de EF Core publica estas notificaciones.
/// </summary>
/// <typeparam name="TDomainEvent">Tipo concreto del evento de dominio.</typeparam>
public sealed class DomainEventNotification<TDomainEvent>(TDomainEvent domainEvent) : INotification
    where TDomainEvent : IDomainEvent
{
    /// <summary>Evento de dominio envuelto.</summary>
    public TDomainEvent DomainEvent { get; } = domainEvent;
}
