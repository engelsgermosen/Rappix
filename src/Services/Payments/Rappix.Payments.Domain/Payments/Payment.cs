using Rappix.BuildingBlocks.Core.Domain;
using Rappix.BuildingBlocks.Core.Results;

namespace Rappix.Payments.Domain.Payments;

/// <summary>
/// Pago de un pedido en el modelo hold+capture. Una fila por <c>OrderId</c>; el <see cref="Entity{TId}.Id"/>
/// ES el <c>OrderId</c> externo, asi el PK natural actua como unique constraint para impedir doble-insercion
/// concurrente (Nivel 2 de idempotencia de dinero, ver plan Fase 8 seccion 4 y ADR-0009).
/// </summary>
/// <remarks>
/// Decisiones de diseno:
/// <list type="bullet">
/// <item>Hereda de <see cref="Entity{TId}"/> y NO de <c>AggregateRoot</c>: Payments no emite
///   <c>IDomainEvent</c>; las <c>IntegrationEvent</c> las publican los consumers directamente con el
///   contrato de <c>Rappix.Contracts.Payments</c> (mismo patron de <c>OrderTracking</c>).</item>
/// <item>Identidad <c>Payment.Id == OrderId</c> (decision usuario + ADR-0009 #4): cardinalidad 1:1 con
///   Order y el PK natural protege contra inserts duplicados sin indices adicionales.</item>
/// <item>Todas las transiciones devuelven <see cref="Result"/> y son IDEMPOTENTES con los mismos
///   identificadores. Critico para sobrevivir a re-entregas del broker sin doble cobro.</item>
/// <item>Concurrencia optimista por columna shadow <c>xmin</c> (configurada en EF).</item>
/// </list>
/// </remarks>
public sealed class Payment : Entity<Guid>
{
    // EF necesita ctor sin parametros para materializar.
    private Payment() { }

    private Payment(Guid orderId, Guid customerUserId, Money amount, DateTime utcNow)
        : base(orderId)
    {
        CustomerUserId = customerUserId;
        Amount = amount;
        Status = PaymentStatus.Pending;
        CreatedAtUtc = utcNow;
        UpdatedAtUtc = utcNow;
    }

    /// <summary>Cliente al que se le cobra (cache del <c>PaymentRequestedIntegrationEvent</c>).</summary>
    public Guid CustomerUserId { get; private set; }

    /// <summary>Monto + moneda. Inmutable tras la creacion.</summary>
    public Money Amount { get; private set; } = null!;

    /// <summary>Estado actual en la maquina hold+capture.</summary>
    public PaymentStatus Status { get; private set; }

    /// <summary>
    /// Id del PaymentIntent en el proveedor (e.g. <c>pi_xxx</c> en Stripe, <c>pi_fake_xxx</c> en Fake).
    /// Null mientras <see cref="Status"/> es <see cref="PaymentStatus.Pending"/>; estable tras Authorize.
    /// </summary>
    public string? ProviderPaymentIntentId { get; private set; }

    /// <summary>Id del refund cuando <see cref="Status"/> es <see cref="PaymentStatus.Refunded"/>.</summary>
    public string? ProviderRefundId { get; private set; }

    /// <summary>Razon textual de Fail/Void/NeedsReview (para auditoria).</summary>
    public string? Reason { get; private set; }

    /// <summary>Creacion (UTC).</summary>
    public DateTime CreatedAtUtc { get; private set; }

    /// <summary>Ultima actualizacion (UTC).</summary>
    public DateTime UpdatedAtUtc { get; private set; }

    /// <summary>Crea un <see cref="Payment"/> en estado <see cref="PaymentStatus.Pending"/>.</summary>
    public static Payment Create(Guid orderId, Guid customerUserId, Money amount, DateTime utcNow) =>
        new(orderId, customerUserId, amount, utcNow);

    /// <summary>
    /// Aplica el resultado del Authorize del gateway. Idempotente con el mismo intentId; conflicto
    /// si re-autoriza con uno distinto (estado inconsistente — el gateway no deberia regenerar el id
    /// con la misma Idempotency-Key, pero defensa en profundidad).
    /// </summary>
    public Result Authorize(string providerPaymentIntentId, DateTime utcNow)
    {
        switch (Status)
        {
            case PaymentStatus.Pending:
                ProviderPaymentIntentId = providerPaymentIntentId;
                Status = PaymentStatus.Authorized;
                UpdatedAtUtc = utcNow;
                return Result.Success();

            case PaymentStatus.Authorized:
                return string.Equals(ProviderPaymentIntentId, providerPaymentIntentId, StringComparison.Ordinal)
                    ? Result.Success()
                    : Result.Failure(PaymentErrors.AlreadyAuthorizedWithDifferentIntent);

            default:
                return Result.Failure(PaymentErrors.TerminalState);
        }
    }

    /// <summary>Captura un hold previamente autorizado. Idempotente desde <see cref="PaymentStatus.Captured"/>.</summary>
    public Result Capture(DateTime utcNow)
    {
        switch (Status)
        {
            case PaymentStatus.Authorized:
                Status = PaymentStatus.Captured;
                UpdatedAtUtc = utcNow;
                return Result.Success();

            case PaymentStatus.Captured:
                return Result.Success();

            case PaymentStatus.Pending:
                return Result.Failure(PaymentErrors.NotAuthorized);

            default:
                return Result.Failure(PaymentErrors.TerminalState);
        }
    }

    /// <summary>
    /// Cancela el hold (Authorized -> Voided) o escala a <see cref="PaymentStatus.NeedsReview"/> si el dinero
    /// ya fue capturado (consistente con el flujo <c>StockCommitFails_NeedsReview</c> de Orders:
    /// nunca auto-refund, humano decide). Idempotente desde Voided/NeedsReview.
    /// </summary>
    public Result Void(string reason, DateTime utcNow)
    {
        switch (Status)
        {
            case PaymentStatus.Authorized:
                Status = PaymentStatus.Voided;
                Reason = reason;
                UpdatedAtUtc = utcNow;
                return Result.Success();

            case PaymentStatus.Captured:
                Status = PaymentStatus.NeedsReview;
                Reason = reason;
                UpdatedAtUtc = utcNow;
                return Result.Success();

            case PaymentStatus.Voided:
            case PaymentStatus.NeedsReview:
                return Result.Success();

            case PaymentStatus.Pending:
                return Result.Failure(PaymentErrors.NotAuthorized);

            default:
                return Result.Failure(PaymentErrors.TerminalState);
        }
    }

    /// <summary>Marca el pago como fallido (rechazo del gateway, error no recuperable).</summary>
    public Result Fail(string reason, DateTime utcNow)
    {
        switch (Status)
        {
            case PaymentStatus.Pending:
            case PaymentStatus.Authorized:
                Status = PaymentStatus.Failed;
                Reason = reason;
                UpdatedAtUtc = utcNow;
                return Result.Success();

            case PaymentStatus.Failed:
                return Result.Success();

            default:
                return Result.Failure(PaymentErrors.TerminalState);
        }
    }

    /// <summary>
    /// Marca el pago capturado como necesitando revision humana. Llamado por el consumer de
    /// compensacion cuando un terminal de orden (Cancelled/Failed) llega despues de la captura.
    /// </summary>
    public Result MarkNeedsReview(string reason, DateTime utcNow)
    {
        switch (Status)
        {
            case PaymentStatus.Captured:
                Status = PaymentStatus.NeedsReview;
                Reason = reason;
                UpdatedAtUtc = utcNow;
                return Result.Success();

            case PaymentStatus.NeedsReview:
                return Result.Success();

            default:
                return Result.Failure(PaymentErrors.NotCaptured);
        }
    }

    /// <summary>
    /// Marca un refund completado tras un RefundAsync exitoso en el gateway. Idempotente con el mismo
    /// refundId; conflicto si se intenta con uno distinto (defensa contra doble refund). Acepta desde
    /// Captured o NeedsReview (humano marcando NeedsReview como refund manual).
    /// </summary>
    public Result MarkRefunded(string providerRefundId, DateTime utcNow)
    {
        switch (Status)
        {
            case PaymentStatus.Captured:
            case PaymentStatus.NeedsReview:
                ProviderRefundId = providerRefundId;
                Status = PaymentStatus.Refunded;
                UpdatedAtUtc = utcNow;
                return Result.Success();

            case PaymentStatus.Refunded:
                return string.Equals(ProviderRefundId, providerRefundId, StringComparison.Ordinal)
                    ? Result.Success()
                    : Result.Failure(PaymentErrors.AlreadyRefundedWithDifferentId);

            default:
                return Result.Failure(PaymentErrors.NotCaptured);
        }
    }
}
