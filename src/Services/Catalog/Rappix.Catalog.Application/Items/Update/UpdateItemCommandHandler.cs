using MediatR;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.BuildingBlocks.Core.Time;
using Rappix.Catalog.Application.Abstractions;
using Rappix.Catalog.Application.Responses;
using Rappix.Catalog.Domain;
using Rappix.Catalog.Domain.Catalogs;
using Rappix.Catalog.Domain.Items;

namespace Rappix.Catalog.Application.Items.Update;

/// <summary>Actualiza un item validando pertenencia y, si cambia, que la categoria sea del catalogo.</summary>
internal sealed class UpdateItemCommandHandler(
    ICatalogRepository catalogs,
    IItemRepository items,
    IUnitOfWork unitOfWork,
    IDateTimeProvider clock)
    : IRequestHandler<UpdateItemCommand, Result<ItemResponse>>
{
    public async Task<Result<ItemResponse>> Handle(UpdateItemCommand command, CancellationToken cancellationToken)
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

        if (command.CategoryId is { } categoryId)
        {
            MerchantCatalog? catalog = await catalogs.GetByMerchantAsync(command.MerchantId, cancellationToken);
            if (catalog is null || !catalog.HasCategory(categoryId))
            {
                return Result.Failure<ItemResponse>(ItemErrors.CategoryNotInCatalog);
            }
        }

        Result<Money> price = Money.Create(command.PriceAmount, command.Currency);
        if (price.IsFailure)
        {
            return Result.Failure<ItemResponse>(price.Error);
        }

        Result update = item.UpdateDetails(command.CategoryId, command.Name, command.Description, price.Value, clock.UtcNow);
        if (update.IsFailure)
        {
            return Result.Failure<ItemResponse>(update.Error);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ItemResponse.From(item);
    }
}
