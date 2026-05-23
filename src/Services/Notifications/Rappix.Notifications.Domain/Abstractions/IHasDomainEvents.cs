using Rappix.BuildingBlocks.Core.Domain;

namespace Rappix.Notifications.Domain.Abstractions;

/// <summary>
/// Marcador no generico para que la infraestructura (DbContext) descubra agregados con eventos de
/// dominio pendientes sin conocer el tipo de Id. Notifications en Fase 9 NO emite integration events
/// (es un projector + emisor de email), pero el contrato se mantiene por consistencia con el resto
/// del repo (mismo patron de <c>OrderTracking</c>) y por si en el futuro algun aggregate eleva
/// eventos internos (e.g. NotificationFailedDomainEvent para reintentos asincronos).
/// </summary>
public interface IHasDomainEvents
{
    /// <summary>Eventos de dominio pendientes de despachar.</summary>
    IReadOnlyCollection<IDomainEvent> DomainEvents { get; }

    /// <summary>Limpia los eventos de dominio tras despacharlos.</summary>
    void ClearDomainEvents();
}
