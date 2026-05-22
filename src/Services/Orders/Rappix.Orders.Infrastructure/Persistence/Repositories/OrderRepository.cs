using Microsoft.EntityFrameworkCore;
using Rappix.Orders.Application.Abstractions;
using Rappix.Orders.Domain.Orders;

namespace Rappix.Orders.Infrastructure.Persistence.Repositories;

/// <summary>Implementacion EF Core del repositorio del agregado <see cref="Order"/>.</summary>
internal sealed class OrderRepository(OrdersDbContext context) : IOrderRepository
{
    public void Add(Order order) => context.Orders.Add(order);

    public Task<Order?> GetByIdAsync(OrderId id, CancellationToken cancellationToken) =>
        context.Orders.Include(order => order.Lines).FirstOrDefaultAsync(order => order.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Order>> ListByCustomerAsync(Guid customerUserId, int skip, int take, CancellationToken cancellationToken) =>
        await context.Orders
            .Include(order => order.Lines)
            .Where(order => order.CustomerUserId == customerUserId)
            .OrderByDescending(order => order.CreatedAtUtc)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Order>> ListByMerchantOwnerAndStatusAsync(Guid merchantOwnerUserId, OrderStatus status, int skip, int take, CancellationToken cancellationToken) =>
        await context.Orders
            .Include(order => order.Lines)
            .Where(order => order.MerchantOwnerUserId == merchantOwnerUserId && order.Status == status)
            .OrderBy(order => order.CreatedAtUtc)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);
}
