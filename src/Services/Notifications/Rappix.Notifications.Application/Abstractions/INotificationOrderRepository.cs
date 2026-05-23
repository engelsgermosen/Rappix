using Rappix.Notifications.Domain.NotificationOrders;

namespace Rappix.Notifications.Application.Abstractions;

/// <summary>
/// Repositorio de la proyeccion <see cref="NotificationOrder"/>. El consumer de
/// <c>OrderSubmittedIntegrationEvent</c> hace <see cref="Add"/>; el de
/// <c>CourierAssignedIntegrationEvent</c> hace lookup + <c>SetCourier</c>; los consumers de
/// terminales (Accepted/Cancelled/Failed/Delivered) hacen solo <see cref="GetByIdAsync"/> para
/// resolver los UserIds (Customer/Merchant/Courier) que sus eventos NO llevan.
/// </summary>
public interface INotificationOrderRepository
{
    /// <summary>Recupera la proyeccion del pedido por <c>OrderId</c>. Null si no existe (cold-start gap).</summary>
    Task<NotificationOrder?> GetByIdAsync(Guid orderId, CancellationToken cancellationToken);

    /// <summary>Lo marca para insercion al proximo SaveChanges.</summary>
    void Add(NotificationOrder notificationOrder);
}
