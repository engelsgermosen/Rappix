using Microsoft.EntityFrameworkCore;
using NpgsqlTypes;
using Rappix.BuildingBlocks.Core.Pagination;
using Rappix.Catalog.Application.Abstractions;
using Rappix.Catalog.Domain.Items;

namespace Rappix.Catalog.Infrastructure.Persistence.Repositories;

/// <summary>Implementacion EF Core del repositorio del agregado <see cref="Item"/> (incluye busqueda full-text).</summary>
internal sealed class ItemRepository(CatalogDbContext context) : IItemRepository
{
    public void Add(Item item) => context.Items.Add(item);

    public Task<Item?> GetByIdAsync(ItemId id, CancellationToken cancellationToken) =>
        WithChildren(context.Items).FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

    public async Task<PagedResult<Item>> ListByMerchantAsync(Guid merchantId, Guid? categoryId, PagedRequest paging, CancellationToken cancellationToken)
    {
        IQueryable<Item> query = context.Items.Where(item => item.MerchantId == merchantId);
        if (categoryId is { } category)
        {
            query = query.Where(item => item.CategoryId == category);
        }

        return await ToPagedResultAsync(query, paging, cancellationToken);
    }

    public async Task<PagedResult<Item>> SearchAsync(
        string? text,
        Guid? merchantId,
        Guid? categoryId,
        bool onlyPurchasable,
        PagedRequest paging,
        CancellationToken cancellationToken)
    {
        IQueryable<Item> query = context.Items;

        if (merchantId is { } merchant)
        {
            query = query.Where(item => item.MerchantId == merchant);
        }

        if (categoryId is { } category)
        {
            query = query.Where(item => item.CategoryId == category);
        }

        if (onlyPurchasable)
        {
            query = query.Where(item => item.IsAvailable
                && context.Catalogs.Any(catalog => catalog.MerchantId == item.MerchantId && catalog.IsEnabled));
        }

        if (!string.IsNullOrWhiteSpace(text))
        {
            string term = text.Trim();
            // Full-text español sobre la columna generada search_vector (acelerada por indice GIN).
            query = query.Where(item =>
                EF.Property<NpgsqlTsVector>(item, "SearchVector")
                    .Matches(EF.Functions.PlainToTsQuery("spanish", term)));
        }

        return await ToPagedResultAsync(query, paging, cancellationToken);
    }

    private static async Task<PagedResult<Item>> ToPagedResultAsync(IQueryable<Item> query, PagedRequest paging, CancellationToken cancellationToken)
    {
        int total = await query.CountAsync(cancellationToken);
        List<Item> items = await WithChildren(query)
            .OrderByDescending(item => item.CreatedAtUtc)
            .Skip(paging.Skip)
            .Take(paging.Take)
            .ToListAsync(cancellationToken);

        return new PagedResult<Item>(items, paging.Page, paging.Take, total);
    }

    private static IQueryable<Item> WithChildren(IQueryable<Item> query) =>
        query
            .Include(item => item.Modifiers)
            .ThenInclude(modifier => modifier.Options);
}
