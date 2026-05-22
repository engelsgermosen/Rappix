using Microsoft.EntityFrameworkCore;
using Rappix.Catalog.Application.Abstractions;
using Rappix.Catalog.Domain.Catalogs;

namespace Rappix.Catalog.Infrastructure.Persistence.Repositories;

/// <summary>Implementacion EF Core del repositorio del agregado <see cref="MerchantCatalog"/>.</summary>
internal sealed class CatalogRepository(CatalogDbContext context) : ICatalogRepository
{
    public void Add(MerchantCatalog catalog) => context.Catalogs.Add(catalog);

    public Task<MerchantCatalog?> GetByMerchantAsync(Guid merchantId, CancellationToken cancellationToken) =>
        context.Catalogs
            .Include(catalog => catalog.Categories)
            .FirstOrDefaultAsync(catalog => catalog.MerchantId == merchantId, cancellationToken);

    public Task<bool> ExistsByMerchantAsync(Guid merchantId, CancellationToken cancellationToken) =>
        context.Catalogs.AnyAsync(catalog => catalog.MerchantId == merchantId, cancellationToken);
}
