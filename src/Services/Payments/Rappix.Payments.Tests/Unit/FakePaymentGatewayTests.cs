using FluentAssertions;
using Microsoft.Extensions.Options;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.Payments.Application.Abstractions;
using Rappix.Payments.Application.Configuration;
using Rappix.Payments.Infrastructure.Gateways;

namespace Rappix.Payments.Tests.Unit;

/// <summary>
/// Tests del <see cref="FakePaymentGateway"/>. Cubre la invariante critica: misma
/// <c>idempotencyKey</c> devuelve el MISMO <c>ProviderPaymentIntentId</c>/<c>ProviderRefundId</c>
/// (Nivel 3 de la idempotencia de dinero en clones in-process — Stripe.net ofrece la misma garantia
/// real). Tambien valida el aislamiento de estado entre instancias (vigilance item #2).
/// </summary>
public sealed class FakePaymentGatewayTests
{
    private const string OrderId = "11111111-1111-1111-1111-111111111111";

    private static FakePaymentGateway NewGateway(string mode = "Always")
    {
        var opts = Options.Create(new PaymentsOptions { Fake = new PaymentsOptions.FakeOptions { Mode = mode } });
        return new FakePaymentGateway(opts);
    }

    private static AuthorizeRequest NewRequest(string key) =>
        new(OrderId: Guid.Parse(OrderId), CustomerUserId: Guid.CreateVersion7(), Amount: 250m, Currency: "DOP",
            IdempotencyKey: key, CustomerReference: null);

    [Fact]
    public async Task AuthorizeAsync_SameIdempotencyKey_ReturnsSameIntentId()
    {
        FakePaymentGateway gateway = NewGateway();

        Result<PaymentAuthorizationResult> first = await gateway.AuthorizeAsync(NewRequest("pmt-auth-A"), CancellationToken.None);
        Result<PaymentAuthorizationResult> second = await gateway.AuthorizeAsync(NewRequest("pmt-auth-A"), CancellationToken.None);

        first.IsSuccess.Should().BeTrue();
        second.IsSuccess.Should().BeTrue();
        second.Value.ProviderPaymentIntentId.Should().Be(first.Value.ProviderPaymentIntentId);
        first.Value.ProviderPaymentIntentId.Should().StartWith("pi_fake_");
        first.Value.ProviderStatus.Should().Be("requires_capture");
    }

    [Fact]
    public async Task AuthorizeAsync_DifferentIdempotencyKeys_ReturnDifferentIntentIds()
    {
        FakePaymentGateway gateway = NewGateway();

        Result<PaymentAuthorizationResult> a = await gateway.AuthorizeAsync(NewRequest("pmt-auth-A"), CancellationToken.None);
        Result<PaymentAuthorizationResult> b = await gateway.AuthorizeAsync(NewRequest("pmt-auth-B"), CancellationToken.None);

        a.IsSuccess.Should().BeTrue();
        b.IsSuccess.Should().BeTrue();
        a.Value.ProviderPaymentIntentId.Should().NotBe(b.Value.ProviderPaymentIntentId);
    }

    [Fact]
    public async Task AuthorizeAsync_ModeAlwaysFail_ReturnsFailure()
    {
        FakePaymentGateway gateway = NewGateway(mode: "AlwaysFail");

        Result<PaymentAuthorizationResult> result = await gateway.AuthorizeAsync(NewRequest("pmt-auth-X"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Payments.Fake.Rejected");
    }

    [Fact]
    public async Task TwoSeparateInstances_DoNotShareState()
    {
        // VIGILANCE ITEM #2 del plan: el dict es INSTANCE-LEVEL. Dos instancias separadas (como las
        // que crea cada IAsyncLifetime por test) NO comparten respuestas — sin esta garantia, un test
        // anterior podria "ya tener" el intent del test actual y enmascarar bugs.
        FakePaymentGateway alpha = NewGateway();
        FakePaymentGateway beta = NewGateway();

        Result<PaymentAuthorizationResult> a = await alpha.AuthorizeAsync(NewRequest("pmt-auth-shared"), CancellationToken.None);
        Result<PaymentAuthorizationResult> b = await beta.AuthorizeAsync(NewRequest("pmt-auth-shared"), CancellationToken.None);

        a.Value.ProviderPaymentIntentId.Should().NotBe(b.Value.ProviderPaymentIntentId);
    }

    [Fact]
    public async Task CaptureAsync_SameKey_ReturnsSameIntentId_AlwaysSucceeds()
    {
        FakePaymentGateway gateway = NewGateway();
        const string intentId = "pi_fake_001";

        Result<PaymentCaptureResult> first = await gateway.CaptureAsync(intentId, "pmt-cap-A", CancellationToken.None);
        Result<PaymentCaptureResult> second = await gateway.CaptureAsync(intentId, "pmt-cap-A", CancellationToken.None);

        first.IsSuccess.Should().BeTrue();
        second.IsSuccess.Should().BeTrue();
        first.Value.ProviderPaymentIntentId.Should().Be(intentId);
        second.Value.ProviderPaymentIntentId.Should().Be(intentId);
    }

    [Fact]
    public async Task VoidAsync_AlwaysSucceeds_ReturnsIntentId()
    {
        FakePaymentGateway gateway = NewGateway();
        const string intentId = "pi_fake_002";

        Result<PaymentVoidResult> result = await gateway.VoidAsync(intentId, "pmt-void-A", "merchant rejected", CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.ProviderPaymentIntentId.Should().Be(intentId);
    }

    [Fact]
    public async Task RefundAsync_SameKey_ReturnsSameRefundId()
    {
        FakePaymentGateway gateway = NewGateway();

        Result<PaymentRefundResult> first = await gateway.RefundAsync("pi_fake_003", 100m, "DOP", "pmt-refund-A", CancellationToken.None);
        Result<PaymentRefundResult> second = await gateway.RefundAsync("pi_fake_003", 100m, "DOP", "pmt-refund-A", CancellationToken.None);

        first.IsSuccess.Should().BeTrue();
        first.Value.ProviderRefundId.Should().StartWith("re_fake_");
        second.Value.ProviderRefundId.Should().Be(first.Value.ProviderRefundId);
    }

    [Fact]
    public async Task RefundAsync_DifferentKeys_ReturnDifferentRefundIds()
    {
        FakePaymentGateway gateway = NewGateway();

        Result<PaymentRefundResult> a = await gateway.RefundAsync("pi_fake_003", 100m, "DOP", "pmt-refund-A", CancellationToken.None);
        Result<PaymentRefundResult> b = await gateway.RefundAsync("pi_fake_003", 100m, "DOP", "pmt-refund-B", CancellationToken.None);

        a.Value.ProviderRefundId.Should().NotBe(b.Value.ProviderRefundId);
    }
}
