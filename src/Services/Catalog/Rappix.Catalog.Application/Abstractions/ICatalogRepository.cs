using Rappix.Catalog.Domain.Catalogs;

namespace Rappix.Catalog.Application.Abstractions;

/// <summary>Acceso a la persistencia del agregado <see cref="MerchantCatalog"/>.</summary>
public interface ICatalogRepository
{
    /// <summary>Marca un nuevo catalogo para insercion.</summary>
    void Add(MerchantCatalog catalog);

    /// <summary>Obtiene el catalogo de un merchant, incluyendo sus categorias.</summary>
    Task<MerchantCatalog?> GetByMerchantAsync(Guid merchantId, CancellationToken cancellationToken);

    /// <summary>Resuelve el MerchantId del catalogo de un usuario propietario (o null si no tiene catalogo).</summary>
    Task<Guid?> GetMerchantIdByOwnerAsync(Guid ownerUserId, CancellationToken cancellationToken);

    /// <summary>Indica si el merchant ya tiene catalogo (idempotencia del consumer).</summary>
    Task<bool> ExistsByMerchantAsync(Guid merchantId, CancellationToken cancellationToken);
}
