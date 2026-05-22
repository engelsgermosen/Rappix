using MediatR;
using Rappix.BuildingBlocks.Core.Pagination;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.Catalog.Application.Abstractions;
using Rappix.Catalog.Application.Responses;
using Rappix.Catalog.Domain.Items;

namespace Rappix.Catalog.Application.Items.ListMine;

/// <summary>Lista paginada de los items del merchant.</summary>
internal sealed class ListMyItemsQueryHandler(IItemRepository items)
    : IRequestHandler<ListMyItemsQuery, Result<PagedResult<ItemResponse>>>
{
    public async Task<Result<PagedResult<ItemResponse>>> Handle(ListMyItemsQuery query, CancellationToken cancellationToken)
    {
        PagedResult<Item> page = await items.ListByMerchantAsync(
            query.MerchantId,
            query.CategoryId,
            new PagedRequest(query.Page, query.PageSize),
            cancellationToken);

        return new PagedResult<ItemResponse>(
            [.. page.Items.Select(ItemResponse.From)],
            page.Page,
            page.PageSize,
            page.TotalCount);
    }
}
