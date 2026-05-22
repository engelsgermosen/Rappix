using MediatR;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.BuildingBlocks.Core.Time;
using Rappix.Catalog.Application.Abstractions;
using Rappix.Catalog.Domain;
using Rappix.Catalog.Domain.Items;

namespace Rappix.Catalog.Application.Items.Delete;

/// <summary>Aplica el borrado logico de un item.</summary>
internal sealed class DeleteItemCommandHandler(
    IItemRepository items,
    IUnitOfWork unitOfWork,
    IDateTimeProvider clock)
    : IRequestHandler<DeleteItemCommand, Result>
{
    public async Task<Result> Handle(DeleteItemCommand command, CancellationToken cancellationToken)
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

        item.Delete(clock.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
