using Rappix.Orders.Domain.Orders;

namespace Rappix.Orders.Application.Abstractions;

/// <summary>Acceso a la persistencia del agregado <see cref="Order"/>.</summary>
public interface IOrderRepository
{
    /// <summary>Marca un nuevo pedido para insercion.</summary>
    void Add(Order order);

    /// <summary>Obtiene un pedido por id, incluyendo sus lineas (o null si no existe).</summary>
    Task<Order?> GetByIdAsync(OrderId id, CancellationToken cancellationToken);

    /// <summary>Lista los pedidos de un cliente (paginado, mas recientes primero).</summary>
    Task<IReadOnlyList<Order>> ListByCustomerAsync(Guid customerUserId, int skip, int take, CancellationToken cancellationToken);

    /// <summary>Lista los pedidos de un merchant (por su usuario dueno) en un estado dado (p. ej. esperando aceptacion).</summary>
    Task<IReadOnlyList<Order>> ListByMerchantOwnerAndStatusAsync(Guid merchantOwnerUserId, OrderStatus status, int skip, int take, CancellationToken cancellationToken);
}
