using MediatR;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.BuildingBlocks.Core.Time;
using Rappix.Catalog.Application.Abstractions;
using Rappix.Catalog.Domain;
using Rappix.Catalog.Domain.Items;

namespace Rappix.Catalog.Application.Stock.Reserve;

/// <summary>
/// Aparta stock para un pedido. Idempotente por OrderId. Antes de validar el disponible barre los
/// holds vencidos (TTL) de los items pedidos y los libera (devuelve al disponible). La reserva es
/// todo-o-nada: si una sola linea no alcanza, no se aparta ninguna. El xmin del stock evita la
/// sobreventa ante reservas concurrentes (uno gana, el otro recibe ConcurrencyConflict).
/// </summary>
internal sealed class ReserveStockCommandHandler(
    IStockRepository stocks,
    IStockReservationRepository reservations,
    IUnitOfWork unitOfWork,
    IDateTimeProvider clock)
    : IRequestHandler<ReserveStockCommand, Result>
{
    public async Task<Result> Handle(ReserveStockCommand command, CancellationToken cancellationToken)
    {
        DateTime now = clock.UtcNow;

        // Idempotencia: si el pedido ya tiene holds (Held o Committed), es una re-entrega -> exito sin duplicar.
        IReadOnlyList<StockReservation> existing = await reservations.GetByOrderIdAsync(command.OrderId, cancellationToken);
        if (existing.Any(reservation => reservation.Status != StockReservationStatus.Released))
        {
            return Result.Success();
        }

        ItemId[] itemIds = [.. command.Lines.Select(line => new ItemId(line.ItemId))];

        await SweepExpiredHoldsAsync(itemIds, now, cancellationToken);

        // Validar disponible de TODAS las lineas antes de mutar (todo-o-nada).
        var targets = new List<(StockLevel Stock, int Quantity)>(command.Lines.Count);
        foreach (ReserveStockLine line in command.Lines)
        {
            StockLevel? stock = await stocks.GetByItemIdAsync(new ItemId(line.ItemId), cancellationToken);
            if (stock is null)
            {
                return Result.Failure(StockErrors.NotFound);
            }

            if (line.Quantity > stock.Available)
            {
                return Result.Failure(StockErrors.InsufficientStock);
            }

            targets.Add((stock, line.Quantity));
        }

        DateTime expiresAtUtc = now.AddSeconds(command.TtlSeconds);
        foreach ((StockLevel stock, int quantity) in targets)
        {
            Result reserve = stock.Reserve(quantity, now);
            if (reserve.IsFailure)
            {
                return Result.Failure(reserve.Error);
            }

            reservations.Add(StockReservation.Create(command.OrderId, stock.Id, stock.MerchantId, quantity, expiresAtUtc, now));
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

    private async Task SweepExpiredHoldsAsync(IReadOnlyCollection<ItemId> itemIds, DateTime now, CancellationToken cancellationToken)
    {
        IReadOnlyList<StockReservation> held = await reservations.GetHeldByItemIdsAsync(itemIds, cancellationToken);
        foreach (StockReservation hold in held)
        {
            if (!hold.IsExpired(now))
            {
                continue;
            }

            StockLevel? stock = await stocks.GetByItemIdAsync(hold.ItemId, cancellationToken);
            stock?.ReleaseReservation(hold.Quantity, now);
            hold.Release(now);
        }
    }
}
