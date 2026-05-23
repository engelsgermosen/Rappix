namespace Rappix.Notifications.Domain.Notifications;

/// <summary>
/// Tipo LOGICO de una notificacion, NO el tipo del evento de integracion. Multiples eventos pueden
/// mapear al mismo tipo logico — el caso clave es <see cref="OrderDelivered"/>, al que mapean tanto
/// <c>OrderDeliveredIntegrationEvent</c> (Dispatch) como <c>OrderCompletedIntegrationEvent</c>
/// (Orders saga). El unique partial index <c>UX_Notification_BusinessKey</c> sobre
/// <c>(RelatedOrderId, RecipientUserId, NotificationType)</c> dedupe ambas — el primero inserta, el
/// segundo recibe <c>DbUpdateException</c> y sale como no-op (sin volver a llamar al canal). Esto
/// evita el email duplicado al cliente, espejo del doble-cobro de Payments (lecciones Fase 8).
/// </summary>
public enum NotificationType
{
    /// <summary>Merchant recibe "tienes un nuevo pedido entrante" (disparado por OrderSubmitted).</summary>
    NewOrder = 1,

    /// <summary>Cliente recibe "tu pedido fue aceptado" (disparado por OrderAccepted).</summary>
    OrderAccepted = 2,

    /// <summary>Cliente recibe "tu courier esta en camino al comercio" (disparado por CourierAssigned).</summary>
    CourierAssignedForCustomer = 3,

    /// <summary>Courier recibe "tienes una nueva asignacion" (disparado por CourierAssigned).</summary>
    CourierAssignedForCourier = 4,

    /// <summary>
    /// Cliente + Merchant reciben "el pedido fue entregado". Disparado tanto por
    /// <c>OrderDeliveredIntegrationEvent</c> (Dispatch) como por <c>OrderCompletedIntegrationEvent</c>
    /// (Orders saga); el unique index dedupe ambos por <c>(RelatedOrderId, RecipientUserId, NotificationType)</c>.
    /// </summary>
    OrderDelivered = 5,

    /// <summary>Cliente + Merchant reciben "el pedido fue cancelado" con razon.</summary>
    OrderCancelled = 6,

    /// <summary>Cliente + Merchant reciben "el pedido fallo" con razon.</summary>
    OrderFailed = 7,
}
