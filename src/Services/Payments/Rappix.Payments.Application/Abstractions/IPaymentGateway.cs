using Rappix.BuildingBlocks.Core.Results;

namespace Rappix.Payments.Application.Abstractions;

/// <summary>
/// Adaptador a una pasarela de pagos externa (Stripe en produccion, Fake en tests y smoke E2E).
/// El dominio jamas referencia Stripe.net directamente; toda interaccion con el proveedor pasa por
/// esta interfaz (Hexagonal/Ports-and-Adapters, ADR-0009 #1).
/// </summary>
/// <remarks>
/// Convencion de <c>idempotencyKey</c> (estable por operacion + OrderId, ver plan Fase 8 seccion 4):
/// <list type="bullet">
/// <item>Authorize: <c>$"pmt-auth-{orderId}"</c></item>
/// <item>Capture: <c>$"pmt-cap-{orderId}"</c></item>
/// <item>Void: <c>$"pmt-void-{orderId}"</c></item>
/// <item>Refund: <c>$"pmt-refund-{orderId}"</c></item>
/// </list>
/// Stripe cachea la respuesta 24h por idempotency key; reintento devuelve la misma sin doble cobro.
/// </remarks>
public interface IPaymentGateway
{
    /// <summary>Crea un hold (autorizacion) por el monto indicado. No mueve dinero todavia.</summary>
    Task<Result<PaymentAuthorizationResult>> AuthorizeAsync(AuthorizeRequest request, CancellationToken cancellationToken);

    /// <summary>Captura un hold previamente autorizado. Mueve el dinero al merchant.</summary>
    Task<Result<PaymentCaptureResult>> CaptureAsync(string providerPaymentIntentId, string idempotencyKey, CancellationToken cancellationToken);

    /// <summary>Cancela un hold sin capturar. El cliente nunca es cobrado.</summary>
    Task<Result<PaymentVoidResult>> VoidAsync(string providerPaymentIntentId, string idempotencyKey, string reason, CancellationToken cancellationToken);

    /// <summary>Reembolsa un pago ya capturado (refund completo en Fase 8; parcial es follow-up).</summary>
    Task<Result<PaymentRefundResult>> RefundAsync(string providerPaymentIntentId, decimal amount, string currency, string idempotencyKey, CancellationToken cancellationToken);
}
