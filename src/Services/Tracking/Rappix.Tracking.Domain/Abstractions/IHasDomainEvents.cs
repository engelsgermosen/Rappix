using Rappix.BuildingBlocks.Core.Domain;

namespace Rappix.Tracking.Domain.Abstractions;

/// <summary>
/// Marcador no generico para que la infraestructura (DbContext) descubra agregados con
/// eventos de dominio pendientes sin conocer el tipo de Id. Tracking en Fase 7 no emite
/// integration events (es un read model puro), pero el contrato se mantiene por consistencia
/// con el resto del repo y por si en el futuro algun read model eleva eventos internos
/// (e.g. housekeeping de tracking expirados).
/// </summary>
public interface IHasDomainEvents
{
    /// <summary>Eventos de dominio pendientes de despachar.</summary>
    IReadOnlyCollection<IDomainEvent> DomainEvents { get; }

    /// <summary>Limpia los eventos de dominio tras despacharlos.</summary>
    void ClearDomainEvents();
}
