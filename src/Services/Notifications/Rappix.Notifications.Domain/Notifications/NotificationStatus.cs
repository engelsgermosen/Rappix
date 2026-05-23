namespace Rappix.Notifications.Domain.Notifications;

/// <summary>
/// Estados de una <see cref="Notification"/> en el ciclo de envio. <see cref="Pending"/> es el
/// estado inicial al insertar la fila (antes de llamar al canal); el canal devuelve y se transita a
/// <see cref="Sent"/> o <see cref="Failed"/>. Persistir el <c>Pending</c> antes del side-effect es
/// la red de seguridad transaccional contra el unique index <c>UX_Notification_BusinessKey</c>: si
/// dos workers consumen el mismo evento en paralelo, uno inserta y envia, el otro recibe
/// <c>DbUpdateException</c> y sale como no-op (sin llamar al canal). Las filas en estado
/// <see cref="Failed"/> son candidatas a un job de reintento (follow-up de Fase 9).
/// </summary>
public enum NotificationStatus
{
    /// <summary>Insertada en BD, aun no enviada al canal.</summary>
    Pending = 0,

    /// <summary>El canal devolvio exito (con o sin <c>ProviderMessageId</c>).</summary>
    Sent = 1,

    /// <summary>El canal devolvio error o lanzo excepcion; queda registrada para reintento manual.</summary>
    Failed = 2,
}
