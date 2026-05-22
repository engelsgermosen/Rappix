using MediatR;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.BuildingBlocks.Core.Time;
using Rappix.Catalog.Application.Abstractions;
using Rappix.Catalog.Application.Responses;
using Rappix.Catalog.Domain;
using Rappix.Catalog.Domain.Items;

namespace Rappix.Catalog.Application.Items.AddModifierOption;

/// <summary>Agrega una opcion a un grupo de modificadores existente.</summary>
internal sealed class AddModifierOptionCommandHandler(
    IItemRepository items,
    IUnitOfWork unitOfWork,
    IDateTimeProvider clock)
    : IRequestHandler<AddModifierOptionCommand, Result<ItemResponse>>
{
    public async Task<Result<ItemResponse>> Handle(AddModifierOptionCommand command, CancellationToken cancellationToken)
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

        Result added = item.AddModifierOption(command.ModifierId, command.Name, command.PriceDelta, clock.UtcNow);
        if (added.IsFailure)
        {
            return Result.Failure<ItemResponse>(added.Error);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ItemResponse.From(item);
    }
}
