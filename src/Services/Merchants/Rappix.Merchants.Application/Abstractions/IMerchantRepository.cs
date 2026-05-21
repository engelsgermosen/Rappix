using NetTopologySuite.Geometries;
using Rappix.BuildingBlocks.Core.Pagination;
using Rappix.Merchants.Domain.Merchants;

namespace Rappix.Merchants.Application.Abstractions;

/// <summary>Acceso a la persistencia del agregado Merchant.</summary>
public interface IMerchantRepository
{
    /// <summary>Marca un nuevo merchant para insercion.</summary>
    void Add(Merchant merchant);

    /// <summary>Obtiene un merchant por Id, incluyendo zonas y horarios.</summary>
    Task<Merchant?> GetByIdAsync(MerchantId id, CancellationToken cancellationToken);

    /// <summary>Obtiene el merchant de un owner, incluyendo zonas y horarios.</summary>
    Task<Merchant?> GetByOwnerAsync(Guid ownerUserId, CancellationToken cancellationToken);

    /// <summary>Obtiene un merchant por slug, incluyendo zonas y horarios.</summary>
    Task<Merchant?> GetBySlugAsync(string slug, CancellationToken cancellationToken);

    /// <summary>Indica si el owner ya tiene un merchant (idempotencia del consumer).</summary>
    Task<bool> ExistsByOwnerAsync(Guid ownerUserId, CancellationToken cancellationToken);

    /// <summary>Indica si el slug ya esta en uso por otro merchant.</summary>
    Task<bool> SlugExistsAsync(string slug, MerchantId? excluding, CancellationToken cancellationToken);

    /// <summary>Indica si el RNC ya esta en uso por otro merchant.</summary>
    Task<bool> RncExistsAsync(string rnc, MerchantId? excluding, CancellationToken cancellationToken);

    /// <summary>Lista paginada de merchants por estado (admin).</summary>
    Task<PagedResult<Merchant>> ListByStatusAsync(MerchantStatus status, PagedRequest paging, CancellationToken cancellationToken);

    /// <summary>
    /// Merchants Active cuyas zonas de cobertura contienen el punto dado, ordenados por cercania.
    /// Poligonos via ST_Contains; circulos via ST_DWithin (geography, metros).
    /// </summary>
    Task<IReadOnlyList<Merchant>> SearchNearbyAsync(Point point, VerticalType? vertical, PagedRequest paging, CancellationToken cancellationToken);
}
