using MediatR;
using Rappix.BuildingBlocks.Core.Pagination;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.Merchants.Application.Responses;

namespace Rappix.Merchants.Application.Admin.ListByStatus;

/// <summary>Lista paginada de merchants por estado (admin).</summary>
public sealed record ListMerchantsByStatusQuery(string Status, int Page, int PageSize)
    : IRequest<Result<PagedResult<MerchantResponse>>>;
