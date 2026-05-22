using MediatR;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.BuildingBlocks.Core.Time;
using Rappix.Catalog.Application.Abstractions;
using Rappix.Catalog.Application.Responses;
using Rappix.Catalog.Domain;
using Rappix.Catalog.Domain.Items;

namespace Rappix.Catalog.Application.Items.AddModifier;

/// <summary>Agrega un grupo de modificadores a un item.</summary>
internal sealed class AddModifierCommandHandler(
    IItemRepository items,
    IUnitOfWork unitOfWork,
    IDateTimeProvider clock)
    : IRequestHandler<AddModifierCommand, Result<ItemResponse>>
{
    public async Task<Result<ItemResponse>> Handle(AddModifierCommand command, CancellationToken cancellationToken)
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

        Result<Modifier> modifier = item.AddModifier(
            command.Name,
            command.IsRequired,
            command.MinSelections,
            command.MaxSelections,
            clock.UtcNow);
        if (modifier.IsFailure)
        {
            return Result.Failure<ItemResponse>(modifier.Error);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ItemResponse.From(item);
    }
}
