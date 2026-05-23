using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.Payments.Application.Abstractions;
using Rappix.Payments.Application.Configuration;

namespace Rappix.Payments.Infrastructure.Gateways;

/// <summary>
/// Implementacion in-process de <see cref="IPaymentGateway"/> para smoke E2E y tests. Sin red, sin
/// dependencia de Stripe ni cuenta. Configurable via <c>Payments:Fake:Mode</c>.
/// </summary>
/// <remarks>
/// VIGILANCE ITEM #2 del plan Fase 8: los <see cref="ConcurrentDictionary{TKey,TValue}"/> son
/// INSTANCE-LEVEL (no <c>static</c>). Esto importa por aislamiento entre tests:
/// <list type="bullet">
/// <item>En produccion se registra como <c>Singleton</c>: una sola instancia por proceso, el dict
///   acumula intents durante toda la vida del servicio (correcto — es exactamente el
///   comportamiento de la idempotency-cache de Stripe a 24h).</item>
/// <item>En tests, cada <c>IAsyncLifetime</c> crea un <c>ServiceProvider</c> nuevo, lo que
///   instancia un <c>FakePaymentGateway</c> nuevo con dicts vacios. Sin estado leakeado entre
///   tests. Es exactamente la misma decision que llevo a usar
///   <c>IAsyncLifetime</c>-per-test en <c>CourierRequestedConsumerTests</c> de Dispatch.</item>
/// </list>
/// Si el dict fuese <c>static</c>, el estado se filtraria entre tests aunque cada uno tuviera su
/// propio <c>ServiceProvider</c> — bug catastrofico para tests de idempotencia.
/// </remarks>
internal sealed class FakePaymentGateway(IOptions<PaymentsOptions> options) : IPaymentGateway
{
    // 4 dicts independientes (uno por operacion): la idempotencia es por (operacion, OrderId), y los
    // ids generados son de naturaleza distinta (pi_ vs re_). GetOrAdd garantiza el invariante "misma
    // key -> mismo resultado" thread-safe.
    private readonly ConcurrentDictionary<string, string> _authorizeResults = new();
    private readonly ConcurrentDictionary<string, string> _captureResults = new();
    private readonly ConcurrentDictionary<string, string> _voidResults = new();
    private readonly ConcurrentDictionary<string, string> _refundResults = new();

    /// <summary>
    /// Crea un PaymentIntent fake <c>pi_fake_xxx</c> sincrono. Si <c>Payments:Fake:Mode=AlwaysFail</c>,
    /// devuelve Result.Failure (para tests negativos). Misma idempotencyKey siempre devuelve el mismo intentId.
    /// </summary>
    public Task<Result<PaymentAuthorizationResult>> AuthorizeAsync(AuthorizeRequest request, CancellationToken cancellationToken)
    {
        if (string.Equals(options.Value.Fake.Mode, "AlwaysFail", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(Result.Failure<PaymentAuthorizationResult>(
                Error.Failure("Payments.Fake.Rejected", "Fake gateway rechaza por configuracion (Mode=AlwaysFail).")));
        }

        string intentId = _authorizeResults.GetOrAdd(request.IdempotencyKey, _ => $"pi_fake_{Guid.CreateVersion7():N}");
        return Task.FromResult(Result.Success(new PaymentAuthorizationResult(intentId, "requires_capture")));
    }

    /// <summary>Marca el intent como capturado en el dict y devuelve OK. Mismo intent id, misma key -> mismo resultado.</summary>
    public Task<Result<PaymentCaptureResult>> CaptureAsync(string providerPaymentIntentId, string idempotencyKey, CancellationToken cancellationToken)
    {
        _captureResults.GetOrAdd(idempotencyKey, _ => providerPaymentIntentId);
        return Task.FromResult(Result.Success(new PaymentCaptureResult(providerPaymentIntentId)));
    }

    /// <summary>Marca el intent como cancelado y devuelve OK.</summary>
    public Task<Result<PaymentVoidResult>> VoidAsync(string providerPaymentIntentId, string idempotencyKey, string reason, CancellationToken cancellationToken)
    {
        _voidResults.GetOrAdd(idempotencyKey, _ => providerPaymentIntentId);
        return Task.FromResult(Result.Success(new PaymentVoidResult(providerPaymentIntentId)));
    }

    /// <summary>Crea un refund fake <c>re_fake_xxx</c> y devuelve OK. Misma key -> mismo refundId.</summary>
    public Task<Result<PaymentRefundResult>> RefundAsync(string providerPaymentIntentId, decimal amount, string currency, string idempotencyKey, CancellationToken cancellationToken)
    {
        string refundId = _refundResults.GetOrAdd(idempotencyKey, _ => $"re_fake_{Guid.CreateVersion7():N}");
        return Task.FromResult(Result.Success(new PaymentRefundResult(refundId)));
    }
}
