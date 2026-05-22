using MediatR;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.BuildingBlocks.Core.Time;
using Rappix.Catalog.Application.Abstractions;
using Rappix.Catalog.Application.Items.Attributes;
using Rappix.Catalog.Application.Responses;
using Rappix.Catalog.Domain;
using Rappix.Catalog.Domain.Catalogs;
using Rappix.Catalog.Domain.Items;

namespace Rappix.Catalog.Application.Items.SetAttributes;

/// <summary>Valida los atributos contra el vertical del catalogo y los reemplaza en el item.</summary>
internal sealed class SetItemAttributesCommandHandler(
    ICatalogRepository catalogs,
    IItemRepository items,
    IUnitOfWork unitOfWork,
    IDateTimeProvider clock)
    : IRequestHandler<SetItemAttributesCommand, Result<ItemResponse>>
{
    public async Task<Result<ItemResponse>> Handle(SetItemAttributesCommand command, CancellationToken cancellationToken)
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

        MerchantCatalog? catalog = await catalogs.GetByMerchantAsync(command.MerchantId, cancellationToken);
        if (catalog is null)
        {
            return Result.Failure<ItemResponse>(CatalogErrors.NotFound);
        }

        Result attributeValidation = VerticalAttributeRules.Validate(catalog.VerticalType, command.Attributes);
        if (attributeValidation.IsFailure)
        {
            return Result.Failure<ItemResponse>(attributeValidation.Error);
        }

        Result update = item.ReplaceAttributes(command.Attributes, clock.UtcNow);
        if (update.IsFailure)
        {
            return Result.Failure<ItemResponse>(update.Error);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ItemResponse.From(item);
    }
}
