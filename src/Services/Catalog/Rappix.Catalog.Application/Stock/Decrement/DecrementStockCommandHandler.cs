using MediatR;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.BuildingBlocks.Core.Time;
using Rappix.Catalog.Application.Abstractions;
using Rappix.Catalog.Application.Responses;
using Rappix.Catalog.Domain;
using Rappix.Catalog.Domain.Items;

namespace Rappix.Catalog.Application.Stock.Decrement;

/// <summary>Decrementa el stock; el guardado choca con xmin ante concurrencia y se traduce a Conflict.</summary>
internal sealed class DecrementStockCommandHandler(
    IStockRepository stocks,
    IUnitOfWork unitOfWork,
    IDateTimeProvider clock)
    : IRequestHandler<DecrementStockCommand, Result<StockResponse>>
{
    public async Task<Result<StockResponse>> Handle(DecrementStockCommand command, CancellationToken cancellationToken)
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

        Result decrement = stock.Decrement(command.Quantity, clock.UtcNow);
        if (decrement.IsFailure)
        {
            return Result.Failure<StockResponse>(decrement.Error);
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
