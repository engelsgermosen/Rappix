using MediatR;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.BuildingBlocks.Core.Time;
using Rappix.Catalog.Application.Abstractions;
using Rappix.Catalog.Domain;
using Rappix.Catalog.Domain.Items;

namespace Rappix.Catalog.Application.Items.SetAvailability;

/// <summary>Cambia la disponibilidad publicada de un item.</summary>
internal sealed class SetItemAvailabilityCommandHandler(
    IItemRepository items,
    IUnitOfWork unitOfWork,
    IDateTimeProvider clock)
    : IRequestHandler<SetItemAvailabilityCommand, Result>
{
    public async Task<Result> Handle(SetItemAvailabilityCommand command, CancellationToken cancellationToken)
    {
        Item? item = await items.GetByIdAsync(new ItemId(command.ItemId), cancellationToken);
        if (item is null || item.IsDeleted)
        {
            return Result.Failure(ItemErrors.NotFound);
        }

        if (item.MerchantId != command.MerchantId)
        {
            return Result.Failure(ItemErrors.NotOwnedByMerchant);
        }

        Result update = command.Available ? item.MakeAvailable(clock.UtcNow) : item.MakeUnavailable(clock.UtcNow);
        if (update.IsFailure)
        {
            return update;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
