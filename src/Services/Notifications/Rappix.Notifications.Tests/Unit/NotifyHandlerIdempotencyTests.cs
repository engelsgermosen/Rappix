using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.BuildingBlocks.Core.Time;
using Rappix.Notifications.Application.Abstractions;
using Rappix.Notifications.Application.Notifications;
using Rappix.Notifications.Domain.Notifications;

namespace Rappix.Notifications.Tests.Unit;

/// <summary>
/// Pruebas unitarias del orquestador <see cref="NotifyHandler"/> cubriendo la matriz de
/// idempotencia documentada en ADR-0010 D4. El test critico end-to-end con BD real + RabbitMQ vive
/// en commit 10 (DoubleDeliveryIdempotencyTests); aqui se valida la logica del handler con todas las
/// dependencias mockeadas via NSubstitute.
/// </summary>
public sealed class NotifyHandlerIdempotencyTests
{
    private static readonly DateTime Now = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly Guid OrderId = Guid.CreateVersion7();
    private static readonly Guid RecipientUserId = Guid.CreateVersion7();

    private static NotifyRequest BuildRequest(NotificationType type = NotificationType.OrderAccepted) =>
        new(
            SourceMessageId: Guid.CreateVersion7().ToString(),
            Recipient: new Recipient(
                UserId: RecipientUserId,
                Email: "customer@test.local",
                Name: "Juan Perez",
                Role: RecipientRole.Customer),
            Type: type,
            RelatedOrderId: OrderId,
            Content: new NotificationContent(
                Subject: "Tu pedido fue aceptado",
                Body: "Hola Juan, el comercio acepto tu pedido."));

    private static (NotifyHandler handler,
                    INotificationRepository repo,
                    INotificationChannel channel,
                    IUnitOfWork uow) BuildHandler()
    {
        var repo = Substitute.For<INotificationRepository>();
        var channel = Substitute.For<INotificationChannel>();
        var uow = Substitute.For<IUnitOfWork>();
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(Now);

        var handler = new NotifyHandler(repo, channel, uow, clock, NullLogger<NotifyHandler>.Instance);
        return (handler, repo, channel, uow);
    }

    // -------------------- Nivel 1: lookup encuentra existente -> no-op --------------------

    [Fact]
    public async Task SendAsync_WhenNotificationAlreadyExists_DoesNotInsertNorSendNorSave()
    {
        (NotifyHandler handler, INotificationRepository repo, INotificationChannel channel, IUnitOfWork uow) = BuildHandler();
        NotifyRequest request = BuildRequest();

        Notification existing = Notification.Create(
            sourceMessageId: "previous-msg-id",
            recipientUserId: request.Recipient.UserId,
            recipientEmail: request.Recipient.Email,
            recipientRole: request.Recipient.Role,
            notificationType: request.Type,
            relatedOrderId: request.RelatedOrderId,
            subject: "previo",
            body: "previo",
            utcNow: Now).Value;

        repo.GetByBusinessKeyAsync(request.RelatedOrderId, request.Recipient.UserId, request.Type, Arg.Any<CancellationToken>())
            .Returns(existing);

        Result result = await handler.SendAsync(request, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        repo.DidNotReceive().Add(Arg.Any<Notification>());
        await channel.DidNotReceive().SendAsync(Arg.Any<NotificationMessage>(), Arg.Any<CancellationToken>());
        await uow.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    // -------------------- Happy path: insert + send OK -> Sent --------------------

    [Fact]
    public async Task SendAsync_WhenNewAndChannelSucceeds_InsertsSavesTwiceAndMarksSent()
    {
        (NotifyHandler handler, INotificationRepository repo, INotificationChannel channel, IUnitOfWork uow) = BuildHandler();
        NotifyRequest request = BuildRequest();

        repo.GetByBusinessKeyAsync(request.RelatedOrderId, request.Recipient.UserId, request.Type, Arg.Any<CancellationToken>())
            .Returns((Notification?)null);

        channel.SendAsync(Arg.Any<NotificationMessage>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new NotificationSendResult("sg-msg-001")));

        Notification? captured = null;
        repo.When(r => r.Add(Arg.Any<Notification>())).Do(call => captured = call.Arg<Notification>());

        Result result = await handler.SendAsync(request, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        repo.Received(1).Add(Arg.Any<Notification>());
        await channel.Received(1).SendAsync(
            Arg.Is<NotificationMessage>(m =>
                m.ToEmail == request.Recipient.Email &&
                m.ToName == request.Recipient.Name &&
                m.Subject == request.Content.Subject &&
                m.Body == request.Content.Body),
            Arg.Any<CancellationToken>());
        // 2 SaveChanges: 1 al insertar Pending, 1 al transitar a Sent.
        await uow.Received(2).SaveChangesAsync(Arg.Any<CancellationToken>());
        captured.Should().NotBeNull();
        captured!.Status.Should().Be(NotificationStatus.Sent);
        captured.ProviderMessageId.Should().Be("sg-msg-001");
    }

    // -------------------- Nivel 2: race condition -> no-op silencioso --------------------

    [Fact]
    public async Task SendAsync_WhenSaveThrowsDuplicate_DoesNotCallChannelAndReturnsSuccess()
    {
        (NotifyHandler handler, INotificationRepository repo, INotificationChannel channel, IUnitOfWork uow) = BuildHandler();
        NotifyRequest request = BuildRequest();

        repo.GetByBusinessKeyAsync(request.RelatedOrderId, request.Recipient.UserId, request.Type, Arg.Any<CancellationToken>())
            .Returns((Notification?)null);

        // El primer SaveChanges (insert) lanza por race del unique index.
        uow.SaveChangesAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new DuplicateNotificationException());

        Result result = await handler.SendAsync(request, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        repo.Received(1).Add(Arg.Any<Notification>());
        // CRITICO: el canal no se llama si el insert fallo por race — evita doble email.
        await channel.DidNotReceive().SendAsync(Arg.Any<NotificationMessage>(), Arg.Any<CancellationToken>());
        // Solo el primer SaveChanges (que lanzo). El segundo (post-canal) no se invoca.
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    // -------------------- Camino fallido del canal: Failed, sin relanzar --------------------

    [Fact]
    public async Task SendAsync_WhenChannelFails_MarksFailed_DoesNotRethrow()
    {
        (NotifyHandler handler, INotificationRepository repo, INotificationChannel channel, IUnitOfWork uow) = BuildHandler();
        NotifyRequest request = BuildRequest();

        repo.GetByBusinessKeyAsync(request.RelatedOrderId, request.Recipient.UserId, request.Type, Arg.Any<CancellationToken>())
            .Returns((Notification?)null);

        var sendError = Error.Failure("Notifications.Channel.SendFailed", "SendGrid devolvio 500.");
        channel.SendAsync(Arg.Any<NotificationMessage>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<NotificationSendResult>(sendError));

        Notification? captured = null;
        repo.When(r => r.Add(Arg.Any<Notification>())).Do(call => captured = call.Arg<Notification>());

        Result result = await handler.SendAsync(request, CancellationToken.None);

        // El handler SIEMPRE devuelve Success si la fila se persistio — el fallo de envio NO relanza
        // (eso permitiria al broker reintentar, lo que produciria emails duplicados si el segundo
        // intento del canal SI funciona; el reintento de envios es responsabilidad de un job
        // separado, follow-up de ADR-0010).
        result.IsSuccess.Should().BeTrue();
        captured.Should().NotBeNull();
        captured!.Status.Should().Be(NotificationStatus.Failed);
        captured.ErrorReason.Should().Be(sendError.Description);
        await uow.Received(2).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    // -------------------- Validacion del aggregate: email vacio -> failure --------------------

    [Fact]
    public async Task SendAsync_WhenRecipientEmailIsEmpty_ReturnsValidationFailure_DoesNotInsertNorSend()
    {
        (NotifyHandler handler, INotificationRepository repo, INotificationChannel channel, IUnitOfWork uow) = BuildHandler();

        var request = BuildRequest() with
        {
            Recipient = new Recipient(RecipientUserId, Email: string.Empty, Name: "X", Role: RecipientRole.Customer),
        };

        repo.GetByBusinessKeyAsync(request.RelatedOrderId, request.Recipient.UserId, request.Type, Arg.Any<CancellationToken>())
            .Returns((Notification?)null);

        Result result = await handler.SendAsync(request, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(NotificationErrors.MissingRecipientEmail);
        repo.DidNotReceive().Add(Arg.Any<Notification>());
        await channel.DidNotReceive().SendAsync(Arg.Any<NotificationMessage>(), Arg.Any<CancellationToken>());
        await uow.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
