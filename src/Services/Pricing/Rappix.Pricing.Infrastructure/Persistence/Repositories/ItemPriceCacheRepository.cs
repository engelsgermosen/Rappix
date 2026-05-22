using Microsoft.EntityFrameworkCore;
using Rappix.Pricing.Application.Abstractions;
using Rappix.Pricing.Domain.ItemPrices;

namespace Rappix.Pricing.Infrastructure.Persistence.Repositories;

/// <summary>Implementacion EF Core del cache local de precios de item.</summary>
internal sealed class ItemPriceCacheRepository(PricingDbContext context) : IItemPriceCacheRepository
{
    public Task<ItemPriceCache?> GetByItemIdAsync(Guid itemId, CancellationToken cancellationToken) =>
        context.ItemPrices.FirstOrDefaultAsync(cache => cache.ItemId == itemId, cancellationToken);

    public void Add(ItemPriceCache cache) => context.ItemPrices.Add(cache);
}
