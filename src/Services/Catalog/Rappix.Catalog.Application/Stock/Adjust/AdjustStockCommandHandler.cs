using MediatR;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.BuildingBlocks.Core.Time;
using Rappix.Catalog.Application.Abstractions;
using Rappix.Catalog.Application.Responses;
using Rappix.Catalog.Domain;
using Rappix.Catalog.Domain.Items;

namespace Rappix.Catalog.Application.Stock.Adjust;

/// <summary>Fija o repone el stock de un item. Traduce conflictos de concurrencia (xmin) a Error.Conflict.</summary>
internal sealed class AdjustStockCommandHandler(
    IStockRepository stocks,
    IUnitOfWork unitOfWork,
    IDateTimeProvider clock)
    : IRequestHandler<AdjustStockCommand, Result<StockResponse>>
{
    public async Task<Result<StockResponse>> Handle(AdjustStockCommand command, CancellationToken cancellationToken)
    {
        StockLevel? stock = await stocks.GetByItemIdAsync(new ItemId(command.ItemId), cancellationToken);
        if (stock is null)
        {
            return Result.Failure<StockResponse>(StockErrors.NotFound);
        }

        if (stock.MerchantId != command.MerchantId)
        {
            return Result.Failure<StockResponse>(ItemErrors.NotOwnedByMerchant);
        }

        Result adjustment = command.Mode == StockAdjustmentMode.Set
            ? stock.SetQuantity(command.Quantity, clock.UtcNow)
            : stock.Restock(command.Quantity, clock.UtcNow);
        if (adjustment.IsFailure)
        {
            return Result.Failure<StockResponse>(adjustment.Error);
        }

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (ConcurrencyConflictException)
        {
            return Result.Failure<StockResponse>(StockErrors.ConcurrencyConflict);
        }

        return StockResponse.From(stock);
    }
}
