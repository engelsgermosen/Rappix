namespace Rappix.Payments.Application.Abstractions;

/// <summary>Resultado exitoso de <c>IPaymentGateway.AuthorizeAsync</c>.</summary>
/// <param name="ProviderPaymentIntentId">Id del PaymentIntent en el proveedor (e.g. <c>pi_xxx</c> en Stripe).</param>
/// <param name="ProviderStatus">Status crudo devuelto por el proveedor (e.g. <c>requires_capture</c>).</param>
public sealed record PaymentAuthorizationResult(string ProviderPaymentIntentId, string ProviderStatus);

/// <summary>Resultado exitoso de <c>IPaymentGateway.CaptureAsync</c>.</summary>
/// <param name="ProviderPaymentIntentId">Id del PaymentIntent capturado.</param>
public sealed record PaymentCaptureResult(string ProviderPaymentIntentId);

/// <summary>Resultado exitoso de <c>IPaymentGateway.VoidAsync</c>.</summary>
/// <param name="ProviderPaymentIntentId">Id del PaymentIntent cancelado.</param>
public sealed record PaymentVoidResult(string ProviderPaymentIntentId);

/// <summary>Resultado exitoso de <c>IPaymentGateway.RefundAsync</c>.</summary>
/// <param name="ProviderRefundId">Id del refund en el proveedor (e.g. <c>re_xxx</c> en Stripe).</param>
public sealed record PaymentRefundResult(string ProviderRefundId);
