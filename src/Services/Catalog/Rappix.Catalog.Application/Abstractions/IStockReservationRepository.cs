using Rappix.Catalog.Domain.Items;

namespace Rappix.Catalog.Application.Abstractions;

/// <summary>Acceso a la persistencia de los holds de stock (<see cref="StockReservation"/>).</summary>
public interface IStockReservationRepository
{
    /// <summary>Marca un nuevo hold para insercion.</summary>
    void Add(StockReservation reservation);

    /// <summary>Obtiene todos los holds de un pedido (cualquier estado), para confirmar/liberar e idempotencia.</summary>
    Task<IReadOnlyList<StockReservation>> GetByOrderIdAsync(Guid orderId, CancellationToken cancellationToken);

    /// <summary>Obtiene los holds en estado <see cref="StockReservationStatus.Held"/> de un conjunto de items, para el barrido perezoso de vencidos.</summary>
    Task<IReadOnlyList<StockReservation>> GetHeldByItemIdsAsync(IReadOnlyCollection<ItemId> itemIds, CancellationToken cancellationToken);
}
