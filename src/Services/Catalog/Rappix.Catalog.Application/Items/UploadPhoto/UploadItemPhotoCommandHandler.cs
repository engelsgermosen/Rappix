using MediatR;
using Rappix.BuildingBlocks.Core.Imaging;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.BuildingBlocks.Core.Storage;
using Rappix.BuildingBlocks.Core.Time;
using Rappix.Catalog.Application.Abstractions;
using Rappix.Catalog.Application.Responses;
using Rappix.Catalog.Domain;
using Rappix.Catalog.Domain.Items;

namespace Rappix.Catalog.Application.Items.UploadPhoto;

/// <summary>Valida la imagen, la sube a MinIO (bucket catalog-items) y guarda la key en el item.</summary>
internal sealed class UploadItemPhotoCommandHandler(
    IItemRepository items,
    IUnitOfWork unitOfWork,
    IObjectStorage storage,
    IImageValidator imageValidator,
    IDateTimeProvider clock)
    : IRequestHandler<UploadItemPhotoCommand, Result<ItemResponse>>
{
    public async Task<Result<ItemResponse>> Handle(UploadItemPhotoCommand command, CancellationToken cancellationToken)
    {
        Item? item = await items.GetByIdAsync(new ItemId(command.ItemId), cancellationToken);
        if (item is null || item.IsDeleted)
        {
            return Result.Failure<ItemResponse>(ItemErrors.NotFound);
        }

        if (item.MerchantId != command.MerchantId)
        {
            return Result.Failure<ItemResponse>(ItemErrors.NotOwnedByMerchant);
        }

        command.Content.Position = 0;
        Result<string> validation = await imageValidator.ValidateAsync(command.Content, cancellationToken);
        if (validation.IsFailure)
        {
            return Result.Failure<ItemResponse>(validation.Error);
        }

        string contentType = validation.Value;
        string key = $"items/{item.MerchantId}/{item.Id.Value}/photo.{ExtensionFor(contentType)}";

        command.Content.Position = 0;
        await storage.UploadAsync(key, command.Content, contentType, cancellationToken);

        item.SetPhoto(key, clock.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ItemResponse.From(item);
    }

    private static string ExtensionFor(string contentType) => contentType switch
    {
        "image/png" => "png",
        "image/jpeg" => "jpg",
        "image/webp" => "webp",
        _ => "bin",
    };
}
