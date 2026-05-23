using MassTransit;
using Microsoft.Extensions.Logging;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.BuildingBlocks.Core.Time;
using Rappix.Contracts.Dispatch;
using Rappix.Payments.Application.Abstractions;
using Rappix.Payments.Domain.Payments;

namespace Rappix.Payments.Infrastructure.Messaging;

/// <summary>
/// Captura el pago cuando Dispatch publica <see cref="OrderDeliveredIntegrationEvent"/> (terminal feliz).
/// La saga de Orders NO orquesta la captura — Payments reacciona al evento terminal igual que
/// Tracking y Dispatch consumen terminales (la saga se mantiene limpia de detalles de pago).
/// </summary>
/// <remarks>
/// Idempotencia y manejo de fallos:
/// <list type="bullet">
/// <item><b>Nivel 1 (inbox EF)</b>: re-entrega del mismo MessageId -> bloqueada por
/// <c>AddConfigureEndpointsCallback</c> + <c>UseEntityFrameworkOutbox</c>.</item>
/// <item><b>Nivel 2 (aggregate)</b>: <see cref="Payment.Capture"/> es idempotente — si ya esta Captured
/// devuelve <see cref="Result.Success"/> sin mutar; la segunda invocacion del consumer ve el
/// estado Captured y no llama al gateway.</item>
/// <item><b>Nivel 3 (gateway idempotency-key)</b>: la key <c>pmt-cap-{orderId}</c> es estable;
/// reintento tras timeout HTTP mid-flight devuelve la misma respuesta sin doble captura.</item>
/// <item><b>Fallo transitorio del gateway</b>: throw <see cref="InvalidOperationException"/> para
/// que MassTransit reintente (exponential backoff configurado en
/// <c>MessagingExtensions.cs</c>). NO marcar Failed/NeedsReview en cada blip de Stripe; tras N
/// retries el mensaje va al <c>_error</c> queue para revision manual.</item>
/// </list>
/// NO publica un PaymentCapturedIntegrationEvent en Fase 8 — no hay consumidor (Notifications es
/// Fase 9+). Follow-up documentado en ADR-0009.
/// </remarks>
internal sealed partial class OrderDeliveredCaptureConsumer(
    IPaymentRepository repository,
    IPaymentGateway gateway,
    IUnitOfWork unitOfWork,
    IDateTimeProvider clock,
    ILogger<OrderDeliveredCaptureConsumer> logger)
    : IConsumer<OrderDeliveredIntegrationEvent>
{
    public async Task Consume(ConsumeContext<OrderDeliveredIntegrationEvent> context)
    {
        Guid orderId = context.Message.OrderId;
        CancellationToken cancellationToken = context.CancellationToken;

        Payment? payment = await repository.GetByOrderIdAsync(orderId, cancellationToken);
        if (payment is null)
        {
            // Caso raro: OrderDelivered llega para un pedido sin Payment registrado (Orders nunca paso
            // por AwaitingPayment? Reorden de eventos?). Log sin lanzar — un fallo aqui no debe
            // bloquear la entrega del courier ni la transicion de la saga.
            LogNoPayment(logger, orderId);
            return;
        }

        switch (payment.Status)
        {
            case PaymentStatus.Captured:
                // Re-entrega idempotente — el inbox EF en produccion ya bloquearia esto, pero defensa
                // en profundidad para el harness in-memory y para casos exoticos.
                LogAlreadyCaptured(logger, orderId);
                return;

            case PaymentStatus.Authorized:
                // Camino feliz: captura el hold.
                break;

            default:
                // Pending (no debio llegar aqui sin authorize), Voided/Failed/NeedsReview/Refunded
                // (estados terminales — la saga ya cerro). Log warning y no-op; la fila persiste con
                // su estado actual para auditoria.
                string status = payment.Status.ToString();
                LogUnexpectedStatus(logger, orderId, status);
                return;
        }

        Result<PaymentCaptureResult> captureGateway = await gateway.CaptureAsync(
            payment.ProviderPaymentIntentId!,
            idempotencyKey: $"pmt-cap-{orderId}",
            cancellationToken);

        if (captureGateway.IsFailure)
        {
            // Fallo transitorio del gateway -> retry exponencial via MassTransit; tras N retries va al
            // _error queue para revision manual. NO mutamos el aggregate aqui (sigue en Authorized).
            string code = captureGateway.Error.Code;
            string description = captureGateway.Error.Description;
            LogGatewayFailed(logger, orderId, code);
            throw new InvalidOperationException(
                $"Stripe capture failed for OrderId={orderId}: {code} - {description}");
        }

        Result captureAggregate = payment.Capture(clock.UtcNow);
        if (captureAggregate.IsFailure)
        {
            // No deberia ocurrir: ya verificamos Status == Authorized. Defensivo.
            string code = captureAggregate.Error.Code;
            LogCaptureConflict(logger, orderId, code);
            throw new InvalidOperationException(
                $"Payment.Capture conflict for OrderId={orderId}: {code}");
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        LogCaptured(logger, orderId, payment.ProviderPaymentIntentId!);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "OrderDelivered para el pedido {OrderId} sin Payment registrado: ignorado.")]
    private static partial void LogNoPayment(ILogger logger, Guid orderId);

    [LoggerMessage(Level = LogLevel.Information, Message = "OrderDelivered para el pedido {OrderId}: ya capturado, no-op (idempotencia).")]
    private static partial void LogAlreadyCaptured(ILogger logger, Guid orderId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "OrderDelivered para el pedido {OrderId} en estado inesperado {Status}: no se captura.")]
    private static partial void LogUnexpectedStatus(ILogger logger, Guid orderId, string status);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Gateway fallo capturando el pedido {OrderId}: {ErrorCode}. Se relanza para retry de MassTransit.")]
    private static partial void LogGatewayFailed(ILogger logger, Guid orderId, string errorCode);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Conflicto en Payment.Capture para el pedido {OrderId}: {ErrorCode}.")]
    private static partial void LogCaptureConflict(ILogger logger, Guid orderId, string errorCode);

    [LoggerMessage(Level = LogLevel.Information, Message = "Pago capturado para el pedido {OrderId} (intent {ProviderPaymentIntentId}).")]
    private static partial void LogCaptured(ILogger logger, Guid orderId, string providerPaymentIntentId);
}
