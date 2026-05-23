using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.Payments.Application.Abstractions;
using Rappix.Payments.Application.Configuration;
using Rappix.Payments.Infrastructure.Gateways;
using Xunit;

namespace Rappix.Payments.Tests.Integration;

/// <summary>
/// Smoke tests del <see cref="StripePaymentGateway"/> contra Stripe TEST MODE real. Usan
/// <see cref="SkippableFactAttribute"/>: si la env var <c>STRIPE_API_KEY</c> no esta definida, los
/// tests se SALTAN sin fallar. <b>NO corren en CI por defecto</b> — son una herramienta de
/// validacion manual ocasional para confirmar que el wrap sigue alineado con la SDK de Stripe.
/// </summary>
/// <remarks>
/// Para correrlos localmente:
/// <code>
///   $env:STRIPE_API_KEY = "sk_test_..."
///   dotnet test --filter "FullyQualifiedName~StripePaymentGatewayTests"
/// </code>
/// Cada test crea un PaymentIntent o Refund REAL en tu cuenta Stripe de test (consume creditos de
/// tu rate limit). No usar contra produccion.
/// </remarks>
public sealed class StripePaymentGatewayTests
{
    private const string EnvVarName = "STRIPE_API_KEY";

    private static StripePaymentGateway NewGateway(string apiKey)
    {
        var options = Options.Create(new PaymentsOptions
        {
            Gateway = "Stripe",
            Stripe = new PaymentsOptions.StripeOptions { ApiKey = apiKey, WebhookSecret = null },
        });
        return new StripePaymentGateway(options, NullLogger<StripePaymentGateway>.Instance);
    }

    [SkippableFact]
    public async Task AuthorizeAsync_WithRealStripeTestMode_ReturnsRequiresCaptureStatus()
    {
        string? apiKey = Environment.GetEnvironmentVariable(EnvVarName);
        Skip.If(string.IsNullOrEmpty(apiKey), $"{EnvVarName} no definida — saltando smoke contra Stripe real.");

        StripePaymentGateway gateway = NewGateway(apiKey!);
        var request = new AuthorizeRequest(
            OrderId: Guid.CreateVersion7(),
            CustomerUserId: Guid.CreateVersion7(),
            Amount: 1.00m, // USD 1.00 — minimo aceptado
            Currency: "USD",
            IdempotencyKey: $"smoke-auth-{Guid.CreateVersion7():N}",
            CustomerReference: "rappix-payments-smoke");

        Result<PaymentAuthorizationResult> result = await gateway.AuthorizeAsync(request, CancellationToken.None);

        result.IsSuccess.Should().BeTrue($"AuthorizeAsync deberia tener exito en test mode con pm_card_visa. Error: {result.Error.Description}");
        result.Value.ProviderPaymentIntentId.Should().StartWith("pi_");
        result.Value.ProviderStatus.Should().Be("requires_capture");
    }

    [SkippableFact]
    public async Task AuthorizeAsync_SameIdempotencyKey_ReturnsSameIntentId()
    {
        // Verifica el comportamiento de la Idempotency-Key de Stripe (Nivel 3 de la defensa contra
        // doble cobro): dos llamadas con la misma key devuelven el MISMO PaymentIntent (cached 24h
        // en Stripe).
        string? apiKey = Environment.GetEnvironmentVariable(EnvVarName);
        Skip.If(string.IsNullOrEmpty(apiKey), $"{EnvVarName} no definida — saltando smoke contra Stripe real.");

        StripePaymentGateway gateway = NewGateway(apiKey!);
        string sharedKey = $"smoke-idem-{Guid.CreateVersion7():N}";
        var request = new AuthorizeRequest(
            OrderId: Guid.CreateVersion7(),
            CustomerUserId: Guid.CreateVersion7(),
            Amount: 1.00m,
            Currency: "USD",
            IdempotencyKey: sharedKey,
            CustomerReference: null);

        Result<PaymentAuthorizationResult> first = await gateway.AuthorizeAsync(request, CancellationToken.None);
        Result<PaymentAuthorizationResult> second = await gateway.AuthorizeAsync(request, CancellationToken.None);

        first.IsSuccess.Should().BeTrue();
        second.IsSuccess.Should().BeTrue();
        second.Value.ProviderPaymentIntentId.Should().Be(first.Value.ProviderPaymentIntentId,
            "Stripe debe devolver el mismo PaymentIntent al usar la misma Idempotency-Key (Nivel 3 de la defensa contra doble cobro).");
    }

    [SkippableFact]
    public async Task CaptureAsync_AfterAuthorize_TransitionsToSucceeded()
    {
        string? apiKey = Environment.GetEnvironmentVariable(EnvVarName);
        Skip.If(string.IsNullOrEmpty(apiKey), $"{EnvVarName} no definida — saltando smoke contra Stripe real.");

        StripePaymentGateway gateway = NewGateway(apiKey!);
        Guid orderId = Guid.CreateVersion7();
        var authRequest = new AuthorizeRequest(orderId, Guid.CreateVersion7(), 1.00m, "USD",
            IdempotencyKey: $"smoke-cap-auth-{orderId:N}", CustomerReference: null);

        Result<PaymentAuthorizationResult> auth = await gateway.AuthorizeAsync(authRequest, CancellationToken.None);
        Skip.IfNot(auth.IsSuccess, $"Authorize previo fallo: {auth.Error.Description}");

        Result<PaymentCaptureResult> capture = await gateway.CaptureAsync(
            auth.Value.ProviderPaymentIntentId,
            idempotencyKey: $"smoke-cap-{orderId:N}",
            CancellationToken.None);

        capture.IsSuccess.Should().BeTrue($"CaptureAsync fallo: {capture.Error.Description}");
        capture.Value.ProviderPaymentIntentId.Should().Be(auth.Value.ProviderPaymentIntentId);
    }

    [SkippableFact]
    public async Task VoidAsync_AfterAuthorize_CancelsTheIntent()
    {
        string? apiKey = Environment.GetEnvironmentVariable(EnvVarName);
        Skip.If(string.IsNullOrEmpty(apiKey), $"{EnvVarName} no definida — saltando smoke contra Stripe real.");

        StripePaymentGateway gateway = NewGateway(apiKey!);
        Guid orderId = Guid.CreateVersion7();
        var authRequest = new AuthorizeRequest(orderId, Guid.CreateVersion7(), 1.00m, "USD",
            IdempotencyKey: $"smoke-void-auth-{orderId:N}", CustomerReference: null);

        Result<PaymentAuthorizationResult> auth = await gateway.AuthorizeAsync(authRequest, CancellationToken.None);
        Skip.IfNot(auth.IsSuccess, $"Authorize previo fallo: {auth.Error.Description}");

        Result<PaymentVoidResult> voidResult = await gateway.VoidAsync(
            auth.Value.ProviderPaymentIntentId,
            idempotencyKey: $"smoke-void-{orderId:N}",
            "smoke test",
            CancellationToken.None);

        voidResult.IsSuccess.Should().BeTrue($"VoidAsync fallo: {voidResult.Error.Description}");
        voidResult.Value.ProviderPaymentIntentId.Should().Be(auth.Value.ProviderPaymentIntentId);
    }

    [SkippableFact]
    public async Task RefundAsync_AfterCapture_ReturnsRefundId()
    {
        string? apiKey = Environment.GetEnvironmentVariable(EnvVarName);
        Skip.If(string.IsNullOrEmpty(apiKey), $"{EnvVarName} no definida — saltando smoke contra Stripe real.");

        StripePaymentGateway gateway = NewGateway(apiKey!);
        Guid orderId = Guid.CreateVersion7();

        // Authorize + Capture en cadena para tener un intent capturado.
        var authRequest = new AuthorizeRequest(orderId, Guid.CreateVersion7(), 1.00m, "USD",
            IdempotencyKey: $"smoke-refund-auth-{orderId:N}", CustomerReference: null);
        Result<PaymentAuthorizationResult> auth = await gateway.AuthorizeAsync(authRequest, CancellationToken.None);
        Skip.IfNot(auth.IsSuccess, $"Authorize previo fallo: {auth.Error.Description}");

        Result<PaymentCaptureResult> capture = await gateway.CaptureAsync(
            auth.Value.ProviderPaymentIntentId,
            idempotencyKey: $"smoke-refund-cap-{orderId:N}",
            CancellationToken.None);
        Skip.IfNot(capture.IsSuccess, $"Capture previo fallo: {capture.Error.Description}");

        // Ahora el refund.
        Result<PaymentRefundResult> refund = await gateway.RefundAsync(
            auth.Value.ProviderPaymentIntentId,
            amount: 1.00m,
            currency: "USD",
            idempotencyKey: $"smoke-refund-{orderId:N}",
            CancellationToken.None);

        refund.IsSuccess.Should().BeTrue($"RefundAsync fallo: {refund.Error.Description}");
        refund.Value.ProviderRefundId.Should().StartWith("re_");
    }

    [Fact]
    public void Constructor_WithoutApiKey_ThrowsInvalidOperationException()
    {
        // Test NO requiere STRIPE_API_KEY (no llama a Stripe; valida solo la guard del constructor).
        var options = Options.Create(new PaymentsOptions
        {
            Gateway = "Stripe",
            Stripe = new PaymentsOptions.StripeOptions { ApiKey = "", WebhookSecret = null },
        });

        Action action = () => _ = new StripePaymentGateway(options, NullLogger<StripePaymentGateway>.Instance);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*requiere Payments:Stripe:ApiKey*");
    }
}
