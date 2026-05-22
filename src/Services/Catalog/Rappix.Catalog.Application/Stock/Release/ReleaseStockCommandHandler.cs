using MediatR;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.BuildingBlocks.Core.Time;
using Rappix.Catalog.Application.Abstractions;
using Rappix.Catalog.Domain;
using Rappix.Catalog.Domain.Items;

namespace Rappix.Catalog.Application.Stock.Release;

/// <summary>
/// Libera los holds activos de un pedido (devuelve al disponible). Idempotente: sin holds activos
/// devuelve exito. El xmin del stock traduce conflictos concurrentes a Conflict.
/// </summary>
internal sealed class ReleaseStockCommandHandler(
    IStockRepository stocks,
    IStockReservationRepository reservations,
    IUnitOfWork unitOfWork,
    IDateTimeProvider clock)
    : IRequestHandler<ReleaseStockCommand, Result>
{
    public async Task<Result> Handle(ReleaseStockCommand command, CancellationToken cancellationToken)
    {
        DateTime now = clock.UtcNow;

        IReadOnlyList<StockReservation> reservationsForOrder = await reservations.GetByOrderIdAsync(command.OrderId, cancellationToken);
        StockReservation[] active = [.. reservationsForOrder.Where(reservation => reservation.IsHeld)];
        if (active.Length == 0)
        {
            // Ya liberado/confirmado o inexistente: no-op idempotente.
            return Result.Success();
        }

        foreach (StockReservation reservation in active)
        {
            StockLevel? stock = await stocks.GetByItemIdAsync(reservation.ItemId, cancellationToken);
            if (stock is null)
            {
                return Result.Failure(StockErrors.NotFound);
            }

            Result release = stock.ReleaseReservation(reservation.Quantity, now);
            if (release.IsFailure)
            {
                return Result.Failure(release.Error);
            }

            reservation.Release(now);
        }

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (ConcurrencyConflictException)
        {
            return Result.Failure(StockErrors.ConcurrencyConflict);
        }

        return Result.Success();
    }
}
