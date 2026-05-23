using Microsoft.EntityFrameworkCore;
using Rappix.Dispatch.Application.Abstractions;
using Rappix.Dispatch.Domain.Couriers;

namespace Rappix.Dispatch.Infrastructure.Persistence;

/// <summary>Repositorio de <see cref="CourierAssignment"/> sobre el DbContext.</summary>
internal sealed class CourierAssignmentRepository(DispatchDbContext db) : ICourierAssignmentRepository
{
    public void Add(CourierAssignment assignment) => db.CourierAssignments.Add(assignment);

    public Task<CourierAssignment?> GetActiveByOrderAsync(Guid orderId, CancellationToken cancellationToken) =>
        db.CourierAssignments.FirstOrDefaultAsync(
            assignment => assignment.OrderId == orderId && assignment.ReleasedAtUtc == null,
            cancellationToken);

    public Task<CourierAssignment?> GetActiveByCourierAsync(CourierId courierId, CancellationToken cancellationToken) =>
        db.CourierAssignments.FirstOrDefaultAsync(
            assignment => assignment.CourierId == courierId && assignment.ReleasedAtUtc == null,
            cancellationToken);
}
