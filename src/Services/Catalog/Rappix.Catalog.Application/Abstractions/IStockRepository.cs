using Rappix.Catalog.Domain.Items;

namespace Rappix.Catalog.Application.Abstractions;

/// <summary>Acceso a la persistencia del agregado <see cref="StockLevel"/> (concurrencia optimista via xmin).</summary>
public interface IStockRepository
{
    /// <summary>Marca un nuevo nivel de stock para insercion.</summary>
    void Add(StockLevel stock);

    /// <summary>Obtiene el nivel de stock de un item (o null si el item no lleva inventario).</summary>
    Task<StockLevel?> GetByItemIdAsync(ItemId itemId, CancellationToken cancellationToken);
}
