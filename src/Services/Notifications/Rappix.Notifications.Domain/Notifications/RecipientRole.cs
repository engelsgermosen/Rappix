namespace Rappix.Notifications.Domain.Notifications;

/// <summary>
/// Rol del destinatario al momento de recibir una notificacion. Subset del <c>UserType</c> de
/// Identity excluyendo <c>Admin</c> — un administrador no recibe notificaciones de pedidos
/// (la Fase 9 cubre cliente/merchant/courier; admins se notifican fuera de banda si llega ese caso).
/// </summary>
public enum RecipientRole
{
    /// <summary>Cliente que realizo el pedido.</summary>
    Customer = 0,

    /// <summary>Comercio dueno del item.</summary>
    Merchant = 1,

    /// <summary>Courier asignado al pedido.</summary>
    Courier = 2,
}
