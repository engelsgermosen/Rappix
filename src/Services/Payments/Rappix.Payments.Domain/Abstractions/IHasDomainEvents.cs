using Rappix.BuildingBlocks.Core.Domain;

namespace Rappix.Payments.Domain.Abstractions;

/// <summary>
/// Marcador no generico para que la infraestructura (DbContext) descubra agregados con eventos de
/// dominio pendientes sin conocer el tipo de Id. Payments en Fase 8 NO emite domain events (los
/// integration events los publican los consumers directamente con el contrato de
/// <c>Rappix.Contracts.Payments</c>), pero el contrato se mantiene por consistencia con Tracking
/// y por si en el futuro algun aggregate eleva eventos internos.
/// </summary>
public interface IHasDomainEvents
{
    /// <summary>Eventos de dominio pendientes de despachar.</summary>
    IReadOnlyCollection<IDomainEvent> DomainEvents { get; }

    /// <summary>Limpia los eventos de dominio tras despacharlos.</summary>
    void ClearDomainEvents();
}
