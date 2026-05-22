using Microsoft.EntityFrameworkCore;
using Rappix.Catalog.Application.Abstractions;
using Rappix.Catalog.Domain.Items;

namespace Rappix.Catalog.Infrastructure.Persistence.Repositories;

/// <summary>Implementacion EF Core del repositorio del agregado <see cref="StockLevel"/>.</summary>
internal sealed class StockRepository(CatalogDbContext context) : IStockRepository
{
    public void Add(StockLevel stock) => context.StockLevels.Add(stock);

    public Task<StockLevel?> GetByItemIdAsync(ItemId itemId, CancellationToken cancellationToken) =>
        context.StockLevels.FirstOrDefaultAsync(stock => stock.Id == itemId, cancellationToken);
}
