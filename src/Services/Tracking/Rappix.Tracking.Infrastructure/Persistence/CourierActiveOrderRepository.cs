using Microsoft.EntityFrameworkCore;
using Rappix.Tracking.Application.Abstractions;
using Rappix.Tracking.Domain.CourierActiveOrders;

namespace Rappix.Tracking.Infrastructure.Persistence;

/// <summary>Write side del mapping courier->pedido activo.</summary>
internal sealed class CourierActiveOrderRepository(TrackingDbContext db) : ICourierActiveOrderRepository
{
    public Task<CourierActiveOrder?> GetByCourierIdAsync(Guid courierId, CancellationToken cancellationToken) =>
        db.CourierActiveOrders.FirstOrDefaultAsync(map => map.Id == courierId, cancellationToken);

    public Task<CourierActiveOrder?> GetByOrderIdAsync(Guid orderId, CancellationToken cancellationToken) =>
        db.CourierActiveOrders.FirstOrDefaultAsync(map => map.OrderId == orderId, cancellationToken);

    public void Add(CourierActiveOrder mapping) => db.CourierActiveOrders.Add(mapping);

    public async Task RemoveByOrderIdAsync(Guid orderId, CancellationToken cancellationToken)
    {
        CourierActiveOrder? existing = await GetByOrderIdAsync(orderId, cancellationToken);
        if (existing is not null)
        {
            db.CourierActiveOrders.Remove(existing);
        }
    }
}
