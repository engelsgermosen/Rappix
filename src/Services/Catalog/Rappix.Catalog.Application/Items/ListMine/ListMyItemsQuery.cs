using MediatR;
using Rappix.BuildingBlocks.Core.Pagination;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.Catalog.Application.Responses;

namespace Rappix.Catalog.Application.Items.ListMine;

/// <summary>Lista paginada de los items del merchant (vista owner), opcionalmente por categoria.</summary>
public sealed record ListMyItemsQuery(Guid MerchantId, Guid? CategoryId, int Page, int PageSize)
    : IRequest<Result<PagedResult<ItemResponse>>>;
