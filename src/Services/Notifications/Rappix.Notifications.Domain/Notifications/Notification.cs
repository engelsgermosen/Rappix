using Rappix.BuildingBlocks.Core.Domain;
using Rappix.BuildingBlocks.Core.Results;

namespace Rappix.Notifications.Domain.Notifications;

/// <summary>
/// Registro auditable de una notificacion enviada (o intentada) a un destinatario por un evento del
/// pedido. Una fila por <c>(RelatedOrderId, RecipientUserId, NotificationType)</c> — el unique
/// partial index <c>UX_Notification_BusinessKey</c> sobre esa tripleta es lo que evita el email
/// duplicado cuando dos eventos distintos del broker (e.g. <c>OrderDelivered</c> + <c>OrderCompleted</c>)
/// producen la MISMA notificacion logica. Ver ADR-0010 D4.
/// </summary>
/// <remarks>
/// Decisiones de diseno:
/// <list type="bullet">
/// <item>Hereda de <see cref="Entity{TId}"/> y NO de <c>AggregateRoot</c>: no emite
///   <see cref="IDomainEvent"/>; Notifications es projector + emisor, no orquestador.</item>
/// <item><see cref="Entity{TId}.Id"/> es un <see cref="Guid.CreateVersion7()"/> propio (no <c>OrderId</c>):
///   varias notificaciones por pedido (una por destinatario × tipo). El dedup es por columnas, no por PK.</item>
/// <item><c>SourceMessageId</c> es INFORMACIONAL (audit del MessageId del broker que la produjo). NO
///   participa del unique index — eso es deliberado, ver ADR-0010 D4.</item>
/// <item>Transiciones <see cref="MarkSent"/> y <see cref="MarkFailed"/> son IDEMPOTENTES desde el
///   mismo estado terminal; conflict si se intenta cambiar entre Sent y Failed (broker reentrega
///   no deberia hacer eso porque el inbox EF dedupe primero, pero defensa en profundidad).</item>
/// <item><c>BodyPreview</c> se trunca a 512 chars en la factoria — el cuerpo completo NO se persiste
///   (se mando por email; la BD guarda el preview para el endpoint de historial).</item>
/// </list>
/// </remarks>
public sealed class Notification : Entity<Guid>
{
    /// <summary>Longitud maxima del preview del cuerpo persistido en BD (chars).</summary>
    public const int BodyPreviewMaxLength = 512;

    // EF necesita ctor sin parametros para materializar.
    private Notification() { }

    private Notification(
        Guid id,
        string? sourceMessageId,
        Guid recipientUserId,
        string recipientEmail,
        RecipientRole recipientRole,
        NotificationType notificationType,
        Guid? relatedOrderId,
        string subject,
        string bodyPreview,
        DateTime utcNow)
        : base(id)
    {
        SourceMessageId = sourceMessageId;
        RecipientUserId = recipientUserId;
        RecipientEmail = recipientEmail;
        RecipientRole = recipientRole;
        NotificationType = notificationType;
        RelatedOrderId = relatedOrderId;
        Subject = subject;
        BodyPreview = bodyPreview;
        Status = NotificationStatus.Pending;
        CreatedAtUtc = utcNow;
    }

    /// <summary>
    /// MessageId del integration event del broker que produjo esta notificacion. INFORMACIONAL —
    /// no participa del unique index; el dedup es por <c>(RelatedOrderId, RecipientUserId, NotificationType)</c>.
    /// Permite trazar "que evento del broker emitio este email" para forenses.
    /// </summary>
    public string? SourceMessageId { get; private set; }

    /// <summary>UserId del destinatario (clave para el endpoint de historial /me).</summary>
    public Guid RecipientUserId { get; private set; }

    /// <summary>Email al que se envio (snapshot del momento — si el user cambia su email, el historial preserva el original).</summary>
    public string RecipientEmail { get; private set; } = null!;

    /// <summary>Rol del destinatario (Customer/Merchant/Courier).</summary>
    public RecipientRole RecipientRole { get; private set; }

    /// <summary>Tipo logico de la notificacion (NewOrder, OrderDelivered, etc.).</summary>
    public NotificationType NotificationType { get; private set; }

    /// <summary>Pedido al que pertenece la notificacion. Nullable para futuras notificaciones no-pedido (welcome, password reset).</summary>
    public Guid? RelatedOrderId { get; private set; }

    /// <summary>Asunto enviado al canal (el email subject).</summary>
    public string Subject { get; private set; } = null!;

    /// <summary>Primeros <see cref="BodyPreviewMaxLength"/> chars del cuerpo del email (para el endpoint de historial).</summary>
    public string BodyPreview { get; private set; } = null!;

    /// <summary>Estado actual del envio.</summary>
    public NotificationStatus Status { get; private set; }

    /// <summary>Id que devolvio el canal (e.g. SendGrid <c>X-Message-Id</c>) si lo expone. Null para Fake.</summary>
    public string? ProviderMessageId { get; private set; }

    /// <summary>Razon textual del fallo del canal (para auditoria + futura UI de reintento).</summary>
    public string? ErrorReason { get; private set; }

    /// <summary>Creacion (UTC) — momento del insert Pending.</summary>
    public DateTime CreatedAtUtc { get; private set; }

    /// <summary>Momento (UTC) del transito a Sent o Failed. Null mientras Pending.</summary>
    public DateTime? SentAtUtc { get; private set; }

    /// <summary>
    /// Crea una notificacion en estado <see cref="NotificationStatus.Pending"/>. Trunca el cuerpo a
    /// <see cref="BodyPreviewMaxLength"/> chars. Devuelve <see cref="Result"/> de validacion para
    /// email y subject vacios — el NotifyHandler propaga el fallo como log de error sin caer.
    /// </summary>
    public static Result<Notification> Create(
        string? sourceMessageId,
        Guid recipientUserId,
        string recipientEmail,
        RecipientRole recipientRole,
        NotificationType notificationType,
        Guid? relatedOrderId,
        string subject,
        string body,
        DateTime utcNow)
    {
        if (string.IsNullOrWhiteSpace(recipientEmail))
        {
            return Result.Failure<Notification>(NotificationErrors.MissingRecipientEmail);
        }

        if (string.IsNullOrWhiteSpace(subject))
        {
            return Result.Failure<Notification>(NotificationErrors.MissingSubject);
        }

        string preview = body is null
            ? string.Empty
            : body.Length > BodyPreviewMaxLength
                ? body[..BodyPreviewMaxLength]
                : body;

        return new Notification(
            id: Guid.CreateVersion7(),
            sourceMessageId: sourceMessageId,
            recipientUserId: recipientUserId,
            recipientEmail: recipientEmail,
            recipientRole: recipientRole,
            notificationType: notificationType,
            relatedOrderId: relatedOrderId,
            subject: subject,
            bodyPreview: preview,
            utcNow: utcNow);
    }

    /// <summary>
    /// Marca la notificacion como enviada exitosamente. Idempotente desde <see cref="NotificationStatus.Sent"/>;
    /// conflict si la notificacion ya esta <see cref="NotificationStatus.Failed"/> (no se "recupera" un fallo
    /// — para eso se crea una nueva fila en un reintento explicito).
    /// </summary>
    public Result MarkSent(string? providerMessageId, DateTime utcNow)
    {
        switch (Status)
        {
            case NotificationStatus.Pending:
                Status = NotificationStatus.Sent;
                ProviderMessageId = providerMessageId;
                SentAtUtc = utcNow;
                return Result.Success();

            case NotificationStatus.Sent:
                return Result.Success();

            default:
                return Result.Failure(NotificationErrors.TerminalState);
        }
    }

    /// <summary>
    /// Marca la notificacion como fallida. Idempotente desde <see cref="NotificationStatus.Failed"/>;
    /// conflict si ya esta <see cref="NotificationStatus.Sent"/>.
    /// </summary>
    public Result MarkFailed(string errorReason, DateTime utcNow)
    {
        switch (Status)
        {
            case NotificationStatus.Pending:
                Status = NotificationStatus.Failed;
                ErrorReason = errorReason;
                SentAtUtc = utcNow;
                return Result.Success();

            case NotificationStatus.Failed:
                return Result.Success();

            default:
                return Result.Failure(NotificationErrors.TerminalState);
        }
    }
}
