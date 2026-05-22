using MediatR;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.BuildingBlocks.Core.Storage;
using Rappix.Catalog.Application.Abstractions;
using Rappix.Catalog.Domain;
using Rappix.Catalog.Domain.Items;

namespace Rappix.Catalog.Application.Items.GetPhotoUrl;

/// <summary>Devuelve la URL firmada de la foto del item si la tiene.</summary>
internal sealed class GetItemPhotoUrlQueryHandler(IItemRepository items, IObjectStorage storage)
    : IRequestHandler<GetItemPhotoUrlQuery, Result<string>>
{
    private static readonly TimeSpan PhotoUrlLifetime = TimeSpan.FromHours(1);

    public async Task<Result<string>> Handle(GetItemPhotoUrlQuery query, CancellationToken cancellationToken)
    {
        Item? item = await items.GetByIdAsync(new ItemId(query.ItemId), cancellationToken);
        if (item is null || item.IsDeleted || item.PhotoObjectKey is null)
        {
            return Result.Failure<string>(ItemErrors.NotFound);
        }

        string url = await storage.GetPresignedUrlAsync(item.PhotoObjectKey, PhotoUrlLifetime, cancellationToken);
        return Result.Success(url);
    }
}
