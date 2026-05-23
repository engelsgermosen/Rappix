using Rappix.Tracking.Domain.CourierActiveOrders;

namespace Rappix.Tracking.Application.Abstractions;

/// <summary>
/// Mantiene el mapping <c>CourierId -> OrderId</c> en la tabla <c>courier_active_orders</c>. Lo
/// crea/actualiza <c>CourierAssignedConsumer</c>, lo lee <c>CourierLocationUpdatedConsumer</c>
/// para resolver el pedido del courier, y lo borra <c>OrderTerminalEventsConsumer</c>.
/// </summary>
public interface ICourierActiveOrderRepository
{
    /// <summary>Lookup por courier (el caso critico del consumer de location).</summary>
    Task<CourierActiveOrder?> GetByCourierIdAsync(Guid courierId, CancellationToken cancellationToken);

    /// <summary>Lookup por pedido (lo usa el consumer terminal para borrar la fila).</summary>
    Task<CourierActiveOrder?> GetByOrderIdAsync(Guid orderId, CancellationToken cancellationToken);

    /// <summary>Marca el mapping para insercion al proximo SaveChanges.</summary>
    void Add(CourierActiveOrder mapping);

    /// <summary>Borra la fila por <c>OrderId</c>. No-op si no existe (puede no haberse asignado courier).</summary>
    Task RemoveByOrderIdAsync(Guid orderId, CancellationToken cancellationToken);
}
