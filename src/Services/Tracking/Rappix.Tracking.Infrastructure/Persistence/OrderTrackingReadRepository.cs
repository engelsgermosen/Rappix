using Microsoft.EntityFrameworkCore;
using Rappix.Tracking.Application.Abstractions;

namespace Rappix.Tracking.Infrastructure.Persistence;

/// <summary>
/// Read side: proyecta directamente a <see cref="OrderTrackingSnapshot"/> con <c>AsNoTracking</c>
/// para no levantar el change-tracker en el hot path del hub (Subscribe) ni del GET REST.
/// </summary>
internal sealed class OrderTrackingReadRepository(TrackingDbContext db) : IOrderTrackingReadRepository
{
    public Task<OrderTrackingSnapshot?> GetSnapshotAsync(Guid orderId, CancellationToken cancellationToken) =>
        db.OrderTrackings
            .AsNoTracking()
            .Where(tracking => tracking.Id == orderId)
            .Select(tracking => new OrderTrackingSnapshot(
                tracking.Id,
                tracking.CustomerUserId,
                tracking.MerchantId,
                tracking.CurrentStatus,
                tracking.StatusReason,
                tracking.LastCourierId,
                tracking.LastCourierLat,
                tracking.LastCourierLng,
                tracking.LastLocationAtUtc,
                tracking.PickupLat,
                tracking.PickupLng,
                tracking.DeliveryLat,
                tracking.DeliveryLng,
                tracking.CreatedAtUtc,
                tracking.UpdatedAtUtc))
            .FirstOrDefaultAsync(cancellationToken);
}
