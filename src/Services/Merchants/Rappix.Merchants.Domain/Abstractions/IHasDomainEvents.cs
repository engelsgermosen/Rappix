using Rappix.BuildingBlocks.Core.Domain;

namespace Rappix.Merchants.Domain.Abstractions;

/// <summary>
/// Marcador no generico para que la infraestructura (DbContext) descubra agregados con
/// eventos de dominio pendientes sin conocer el tipo de Id.
/// </summary>
public interface IHasDomainEvents
{
    /// <summary>Eventos de dominio pendientes de despachar.</summary>
    IReadOnlyCollection<IDomainEvent> DomainEvents { get; }

    /// <summary>Limpia los eventos de dominio tras despacharlos.</summary>
    void ClearDomainEvents();
}
