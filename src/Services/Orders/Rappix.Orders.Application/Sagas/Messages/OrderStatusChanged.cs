using Rappix.Orders.Domain.Orders;

namespace Rappix.Orders.Application.Sagas.Messages;

/// <summary>
/// Publicado por la saga en cada transicion relevante. Lo consume la proyeccion (OrderStatusProjectionConsumer)
/// para actualizar el espejo <see cref="OrderStatus"/> del agregado Order. Se correlaciona por OrderId.
/// </summary>
public sealed record OrderStatusChanged(Guid OrderId, OrderStatus Status, DateTime ChangedAtUtc, string? Reason);
