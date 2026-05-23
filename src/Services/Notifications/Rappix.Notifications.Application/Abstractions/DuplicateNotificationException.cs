namespace Rappix.Notifications.Application.Abstractions;

/// <summary>
/// La infraestructura lanza esta excepcion cuando un <c>SaveChangesAsync</c> sobre el aggregate
/// <c>Notification</c> viola el unique partial index <c>UX_Notification_BusinessKey</c> sobre
/// <c>(RelatedOrderId, RecipientUserId, NotificationType)</c>. Significa que OTRO worker (o el mismo
/// reintentando por una entrega previa que pasó pero perdió la respuesta) YA insertó esta notificacion
/// de negocio. El <c>NotifyHandler</c> la captura y la trata como NO-OP idempotente: no se envia el
/// email de nuevo, no se inserta nada, devuelve <see cref="BuildingBlocks.Core.Results.Result"/> de
/// exito. Documentado en ADR-0010 D4 Nivel 3 (side-effect guard transaccional).
/// </summary>
/// <remarks>
/// El traductor desde <c>DbUpdateException</c> + <c>PostgresException</c> con
/// <c>SqlState=="23505"</c> y <c>ConstraintName=="UX_Notification_BusinessKey"</c> vive en
/// Infrastructure (es donde se conoce Npgsql). Esto mantiene Application desacoplada del driver.
/// CRITICO: el catch debe ser ESTRECHO — solo este constraint. Otros DbUpdateException (FK, NOT NULL,
/// otra columna) deben propagar para que el broker reintente; NUNCA se silencian como "ya existe".
/// </remarks>
public sealed class DuplicateNotificationException : Exception
{
    /// <summary>Crea la excepcion con un mensaje por defecto.</summary>
    public DuplicateNotificationException()
        : base("Ya existe una notificacion para esta combinacion (pedido, destinatario, tipo).")
    {
    }

    /// <summary>Crea la excepcion con un mensaje y la excepcion interna que la origino.</summary>
    public DuplicateNotificationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
