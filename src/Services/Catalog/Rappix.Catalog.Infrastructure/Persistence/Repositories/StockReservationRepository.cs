using Microsoft.EntityFrameworkCore;
using Rappix.Catalog.Application.Abstractions;
using Rappix.Catalog.Domain.Items;

namespace Rappix.Catalog.Infrastructure.Persistence.Repositories;

/// <summary>Implementacion EF Core del repositorio de holds de stock (<see cref="StockReservation"/>).</summary>
internal sealed class StockReservationRepository(CatalogDbContext context) : IStockReservationRepository
{
    public void Add(StockReservation reservation) => context.StockReservations.Add(reservation);

    public async Task<IReadOnlyList<StockReservation>> GetByOrderIdAsync(Guid orderId, CancellationToken cancellationToken) =>
        await context.StockReservations
            .Where(reservation => reservation.OrderId == orderId)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<StockReservation>> GetHeldByItemIdsAsync(IReadOnlyCollection<ItemId> itemIds, CancellationToken cancellationToken)
    {
        // Igualdad por item (traduccion fiable del tipo convertido ItemId); el numero de items por pedido es pequeno.
        var held = new List<StockReservation>();
        foreach (ItemId itemId in itemIds)
        {
            List<StockReservation> forItem = await context.StockReservations
                .Where(reservation => reservation.ItemId == itemId && reservation.Status == StockReservationStatus.Held)
                .ToListAsync(cancellationToken);
            held.AddRange(forItem);
        }

        return held;
    }
}
