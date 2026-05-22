using MediatR;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.Catalog.Application.Abstractions;
using Rappix.Catalog.Application.Responses;
using Rappix.Catalog.Domain;
using Rappix.Catalog.Domain.Items;

namespace Rappix.Catalog.Application.Items.GetMine;

/// <summary>Devuelve un item del merchant validando pertenencia.</summary>
internal sealed class GetMyItemQueryHandler(IItemRepository items)
    : IRequestHandler<GetMyItemQuery, Result<ItemResponse>>
{
    public async Task<Result<ItemResponse>> Handle(GetMyItemQuery query, CancellationToken cancellationToken)
    {
        Item? item = await items.GetByIdAsync(new ItemId(query.ItemId), cancellationToken);
        if (item is null || item.IsDeleted)
        {
            return Result.Failure<ItemResponse>(ItemErrors.NotFound);
        }

        return item.MerchantId != query.MerchantId
            ? Result.Failure<ItemResponse>(ItemErrors.NotOwnedByMerchant)
            : ItemResponse.From(item);
    }
}
