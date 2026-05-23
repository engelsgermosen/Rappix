using FluentAssertions;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.Notifications.Domain.Notifications;

namespace Rappix.Notifications.Tests.Unit;

/// <summary>
/// Pruebas unitarias del aggregate <see cref="Notification"/>. Cubre la matriz de transiciones
/// (Pending -> Sent/Failed con idempotencia y conflict en saltos invalidos) que es la base del
/// side-effect guard transaccional (Nivel 3 de la idempotencia documentada en ADR-0010 D4).
/// El dedup por unique index se prueba en los tests integration (Commit 10).
/// </summary>
public sealed class NotificationAggregateTests
{
    private static readonly DateTime Now = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Later = Now.AddMinutes(10);

    private static Result<Notification> NewValid(
        string subject = "Tu pedido fue aceptado",
        string body = "Hola Juan, el comercio acepto tu pedido.") =>
        Notification.Create(
            sourceMessageId: Guid.CreateVersion7().ToString(),
            recipientUserId: Guid.CreateVersion7(),
            recipientEmail: "customer@test.local",
            recipientRole: RecipientRole.Customer,
            notificationType: NotificationType.OrderAccepted,
            relatedOrderId: Guid.CreateVersion7(),
            subject: subject,
            body: body,
            utcNow: Now);

    private static Notification NewPending() => NewValid().Value;

    // -------- Create + validacion --------

    [Fact]
    public void Create_WithValidData_ReturnsNotificationInPending()
    {
        Guid recipientUserId = Guid.CreateVersion7();
        Guid orderId = Guid.CreateVersion7();
        string messageId = Guid.CreateVersion7().ToString();

        Result<Notification> result = Notification.Create(
            sourceMessageId: messageId,
            recipientUserId: recipientUserId,
            recipientEmail: "merchant@test.local",
            recipientRole: RecipientRole.Merchant,
            notificationType: NotificationType.NewOrder,
            relatedOrderId: orderId,
            subject: "Nuevo pedido entrante 12ab3c45",
            body: "Recibiste un pedido por 250.50 DOP.",
            utcNow: Now);

        result.IsSuccess.Should().BeTrue();
        Notification notification = result.Value;

        notification.Id.Should().NotBeEmpty();
        notification.SourceMessageId.Should().Be(messageId);
        notification.RecipientUserId.Should().Be(recipientUserId);
        notification.RecipientEmail.Should().Be("merchant@test.local");
        notification.RecipientRole.Should().Be(RecipientRole.Merchant);
        notification.NotificationType.Should().Be(NotificationType.NewOrder);
        notification.RelatedOrderId.Should().Be(orderId);
        notification.Subject.Should().Be("Nuevo pedido entrante 12ab3c45");
        notification.BodyPreview.Should().Be("Recibiste un pedido por 250.50 DOP.");
        notification.Status.Should().Be(NotificationStatus.Pending);
        notification.ProviderMessageId.Should().BeNull();
        notification.ErrorReason.Should().BeNull();
        notification.CreatedAtUtc.Should().Be(Now);
        notification.SentAtUtc.Should().BeNull();
    }

    [Fact]
    public void Create_WithEmptyEmail_ReturnsValidationError()
    {
        Result<Notification> empty = Notification.Create(
            sourceMessageId: null,
            recipientUserId: Guid.CreateVersion7(),
            recipientEmail: string.Empty,
            recipientRole: RecipientRole.Customer,
            notificationType: NotificationType.OrderAccepted,
            relatedOrderId: Guid.CreateVersion7(),
            subject: "Tu pedido",
            body: "...",
            utcNow: Now);

        Result<Notification> whitespace = Notification.Create(
            sourceMessageId: null,
            recipientUserId: Guid.CreateVersion7(),
            recipientEmail: "   ",
            recipientRole: RecipientRole.Customer,
            notificationType: NotificationType.OrderAccepted,
            relatedOrderId: Guid.CreateVersion7(),
            subject: "Tu pedido",
            body: "...",
            utcNow: Now);

        empty.IsFailure.Should().BeTrue();
        empty.Error.Should().Be(NotificationErrors.MissingRecipientEmail);
        whitespace.IsFailure.Should().BeTrue();
        whitespace.Error.Should().Be(NotificationErrors.MissingRecipientEmail);
    }

    [Fact]
    public void Create_WithEmptySubject_ReturnsValidationError()
    {
        Result<Notification> result = Notification.Create(
            sourceMessageId: null,
            recipientUserId: Guid.CreateVersion7(),
            recipientEmail: "x@test.local",
            recipientRole: RecipientRole.Customer,
            notificationType: NotificationType.OrderAccepted,
            relatedOrderId: Guid.CreateVersion7(),
            subject: string.Empty,
            body: "...",
            utcNow: Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(NotificationErrors.MissingSubject);
    }

    [Fact]
    public void Create_WithLongBody_TruncatesPreviewToMaxLength()
    {
        // Cuerpo de 1000 chars -> preview debe quedar en 512.
        string longBody = new('a', 1_000);

        Notification notification = NewValid(body: longBody).Value;

        notification.BodyPreview.Length.Should().Be(Notification.BodyPreviewMaxLength);
        notification.BodyPreview.Should().Be(new string('a', Notification.BodyPreviewMaxLength));
    }

    [Fact]
    public void Create_WithShortBody_PreservesIt()
    {
        string shortBody = "Texto corto";

        Notification notification = NewValid(body: shortBody).Value;

        notification.BodyPreview.Should().Be(shortBody);
    }

    // -------- MarkSent --------

    [Fact]
    public void MarkSent_FromPending_TransitionsToSent_SetsProviderIdAndSentAt()
    {
        Notification notification = NewPending();

        Result result = notification.MarkSent("sg-msg-001", Later);

        result.IsSuccess.Should().BeTrue();
        notification.Status.Should().Be(NotificationStatus.Sent);
        notification.ProviderMessageId.Should().Be("sg-msg-001");
        notification.SentAtUtc.Should().Be(Later);
        notification.ErrorReason.Should().BeNull();
    }

    [Fact]
    public void MarkSent_WithNullProviderId_IsAllowed_AndStillSent()
    {
        Notification notification = NewPending();

        Result result = notification.MarkSent(providerMessageId: null, Later);

        result.IsSuccess.Should().BeTrue();
        notification.Status.Should().Be(NotificationStatus.Sent);
        notification.ProviderMessageId.Should().BeNull(); // Fake channel devuelve null.
        notification.SentAtUtc.Should().Be(Later);
    }

    [Fact]
    public void MarkSent_FromSent_IsIdempotentNoOp()
    {
        Notification notification = NewPending();
        notification.MarkSent("sg-msg-001", Now);

        Result result = notification.MarkSent("sg-msg-001", Later);

        result.IsSuccess.Should().BeTrue();
        notification.Status.Should().Be(NotificationStatus.Sent);
        notification.ProviderMessageId.Should().Be("sg-msg-001");
        // Idempotente: SentAtUtc no se reescribe en una segunda llamada.
        notification.SentAtUtc.Should().Be(Now);
    }

    [Fact]
    public void MarkSent_FromFailed_ReturnsConflict()
    {
        Notification notification = NewPending();
        notification.MarkFailed("SendGrid 500", Now);

        Result result = notification.MarkSent("sg-msg-001", Later);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(NotificationErrors.TerminalState);
        // El estado no cambia.
        notification.Status.Should().Be(NotificationStatus.Failed);
        notification.ProviderMessageId.Should().BeNull();
    }

    // -------- MarkFailed --------

    [Fact]
    public void MarkFailed_FromPending_TransitionsToFailed_SetsErrorReasonAndSentAt()
    {
        Notification notification = NewPending();

        Result result = notification.MarkFailed("SendGrid devolvio 500", Later);

        result.IsSuccess.Should().BeTrue();
        notification.Status.Should().Be(NotificationStatus.Failed);
        notification.ErrorReason.Should().Be("SendGrid devolvio 500");
        notification.SentAtUtc.Should().Be(Later);
        notification.ProviderMessageId.Should().BeNull();
    }

    [Fact]
    public void MarkFailed_FromFailed_IsIdempotentNoOp()
    {
        Notification notification = NewPending();
        notification.MarkFailed("Error inicial", Now);

        Result result = notification.MarkFailed("Otro error que llega despues", Later);

        result.IsSuccess.Should().BeTrue();
        notification.Status.Should().Be(NotificationStatus.Failed);
        // Idempotente: el ErrorReason original se preserva (no se sobreescribe).
        notification.ErrorReason.Should().Be("Error inicial");
        notification.SentAtUtc.Should().Be(Now);
    }

    [Fact]
    public void MarkFailed_FromSent_ReturnsConflict()
    {
        Notification notification = NewPending();
        notification.MarkSent("sg-msg-001", Now);

        Result result = notification.MarkFailed("Algo paso despues", Later);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(NotificationErrors.TerminalState);
        notification.Status.Should().Be(NotificationStatus.Sent);
        notification.ErrorReason.Should().BeNull();
    }
}
