using MassTransit;
using Microsoft.Extensions.Logging;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.BuildingBlocks.Core.Time;
using Rappix.Contracts.Payments;
using Rappix.Payments.Application.Abstractions;
using Rappix.Payments.Domain.Payments;

namespace Rappix.Payments.Infrastructure.Messaging;

/// <summary>
/// Atiende <see cref="PaymentRequestedIntegrationEvent"/> publicado por la saga de Orders al pasar a
/// AwaitingPayment. Autoriza el cobro contra el <see cref="IPaymentGateway"/> y publica
/// <see cref="PaymentSucceededIntegrationEvent"/> o <see cref="PaymentFailedIntegrationEvent"/> con
/// el mismo contrato que la saga ya consume (decision ADR-0009 #6: no rename, blast radius alto).
/// </summary>
/// <remarks>
/// Flujo y defensa de doble cobro en 3 niveles (ver plan Fase 8 seccion 4 y ADR-0009 #5):
/// <list type="number">
/// <item><b>Idempotency guard (Nivel 2)</b>: <see cref="IPaymentRepository.GetByOrderIdAsync"/>.
/// Si ya existe Payment para el OrderId, no-op silencioso (la reentrega encuentra el aggregate ya
/// creado y NO vuelve a autorizar). Mismo patron de <c>Reentrega_NoCreaSegundaAsignacion</c> en Dispatch.</item>
/// <item><b>Validacion del Money</b>: defensive — Orders construye PaymentRequested con datos validos,
/// pero rechazamos amounts/currency mal formados publicando PaymentFailed sin tocar el gateway.</item>
/// <item><b>Autorizar ANTES de SaveChanges</b>: si el SaveChanges falla (DB caida, conflicto xmin
/// improbable), MassTransit reintenta el consume — el segundo <see cref="IPaymentGateway.AuthorizeAsync"/>
/// con la MISMA <c>IdempotencyKey</c> devuelve el mismo intent (Stripe cachea 24h, Nivel 3). El
/// orden importa: si autorizaramos DESPUES del save y el save fallara, la siguiente vuelta no sabria
/// que ya hay un intent en el gateway. Documentado en plan seccion 5.1.</item>
/// <item><b>Inbox EF (Nivel 1)</b>: el callback AddConfigureEndpointsCallback aplica
/// UseEntityFrameworkOutbox; si el broker re-entrega el MISMO MessageId, el InboxState lo bloquea y
/// el consumer NO se ejecuta. Esta defensa se valida con la suite de integration con
/// WebApplicationFactory + Testcontainers RabbitMQ (deferido a commits futuros si hace falta;
/// el in-memory harness no honra la outbox).</item>
/// </list>
/// </remarks>
internal sealed partial class PaymentRequestedConsumer(
    IPaymentRepository repository,
    IPaymentGateway gateway,
    IUnitOfWork unitOfWork,
    IDateTimeProvider clock,
    ILogger<PaymentRequestedConsumer> logger)
    : IConsumer<PaymentRequestedIntegrationEvent>
{
    public async Task Consume(ConsumeContext<PaymentRequestedIntegrationEvent> context)
    {
        PaymentRequestedIntegrationEvent message = context.Message;
        CancellationToken cancellationToken = context.CancellationToken;

        // 1) Idempotency guard (Nivel 2): si ya existe Payment para el OrderId, no-op.
        Payment? existing = await repository.GetByOrderIdAsync(message.OrderId, cancellationToken);
        if (existing is not null)
        {
            string existingStatus = existing.Status.ToString();
            LogAlreadyExists(logger, message.OrderId, existingStatus);
            return;
        }

        // 2) Validar Money antes de tocar el gateway (defensivo).
        Result<Money> moneyResult = Money.Create(message.Amount, message.Currency);
        if (moneyResult.IsFailure)
        {
            string errorCode = moneyResult.Error.Code;
            LogInvalidMoney(logger, message.OrderId, errorCode);
            await context.Publish(
                new PaymentFailedIntegrationEvent { OrderId = message.OrderId, Reason = moneyResult.Error.Description },
                cancellationToken);
            return;
        }

        // 3) Crear aggregate + AUTORIZAR al gateway ANTES de SaveChanges. La idempotency-key es estable
        //    por OrderId — un retry de MassTransit tras un fallo de SaveChanges encuentra el mismo intent.
        Payment payment = Payment.Create(message.OrderId, message.CustomerUserId, moneyResult.Value, clock.UtcNow);
        repository.Add(payment);

        var authRequest = new AuthorizeRequest(
            OrderId: message.OrderId,
            CustomerUserId: message.CustomerUserId,
            Amount: message.Amount,
            Currency: message.Currency,
            IdempotencyKey: $"pmt-auth-{message.OrderId}",
            CustomerReference: null);

        Result<PaymentAuthorizationResult> authorizeGateway = await gateway.AuthorizeAsync(authRequest, cancellationToken);
        if (authorizeGateway.IsFailure)
        {
            string errorCode = authorizeGateway.Error.Code;
            string errorDescription = authorizeGateway.Error.Description;
            LogGatewayRejected(logger, message.OrderId, errorCode);
            payment.Fail(errorDescription, clock.UtcNow);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            await context.Publish(
                new PaymentFailedIntegrationEvent { OrderId = message.OrderId, Reason = errorDescription },
                cancellationToken);
            return;
        }

        Result authorizeAggregate = payment.Authorize(authorizeGateway.Value.ProviderPaymentIntentId, clock.UtcNow);
        if (authorizeAggregate.IsFailure)
        {
            // No deberia ocurrir: el aggregate esta en Pending al crearlo. Defensivo.
            string conflictCode = authorizeAggregate.Error.Code;
            string conflictDescription = authorizeAggregate.Error.Description;
            LogAuthorizeConflict(logger, message.OrderId, conflictCode);
            payment.Fail(conflictDescription, clock.UtcNow);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            await context.Publish(
                new PaymentFailedIntegrationEvent { OrderId = message.OrderId, Reason = conflictDescription },
                cancellationToken);
            return;
        }

        // 4) Persistir + publicar. AddConfigureEndpointsCallback con UseEntityFrameworkOutbox garantiza
        //    que el publish entra en la misma transaccion del SaveChanges — sin esto el publish se pierde.
        await unitOfWork.SaveChangesAsync(cancellationToken);

        await context.Publish(
            new PaymentSucceededIntegrationEvent
            {
                OrderId = message.OrderId,
                PaymentId = payment.Id, // = OrderId (decision ADR-0009 #4)
                Amount = payment.Amount.Amount,
            },
            cancellationToken);

        string providerIntent = authorizeGateway.Value.ProviderPaymentIntentId;
        LogAuthorized(logger, message.OrderId, providerIntent);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Pago para el pedido {OrderId} ya existe en estado {Status}: reentrega ignorada (idempotencia nivel aggregate).")]
    private static partial void LogAlreadyExists(ILogger logger, Guid orderId, string status);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Monto/moneda invalidos para el pedido {OrderId}: {ErrorCode}.")]
    private static partial void LogInvalidMoney(ILogger logger, Guid orderId, string errorCode);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Gateway rechazo la autorizacion del pedido {OrderId}: {ErrorCode}.")]
    private static partial void LogGatewayRejected(ILogger logger, Guid orderId, string errorCode);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Conflicto en Payment.Authorize para el pedido {OrderId}: {ErrorCode}.")]
    private static partial void LogAuthorizeConflict(ILogger logger, Guid orderId, string errorCode);

    [LoggerMessage(Level = LogLevel.Information, Message = "Pago autorizado para el pedido {OrderId}: {ProviderPaymentIntentId}.")]
    private static partial void LogAuthorized(ILogger logger, Guid orderId, string providerPaymentIntentId);
}
