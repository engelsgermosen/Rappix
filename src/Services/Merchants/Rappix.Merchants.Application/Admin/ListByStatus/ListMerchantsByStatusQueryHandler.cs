using MediatR;
using Rappix.BuildingBlocks.Core.Pagination;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.Merchants.Application.Abstractions;
using Rappix.Merchants.Application.Responses;
using Rappix.Merchants.Domain.Merchants;

namespace Rappix.Merchants.Application.Admin.ListByStatus;

/// <summary>Lista merchants por estado y los proyecta a la vista completa.</summary>
internal sealed class ListMerchantsByStatusQueryHandler(IMerchantRepository merchants)
    : IRequestHandler<ListMerchantsByStatusQuery, Result<PagedResult<MerchantResponse>>>
{
    public async Task<Result<PagedResult<MerchantResponse>>> Handle(ListMerchantsByStatusQuery query, CancellationToken cancellationToken)
    {
        var status = Enum.Parse<MerchantStatus>(query.Status, ignoreCase: true);
        var paging = new PagedRequest(query.Page, query.PageSize);

        PagedResult<Merchant> page = await merchants.ListByStatusAsync(status, paging, cancellationToken);

        var mapped = new PagedResult<MerchantResponse>(
            [.. page.Items.Select(MerchantResponse.From)],
            page.Page,
            page.PageSize,
            page.TotalCount);

        return Result.Success(mapped);
    }
}
