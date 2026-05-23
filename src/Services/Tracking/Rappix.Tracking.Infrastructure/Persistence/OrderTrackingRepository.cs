using Microsoft.EntityFrameworkCore;
using Rappix.Tracking.Application.Abstractions;
using Rappix.Tracking.Domain.OrderTrackings;

namespace Rappix.Tracking.Infrastructure.Persistence;

/// <summary>Write side del read model: los consumers cargan, mutan y guardan via <see cref="IUnitOfWork"/>.</summary>
internal sealed class OrderTrackingRepository(TrackingDbContext db) : IOrderTrackingRepository
{
    public Task<OrderTracking?> GetByOrderIdAsync(Guid orderId, CancellationToken cancellationToken) =>
        db.OrderTrackings.FirstOrDefaultAsync(tracking => tracking.Id == orderId, cancellationToken);

    public void Add(OrderTracking tracking) => db.OrderTrackings.Add(tracking);
}
