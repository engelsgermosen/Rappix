using Rappix.BuildingBlocks.Core.Results;
using Rappix.Payments.Application.Abstractions;

namespace Rappix.Payments.Tests.Integration._Shared;

/// <summary>
/// Decorator de <see cref="IPaymentGateway"/> que cuenta cuantas veces se invoca cada operacion.
/// Critico para el test de DOBLE COBRO (commit 5): permite asertar
/// <c>AuthorizeCallCount == 1</c> aunque el consumer haya sido invocado dos veces con el mismo OrderId.
/// Usa <see cref="Interlocked.Increment(ref int)"/> para soportar conteo bajo paralelismo del harness.
/// </summary>
public sealed class CountingPaymentGateway(IPaymentGateway inner) : IPaymentGateway
{
    private int _authorizeCallCount;
    private int _captureCallCount;
    private int _voidCallCount;
    private int _refundCallCount;

    /// <summary>Numero de veces que se llamo a <see cref="AuthorizeAsync"/>.</summary>
    public int AuthorizeCallCount => Volatile.Read(ref _authorizeCallCount);

    /// <summary>Numero de veces que se llamo a <see cref="CaptureAsync"/>.</summary>
    public int CaptureCallCount => Volatile.Read(ref _captureCallCount);

    /// <summary>Numero de veces que se llamo a <see cref="VoidAsync"/>.</summary>
    public int VoidCallCount => Volatile.Read(ref _voidCallCount);

    /// <summary>Numero de veces que se llamo a <see cref="RefundAsync"/>.</summary>
    public int RefundCallCount => Volatile.Read(ref _refundCallCount);

    public Task<Result<PaymentAuthorizationResult>> AuthorizeAsync(AuthorizeRequest request, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _authorizeCallCount);
        return inner.AuthorizeAsync(request, cancellationToken);
    }

    public Task<Result<PaymentCaptureResult>> CaptureAsync(string providerPaymentIntentId, string idempotencyKey, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _captureCallCount);
        return inner.CaptureAsync(providerPaymentIntentId, idempotencyKey, cancellationToken);
    }

    public Task<Result<PaymentVoidResult>> VoidAsync(string providerPaymentIntentId, string idempotencyKey, string reason, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _voidCallCount);
        return inner.VoidAsync(providerPaymentIntentId, idempotencyKey, reason, cancellationToken);
    }

    public Task<Result<PaymentRefundResult>> RefundAsync(string providerPaymentIntentId, decimal amount, string currency, string idempotencyKey, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _refundCallCount);
        return inner.RefundAsync(providerPaymentIntentId, amount, currency, idempotencyKey, cancellationToken);
    }
}
