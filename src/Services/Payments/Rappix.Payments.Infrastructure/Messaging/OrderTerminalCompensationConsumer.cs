using MassTransit;
using Microsoft.Extensions.Logging;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.BuildingBlocks.Core.Time;
using Rappix.Contracts.Orders;
using Rappix.Contracts.Payments;
using Rappix.Payments.Application.Abstractions;
using Rappix.Payments.Domain.Payments;

namespace Rappix.Payments.Infrastructure.Messaging;

/// <summary>
/// Atiende los terminales de compensacion (OrderCancelled/OrderFailed) y el refund explicito
/// (RefundRequested) que la saga puede emitir cuando el dinero ya fue capturado. Mismo patron
/// multi-IConsumer de Tracking/Dispatch para los terminales.
/// </summary>
/// <remarks>
/// <para>Politica de compensacion (decision usuario + ADR-0009 #3):</para>
/// <list type="bullet">
/// <item><b>Pre-captura (Authorized + OrderCancelled/Failed)</b>: VOID del hold. El cliente nunca es
/// cobrado. No hay refund.</item>
/// <item><b>Post-captura (Captured + OrderCancelled/Failed)</b>: NeedsReview. NO auto-refund. Humano
/// decide via dashboard (Fase 9+). Consistente con StockCommitFails_NeedsReview de la saga.</item>
/// <item><b>Refund explicito (Captured + RefundRequested)</b>: la saga lo emite cuando ella misma
/// detecta que cobro y luego debe reembolsar (race PaymentSucceeded tardio post-cancelacion en
/// OrderStateMachine.cs:159 y :167). Aqui SI hacemos refund real porque la saga lo pidio
/// explicitamente — Payments es la fuente de verdad sobre si el dinero salio, la saga confia.</item>
/// </list>
/// <para>Idempotencia: todos los metodos del aggregate son idempotentes y todos los caminos
/// terminan en log + return si el estado no corresponde. Las re-entregas son siempre no-op
/// despues del primer consume exitoso.</para>
/// </remarks>
internal sealed partial class OrderTerminalCompensationConsumer(
    IPaymentRepository repository,
    IPaymentGateway gateway,
    IUnitOfWork unitOfWork,
    IDateTimeProvider clock,
    ILogger<OrderTerminalCompensationConsumer> logger)
    : IConsumer<OrderCancelledIntegrationEvent>,
      IConsumer<OrderFailedIntegrationEvent>,
      IConsumer<RefundRequestedIntegrationEvent>
{
    public Task Consume(ConsumeContext<OrderCancelledIntegrationEvent> context) =>
        CompensateAsync(context.Message.OrderId, context.Message.Reason, context.CancellationToken);

    public Task Consume(ConsumeContext<OrderFailedIntegrationEvent> context) =>
        CompensateAsync(context.Message.OrderId, context.Message.Reason, context.CancellationToken);

    public async Task Consume(ConsumeContext<RefundRequestedIntegrationEvent> context)
    {
        // Refund EXPLICITO solicitado por la saga. Solo procede si el aggregate esta Captured;
        // cualquier otro estado se reporta como no-op (auditoria explicita).
        RefundRequestedIntegrationEvent message = context.Message;
        Guid orderId = message.OrderId;

        Payment? payment = await repository.GetByOrderIdAsync(orderId, context.CancellationToken);
        if (payment is null)
        {
            LogRefundNoPayment(logger, orderId);
            return;
        }

        if (payment.Status != PaymentStatus.Captured)
        {
            // Voided/Authorized: ya manejado por OrderCancelled/OrderFailed (void o needs-review).
            // Refunded: re-entrega -> no-op silenciosa.
            // Pending/Failed: nada que refundar.
            string currentStatus = payment.Status.ToString();
            LogRefundUnexpectedStatus(logger, orderId, currentStatus);
            return;
        }

        Result<PaymentRefundResult> refundGateway = await gateway.RefundAsync(
            payment.ProviderPaymentIntentId!,
            message.Amount,
            message.Currency,
            idempotencyKey: $"pmt-refund-{orderId}",
            context.CancellationToken);

        if (refundGateway.IsFailure)
        {
            // Fallo transitorio del gateway -> throw para retry de MassTransit.
            string refundErrorCode = refundGateway.Error.Code;
            string refundErrorDescription = refundGateway.Error.Description;
            LogRefundGatewayFailed(logger, orderId, refundErrorCode);
            throw new InvalidOperationException(
                $"Refund gateway failed for OrderId={orderId}: {refundErrorCode} - {refundErrorDescription}");
        }

        Result markRefunded = payment.MarkRefunded(refundGateway.Value.ProviderRefundId, clock.UtcNow);
        if (markRefunded.IsFailure)
        {
            string conflictCode = markRefunded.Error.Code;
            LogRefundAggregateConflict(logger, orderId, conflictCode);
            throw new InvalidOperationException(
                $"Payment.MarkRefunded conflict for OrderId={orderId}: {conflictCode}");
        }

        await unitOfWork.SaveChangesAsync(context.CancellationToken);

        // Publicar RefundCompleted CON el SaveChanges previo en la misma transaccion gracias al
        // AddConfigureEndpointsCallback con UseEntityFrameworkOutbox<PaymentsDbContext> — sin ese
        // wrapping, el publish quedaria buffered al outbox y nunca se enviaria (bug #7 de Orders).
        await context.Publish(
            new RefundCompletedIntegrationEvent
            {
                OrderId = orderId,
                RefundId = Guid.CreateVersion7(),
            },
            context.CancellationToken);

        string providerRefundId = refundGateway.Value.ProviderRefundId;
        LogRefunded(logger, orderId, providerRefundId);
    }

    private async Task CompensateAsync(Guid orderId, string reason, CancellationToken cancellationToken)
    {
        Payment? payment = await repository.GetByOrderIdAsync(orderId, cancellationToken);
        if (payment is null)
        {
            // Cancelacion pre-AwaitingPayment (Orders rechazado por merchant antes de pago): no hay
            // nada que compensar. No-op silencioso.
            LogCompensateNoPayment(logger, orderId);
            return;
        }

        switch (payment.Status)
        {
            case PaymentStatus.Authorized:
                // VOID: cancela el hold sin cobrar nada.
                Result<PaymentVoidResult> voidGateway = await gateway.VoidAsync(
                    payment.ProviderPaymentIntentId!,
                    idempotencyKey: $"pmt-void-{orderId}",
                    reason,
                    cancellationToken);
                if (voidGateway.IsFailure)
                {
                    string voidErrorCode = voidGateway.Error.Code;
                    string voidErrorDescription = voidGateway.Error.Description;
                    LogVoidGatewayFailed(logger, orderId, voidErrorCode);
                    throw new InvalidOperationException(
                        $"Void gateway failed for OrderId={orderId}: {voidErrorCode} - {voidErrorDescription}");
                }

                Result voidResult = payment.Void(reason, clock.UtcNow);
                if (voidResult.IsFailure)
                {
                    string voidConflictCode = voidResult.Error.Code;
                    LogVoidAggregateConflict(logger, orderId, voidConflictCode);
                    throw new InvalidOperationException(
                        $"Payment.Void conflict for OrderId={orderId}: {voidConflictCode}");
                }

                await unitOfWork.SaveChangesAsync(cancellationToken);
                LogVoided(logger, orderId, reason);
                break;

            case PaymentStatus.Captured:
                // Dinero ya cobrado: NO auto-refund (politica). Marca para revision humana.
                // Si la saga decide refundar, emite RefundRequested separadamente (manejado arriba).
                Result needsReview = payment.MarkNeedsReview(reason, clock.UtcNow);
                if (needsReview.IsFailure)
                {
                    string needsReviewCode = needsReview.Error.Code;
                    LogNeedsReviewConflict(logger, orderId, needsReviewCode);
                    throw new InvalidOperationException(
                        $"Payment.MarkNeedsReview conflict for OrderId={orderId}: {needsReviewCode}");
                }

                await unitOfWork.SaveChangesAsync(cancellationToken);
                LogNeedsReview(logger, orderId, reason);
                break;

            case PaymentStatus.Pending:
            case PaymentStatus.Voided:
            case PaymentStatus.Failed:
            case PaymentStatus.NeedsReview:
            case PaymentStatus.Refunded:
                // Re-entrega o cancelacion concurrente: ya manejado. No-op.
                string noOpStatus = payment.Status.ToString();
                LogCompensateNoOp(logger, orderId, noOpStatus);
                return;
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Compensacion del pedido {OrderId} sin Payment registrado: no-op.")]
    private static partial void LogCompensateNoPayment(ILogger logger, Guid orderId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Pago {OrderId} voided (hold cancelado) por: {Reason}.")]
    private static partial void LogVoided(ILogger logger, Guid orderId, string reason);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Pago {OrderId} marcado NeedsReview (capturado + compensacion); humano debe decidir refund. Razon: {Reason}.")]
    private static partial void LogNeedsReview(ILogger logger, Guid orderId, string reason);

    [LoggerMessage(Level = LogLevel.Information, Message = "Compensacion del pedido {OrderId} en estado {Status}: no-op (re-entrega o cancelacion concurrente).")]
    private static partial void LogCompensateNoOp(ILogger logger, Guid orderId, string status);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Gateway fallo en VOID del pedido {OrderId}: {ErrorCode}. Se relanza para retry de MassTransit.")]
    private static partial void LogVoidGatewayFailed(ILogger logger, Guid orderId, string errorCode);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Conflicto en Payment.Void para el pedido {OrderId}: {ErrorCode}.")]
    private static partial void LogVoidAggregateConflict(ILogger logger, Guid orderId, string errorCode);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Conflicto en Payment.MarkNeedsReview para el pedido {OrderId}: {ErrorCode}.")]
    private static partial void LogNeedsReviewConflict(ILogger logger, Guid orderId, string errorCode);

    [LoggerMessage(Level = LogLevel.Information, Message = "RefundRequested para el pedido {OrderId} sin Payment registrado: no-op.")]
    private static partial void LogRefundNoPayment(ILogger logger, Guid orderId);

    [LoggerMessage(Level = LogLevel.Information, Message = "RefundRequested para el pedido {OrderId} en estado inesperado {Status}: no-op (solo se refunda Captured).")]
    private static partial void LogRefundUnexpectedStatus(ILogger logger, Guid orderId, string status);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Gateway fallo en REFUND del pedido {OrderId}: {ErrorCode}. Se relanza para retry de MassTransit.")]
    private static partial void LogRefundGatewayFailed(ILogger logger, Guid orderId, string errorCode);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Conflicto en Payment.MarkRefunded para el pedido {OrderId}: {ErrorCode}.")]
    private static partial void LogRefundAggregateConflict(ILogger logger, Guid orderId, string errorCode);

    [LoggerMessage(Level = LogLevel.Information, Message = "Pago {OrderId} reembolsado (refund {ProviderRefundId}).")]
    private static partial void LogRefunded(ILogger logger, Guid orderId, string providerRefundId);
}
