using Rappix.BuildingBlocks.Core.Pagination;
using Rappix.Catalog.Domain.Items;

namespace Rappix.Catalog.Application.Abstractions;

/// <summary>Acceso a la persistencia del agregado <see cref="Item"/>.</summary>
public interface IItemRepository
{
    /// <summary>Marca un nuevo item para insercion.</summary>
    void Add(Item item);

    /// <summary>Obtiene un item por Id, incluyendo sus grupos de modificadores y opciones.</summary>
    Task<Item?> GetByIdAsync(ItemId id, CancellationToken cancellationToken);

    /// <summary>Lista paginada de los items (no borrados) de un merchant, opcionalmente filtrados por categoria.</summary>
    Task<PagedResult<Item>> ListByMerchantAsync(Guid merchantId, Guid? categoryId, PagedRequest paging, CancellationToken cancellationToken);

    /// <summary>
    /// Busqueda full-text (español, con fallback a ILIKE) por nombre/descripcion. Si onlyPurchasable
    /// es true, filtra items disponibles cuyo catalogo esta habilitado.
    /// </summary>
    Task<PagedResult<Item>> SearchAsync(
        string? text,
        Guid? merchantId,
        Guid? categoryId,
        bool onlyPurchasable,
        PagedRequest paging,
        CancellationToken cancellationToken);
}
