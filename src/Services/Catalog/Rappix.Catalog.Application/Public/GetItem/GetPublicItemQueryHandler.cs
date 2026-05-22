using MediatR;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.Catalog.Application.Abstractions;
using Rappix.Catalog.Application.Responses;
using Rappix.Catalog.Domain;
using Rappix.Catalog.Domain.Catalogs;
using Rappix.Catalog.Domain.Items;

namespace Rappix.Catalog.Application.Public.GetItem;

/// <summary>Devuelve un item al publico solo si esta disponible y el catalogo del merchant esta habilitado.</summary>
internal sealed class GetPublicItemQueryHandler(IItemRepository items, ICatalogRepository catalogs)
    : IRequestHandler<GetPublicItemQuery, Result<PublicItemResponse>>
{
    public async Task<Result<PublicItemResponse>> Handle(GetPublicItemQuery query, CancellationToken cancellationToken)
    {
        Item? item = await items.GetByIdAsync(new ItemId(query.ItemId), cancellationToken);
        if (item is null || item.IsDeleted || !item.IsAvailable)
        {
            return Result.Failure<PublicItemResponse>(ItemErrors.NotFound);
        }

        MerchantCatalog? catalog = await catalogs.GetByMerchantAsync(item.MerchantId, cancellationToken);
        return catalog is null || !catalog.IsEnabled
            ? Result.Failure<PublicItemResponse>(ItemErrors.NotFound)
            : PublicItemResponse.From(item);
    }
}
