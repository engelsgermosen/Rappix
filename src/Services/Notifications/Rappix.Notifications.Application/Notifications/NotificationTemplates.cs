using Rappix.Notifications.Application.Abstractions;

namespace Rappix.Notifications.Application.Notifications;

/// <summary>
/// Plantillas de subject + body por <c>(NotificationType x RecipientRole)</c>. Texto plano en
/// Fase 9 (HTML templates como follow-up en ADR-0010). Hardcoded en C# — no hay tabla de templates
/// ni engine de rendering; cambios = redeploy. Justificacion en ADR-0010 D3: scope minimo, sin
/// admin UI, sin schema extra, testeable como funciones puras.
/// </summary>
/// <remarks>
/// Convencion de naming: <c>{Type}For{Role}</c>. El metodo recibe los datos minimos necesarios para
/// el subject/body y devuelve <see cref="NotificationContent"/>. <c>ShortId</c> formatea los
/// primeros 8 chars del Guid para que el usuario vea un id breve en lugar del Guid completo.
/// </remarks>
public static class NotificationTemplates
{
    // --------------- NewOrder (merchant recibe pedido entrante) ---------------

    /// <summary>Merchant: "Tienes un nuevo pedido entrante".</summary>
    public static NotificationContent NewOrderForMerchant(
        Guid orderId, decimal total, string currency, string deliveryAddress) =>
        new(
            Subject: $"Nuevo pedido entrante {ShortId(orderId)}",
            Body:
                $"Hola,\n\n" +
                $"Recibiste un nuevo pedido (id {ShortId(orderId)}) por {total:F2} {currency}.\n" +
                $"Direccion de entrega: {deliveryAddress}\n\n" +
                $"Acepta o rechaza el pedido desde tu dashboard.");

    // --------------- OrderAccepted (cliente, cuando el merchant acepta) ---------------

    /// <summary>Cliente: "Tu pedido fue aceptado".</summary>
    public static NotificationContent OrderAcceptedForCustomer(Guid orderId, string customerFirstName) =>
        new(
            Subject: $"Tu pedido {ShortId(orderId)} fue aceptado",
            Body:
                $"Hola {customerFirstName},\n\n" +
                $"El comercio acepto tu pedido {ShortId(orderId)} y empezara a prepararlo.\n" +
                $"Te avisaremos cuando un courier este en camino.");

    // --------------- CourierAssigned (cliente + courier) ---------------

    /// <summary>Cliente: "Tu courier esta en camino al comercio".</summary>
    public static NotificationContent CourierAssignedForCustomer(Guid orderId, string courierFirstName) =>
        new(
            Subject: $"Un courier va por tu pedido {ShortId(orderId)}",
            Body:
                $"Hola,\n\n" +
                $"{courierFirstName} fue asignado a tu pedido {ShortId(orderId)} y esta en camino al comercio.\n" +
                $"Puedes ver su ubicacion en vivo desde la app.");

    /// <summary>Courier: "Tienes una nueva asignacion".</summary>
    public static NotificationContent CourierAssignedForCourier(Guid orderId, string courierFirstName) =>
        new(
            Subject: $"Nueva asignacion: pedido {ShortId(orderId)}",
            Body:
                $"Hola {courierFirstName},\n\n" +
                $"Tienes una nueva asignacion (pedido {ShortId(orderId)}).\n" +
                $"Abre la app para ver los detalles y la ubicacion del comercio.");

    // --------------- OrderDelivered (cliente + merchant) ---------------

    /// <summary>Cliente: "Tu pedido fue entregado".</summary>
    public static NotificationContent OrderDeliveredForCustomer(Guid orderId, string customerFirstName) =>
        new(
            Subject: $"Tu pedido {ShortId(orderId)} fue entregado",
            Body:
                $"Hola {customerFirstName},\n\n" +
                $"Tu pedido {ShortId(orderId)} fue entregado.\n" +
                $"Esperamos que lo disfrutes. Gracias por usar Rappix!");

    /// <summary>Merchant: "El pedido fue entregado".</summary>
    public static NotificationContent OrderDeliveredForMerchant(Guid orderId) =>
        new(
            Subject: $"Pedido {ShortId(orderId)} entregado",
            Body:
                $"Hola,\n\n" +
                $"El pedido {ShortId(orderId)} fue entregado al cliente exitosamente.");

    // --------------- OrderCancelled (cliente + merchant) ---------------

    /// <summary>Cliente: "Tu pedido fue cancelado".</summary>
    public static NotificationContent OrderCancelledForCustomer(Guid orderId, string customerFirstName, string reason) =>
        new(
            Subject: $"Tu pedido {ShortId(orderId)} fue cancelado",
            Body:
                $"Hola {customerFirstName},\n\n" +
                $"Tu pedido {ShortId(orderId)} fue cancelado.\n" +
                $"Motivo: {reason}\n\n" +
                $"Si se realizo algun cargo, el reembolso se procesara en los proximos dias.");

    /// <summary>Merchant: "El pedido fue cancelado".</summary>
    public static NotificationContent OrderCancelledForMerchant(Guid orderId, string reason) =>
        new(
            Subject: $"Pedido {ShortId(orderId)} cancelado",
            Body:
                $"Hola,\n\n" +
                $"El pedido {ShortId(orderId)} fue cancelado.\n" +
                $"Motivo: {reason}");

    // --------------- OrderFailed (cliente + merchant) ---------------

    /// <summary>Cliente: "Tu pedido fallo".</summary>
    public static NotificationContent OrderFailedForCustomer(Guid orderId, string customerFirstName, string reason) =>
        new(
            Subject: $"Tu pedido {ShortId(orderId)} no pudo procesarse",
            Body:
                $"Hola {customerFirstName},\n\n" +
                $"No pudimos procesar tu pedido {ShortId(orderId)}.\n" +
                $"Motivo: {reason}\n\n" +
                $"Si se realizo algun cargo, el reembolso se procesara en los proximos dias.");

    /// <summary>Merchant: "El pedido fallo".</summary>
    public static NotificationContent OrderFailedForMerchant(Guid orderId, string reason) =>
        new(
            Subject: $"Pedido {ShortId(orderId)} con error",
            Body:
                $"Hola,\n\n" +
                $"El pedido {ShortId(orderId)} no pudo completarse.\n" +
                $"Motivo: {reason}");

    /// <summary>Formatea los primeros 8 chars del Guid para mostrar al usuario un id corto.</summary>
    private static string ShortId(Guid id) =>
        id.ToString("N", System.Globalization.CultureInfo.InvariantCulture)[..8];
}
