using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.Payments.Application.Abstractions;
using Rappix.Payments.Application.Configuration;
using Stripe;

namespace Rappix.Payments.Infrastructure.Gateways;

/// <summary>
/// Implementacion real de <see cref="IPaymentGateway"/> contra Stripe (test mode o produccion).
/// Wrap delgado sobre <see cref="PaymentIntentService"/> + <see cref="RefundService"/> con
/// <see cref="RequestOptions.IdempotencyKey"/> para garantizar no doble cobro ante timeouts HTTP
/// mid-flight (Nivel 3 de la idempotencia de dinero, ADR-0009 #5).
/// </summary>
/// <remarks>
/// <para>
/// <b>Gap consciente Fase 8</b>: <c>PaymentMethod = "pm_card_visa"</c> esta hardcoded — es la
/// unica forma de ejecutar un Authorize no-interactivo en test mode. Produccion real requiere que
/// el cliente provea un <c>PaymentMethodId</c> desde el frontend (Stripe Elements/PaymentSheet),
/// que esta fuera de scope de Fase 8 (sin frontend con UI de pago). Documentado en ADR-0009 #8.
/// </para>
/// <para>
/// <b>Conversion de monto</b>: Stripe usa la unidad menor de la moneda (centavos). Se redondea a
/// 2 decimales antes de multiplicar por 100 para no perder precision (Money.Amount es decimal(19,4)
/// pero las monedas soportadas en Fase 8 — DOP/USD — usan 2 decimales). Si en el futuro entran
/// monedas zero-decimal (JPY, KRW) o de 3 decimales (KWD), aqui se debe consultar la tabla de
/// Stripe sobre minor units por currency.
/// </para>
/// <para>
/// Todos los catch (<see cref="StripeException"/>) mapean a <c>Result.Failure</c> con un codigo
/// tipado <c>Payments.Stripe.{StripeError.Code}</c> que los consumers traducen consistentemente.
/// </para>
/// </remarks>
internal sealed partial class StripePaymentGateway : IPaymentGateway
{
    private readonly PaymentIntentService _intentService;
    private readonly RefundService _refundService;
    private readonly ILogger<StripePaymentGateway> _logger;

    public StripePaymentGateway(IOptions<PaymentsOptions> options, ILogger<StripePaymentGateway> logger)
    {
        string? apiKey = options.Value.Stripe?.ApiKey;
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException(
                "StripePaymentGateway requiere Payments:Stripe:ApiKey configurado (sk_test_... o sk_live_...).");
        }

        var client = new StripeClient(apiKey);
        _intentService = new PaymentIntentService(client);
        _refundService = new RefundService(client);
        _logger = logger;
    }

    public async Task<Result<PaymentAuthorizationResult>> AuthorizeAsync(AuthorizeRequest request, CancellationToken cancellationToken)
    {
        var createOptions = new PaymentIntentCreateOptions
        {
            Amount = ToMinorUnit(request.Amount),
            Currency = request.Currency.ToLowerInvariant(),
            CaptureMethod = "manual",
            Confirm = true,
            PaymentMethod = "pm_card_visa",
            Metadata = new Dictionary<string, string>
            {
                ["order_id"] = request.OrderId.ToString(),
                ["customer_user_id"] = request.CustomerUserId.ToString(),
                ["customer_reference"] = request.CustomerReference ?? string.Empty,
            },
        };

        var requestOptions = new RequestOptions { IdempotencyKey = request.IdempotencyKey };

        try
        {
            PaymentIntent intent = await _intentService.CreateAsync(createOptions, requestOptions, cancellationToken);
            return Result.Success(new PaymentAuthorizationResult(intent.Id, intent.Status));
        }
        catch (StripeException ex)
        {
            string code = ex.StripeError?.Code ?? "unknown";
            string description = ex.StripeError?.Message ?? ex.Message;
            LogStripeAuthorizeFailed(_logger, request.OrderId, code, description);
            return Result.Failure<PaymentAuthorizationResult>(
                Error.Failure($"Payments.Stripe.{code}", description));
        }
    }

    public async Task<Result<PaymentCaptureResult>> CaptureAsync(string providerPaymentIntentId, string idempotencyKey, CancellationToken cancellationToken)
    {
        var requestOptions = new RequestOptions { IdempotencyKey = idempotencyKey };
        try
        {
            PaymentIntent intent = await _intentService.CaptureAsync(
                providerPaymentIntentId,
                new PaymentIntentCaptureOptions(),
                requestOptions,
                cancellationToken);
            return Result.Success(new PaymentCaptureResult(intent.Id));
        }
        catch (StripeException ex)
        {
            string code = ex.StripeError?.Code ?? "unknown";
            string description = ex.StripeError?.Message ?? ex.Message;
            LogStripeCaptureFailed(_logger, providerPaymentIntentId, code, description);
            return Result.Failure<PaymentCaptureResult>(
                Error.Failure($"Payments.Stripe.{code}", description));
        }
    }

    public async Task<Result<PaymentVoidResult>> VoidAsync(string providerPaymentIntentId, string idempotencyKey, string reason, CancellationToken cancellationToken)
    {
        var cancelOptions = new PaymentIntentCancelOptions
        {
            // Stripe acepta: duplicate, fraudulent, requested_by_customer, abandoned. Mapping conservador:
            CancellationReason = "requested_by_customer",
        };
        var requestOptions = new RequestOptions { IdempotencyKey = idempotencyKey };

        try
        {
            PaymentIntent intent = await _intentService.CancelAsync(
                providerPaymentIntentId,
                cancelOptions,
                requestOptions,
                cancellationToken);
            return Result.Success(new PaymentVoidResult(intent.Id));
        }
        catch (StripeException ex)
        {
            string code = ex.StripeError?.Code ?? "unknown";
            string description = ex.StripeError?.Message ?? ex.Message;
            LogStripeVoidFailed(_logger, providerPaymentIntentId, code, description);
            return Result.Failure<PaymentVoidResult>(
                Error.Failure($"Payments.Stripe.{code}", description));
        }
    }

    public async Task<Result<PaymentRefundResult>> RefundAsync(string providerPaymentIntentId, decimal amount, string currency, string idempotencyKey, CancellationToken cancellationToken)
    {
        // currency no se usa en RefundCreateOptions (Stripe lo infiere del PaymentIntent original);
        // se mantiene en la firma del IPaymentGateway por simetria y para que el Fake pueda asertarlo.
        _ = currency;

        var refundOptions = new RefundCreateOptions
        {
            PaymentIntent = providerPaymentIntentId,
            Amount = ToMinorUnit(amount),
        };
        var requestOptions = new RequestOptions { IdempotencyKey = idempotencyKey };

        try
        {
            Refund refund = await _refundService.CreateAsync(refundOptions, requestOptions, cancellationToken);
            return Result.Success(new PaymentRefundResult(refund.Id));
        }
        catch (StripeException ex)
        {
            string code = ex.StripeError?.Code ?? "unknown";
            string description = ex.StripeError?.Message ?? ex.Message;
            LogStripeRefundFailed(_logger, providerPaymentIntentId, code, description);
            return Result.Failure<PaymentRefundResult>(
                Error.Failure($"Payments.Stripe.{code}", description));
        }
    }

    /// <summary>Convierte un monto decimal a la unidad menor de la moneda (centavos para DOP/USD).</summary>
    private static long ToMinorUnit(decimal amount)
    {
        // Redondea a 2 decimales antes de multiplicar por 100 (DOP/USD usan 2 decimales).
        decimal cents = decimal.Round(amount, 2, MidpointRounding.ToEven) * 100m;
        return (long)cents;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Stripe AuthorizeAsync fallo para OrderId={OrderId}: {Code} - {Description}")]
    private static partial void LogStripeAuthorizeFailed(ILogger logger, Guid orderId, string code, string description);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Stripe CaptureAsync fallo para IntentId={IntentId}: {Code} - {Description}")]
    private static partial void LogStripeCaptureFailed(ILogger logger, string intentId, string code, string description);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Stripe CancelAsync (void) fallo para IntentId={IntentId}: {Code} - {Description}")]
    private static partial void LogStripeVoidFailed(ILogger logger, string intentId, string code, string description);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Stripe RefundAsync fallo para IntentId={IntentId}: {Code} - {Description}")]
    private static partial void LogStripeRefundFailed(ILogger logger, string intentId, string code, string description);
}
