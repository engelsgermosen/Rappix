using MediatR;
using Rappix.BuildingBlocks.Core.Pagination;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.Catalog.Application.Abstractions;
using Rappix.Catalog.Application.Responses;
using Rappix.Catalog.Domain.Items;

namespace Rappix.Catalog.Application.Public.Search;

/// <summary>Busqueda publica: solo items comprables (disponibles y de catalogos habilitados).</summary>
internal sealed class SearchItemsQueryHandler(IItemRepository items)
    : IRequestHandler<SearchItemsQuery, Result<PagedResult<PublicItemResponse>>>
{
    public async Task<Result<PagedResult<PublicItemResponse>>> Handle(SearchItemsQuery query, CancellationToken cancellationToken)
    {
        PagedResult<Item> page = await items.SearchAsync(
            query.Text,
            query.MerchantId,
            query.CategoryId,
            onlyPurchasable: true,
            new PagedRequest(query.Page, query.PageSize),
            cancellationToken);

        return new PagedResult<PublicItemResponse>(
            [.. page.Items.Select(PublicItemResponse.From)],
            page.Page,
            page.PageSize,
            page.TotalCount);
    }
}
