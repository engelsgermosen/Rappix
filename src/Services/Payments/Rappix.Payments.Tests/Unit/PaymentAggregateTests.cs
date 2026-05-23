using FluentAssertions;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.Payments.Domain.Payments;

namespace Rappix.Payments.Tests.Unit;

/// <summary>
/// Pruebas unitarias de la maquina de estados del aggregate <see cref="Payment"/>. Cubre la matriz
/// de transiciones documentada en el plan Fase 8 seccion 2 con enfasis en la IDEMPOTENCIA
/// (mismo input dos veces = no-op) que es el Nivel 2 de la defensa contra doble cobro.
/// </summary>
public sealed class PaymentAggregateTests
{
    private static readonly DateTime Now = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Later = Now.AddMinutes(10);

    private static Payment NewPending()
    {
        Money money = Money.Create(250.50m, "DOP").Value;
        return Payment.Create(orderId: Guid.CreateVersion7(), customerUserId: Guid.CreateVersion7(), amount: money, utcNow: Now);
    }

    // -------- Create + Money validacion --------

    [Fact]
    public void Create_WithValidData_ReturnsPaymentInPending()
    {
        Guid orderId = Guid.CreateVersion7();
        Guid customerUserId = Guid.CreateVersion7();
        Money money = Money.Create(100m, "DOP").Value;

        Payment payment = Payment.Create(orderId, customerUserId, money, Now);

        payment.Id.Should().Be(orderId);
        payment.CustomerUserId.Should().Be(customerUserId);
        payment.Status.Should().Be(PaymentStatus.Pending);
        payment.Amount.Amount.Should().Be(100m);
        payment.Amount.Currency.Should().Be("DOP");
        payment.ProviderPaymentIntentId.Should().BeNull();
        payment.ProviderRefundId.Should().BeNull();
        payment.Reason.Should().BeNull();
        payment.CreatedAtUtc.Should().Be(Now);
        payment.UpdatedAtUtc.Should().Be(Now);
    }

    [Fact]
    public void Money_Create_NonPositive_ReturnsValidationError()
    {
        Result<Money> zero = Money.Create(0m, "DOP");
        Result<Money> negative = Money.Create(-1m, "DOP");

        zero.IsFailure.Should().BeTrue();
        zero.Error.Should().Be(MoneyErrors.NonPositive);
        negative.IsFailure.Should().BeTrue();
        negative.Error.Should().Be(MoneyErrors.NonPositive);
    }

    [Fact]
    public void Money_Create_InvalidCurrency_ReturnsValidationError()
    {
        Money.Create(10m, "").IsFailure.Should().BeTrue();
        Money.Create(10m, "DO").IsFailure.Should().BeTrue();          // muy corto
        Money.Create(10m, "DOPS").IsFailure.Should().BeTrue();        // muy largo
        Money.Create(10m, "D0P").IsFailure.Should().BeTrue();         // contiene digito
        Money.Create(10m, "DOP").Value.Currency.Should().Be("DOP");   // valido
        Money.Create(10m, "dop").Value.Currency.Should().Be("DOP");   // normaliza a uppercase
    }

    // -------- Authorize --------

    [Fact]
    public void Authorize_FromPending_TransitionsToAuthorized_AndSetsIntentId()
    {
        Payment payment = NewPending();

        Result result = payment.Authorize("pi_fake_001", Later);

        result.IsSuccess.Should().BeTrue();
        payment.Status.Should().Be(PaymentStatus.Authorized);
        payment.ProviderPaymentIntentId.Should().Be("pi_fake_001");
        payment.UpdatedAtUtc.Should().Be(Later);
    }

    [Fact]
    public void Authorize_FromAuthorized_SameIntentId_IsIdempotentNoOp()
    {
        Payment payment = NewPending();
        payment.Authorize("pi_fake_001", Now);

        Result result = payment.Authorize("pi_fake_001", Later);

        result.IsSuccess.Should().BeTrue();
        payment.Status.Should().Be(PaymentStatus.Authorized);
        payment.ProviderPaymentIntentId.Should().Be("pi_fake_001");
        // Idempotente: no-op no debe actualizar UpdatedAtUtc.
        payment.UpdatedAtUtc.Should().Be(Now);
    }

    [Fact]
    public void Authorize_FromAuthorized_DifferentIntentId_ReturnsConflict()
    {
        Payment payment = NewPending();
        payment.Authorize("pi_fake_001", Now);

        Result result = payment.Authorize("pi_fake_002", Later);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(PaymentErrors.AlreadyAuthorizedWithDifferentIntent);
        payment.ProviderPaymentIntentId.Should().Be("pi_fake_001");
    }

    [Fact]
    public void Authorize_FromCaptured_ReturnsTerminalStateError()
    {
        Payment payment = NewPending();
        payment.Authorize("pi_fake_001", Now);
        payment.Capture(Now);

        Result result = payment.Authorize("pi_fake_001", Later);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(PaymentErrors.TerminalState);
    }

    // -------- Capture --------

    [Fact]
    public void Capture_FromAuthorized_TransitionsToCaptured()
    {
        Payment payment = NewPending();
        payment.Authorize("pi_fake_001", Now);

        Result result = payment.Capture(Later);

        result.IsSuccess.Should().BeTrue();
        payment.Status.Should().Be(PaymentStatus.Captured);
        payment.UpdatedAtUtc.Should().Be(Later);
    }

    [Fact]
    public void Capture_FromCaptured_IsIdempotentNoOp()
    {
        Payment payment = NewPending();
        payment.Authorize("pi_fake_001", Now);
        payment.Capture(Now);

        Result result = payment.Capture(Later);

        result.IsSuccess.Should().BeTrue();
        payment.Status.Should().Be(PaymentStatus.Captured);
        payment.UpdatedAtUtc.Should().Be(Now); // no se actualiza en no-op
    }

    [Fact]
    public void Capture_FromPending_ReturnsNotAuthorized()
    {
        Payment payment = NewPending();

        Result result = payment.Capture(Later);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(PaymentErrors.NotAuthorized);
        payment.Status.Should().Be(PaymentStatus.Pending);
    }

    [Fact]
    public void Capture_FromVoided_ReturnsTerminalStateError()
    {
        Payment payment = NewPending();
        payment.Authorize("pi_fake_001", Now);
        payment.Void("merchant timeout", Now);

        Result result = payment.Capture(Later);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(PaymentErrors.TerminalState);
    }

    // -------- Void --------

    [Fact]
    public void Void_FromAuthorized_TransitionsToVoided_SetsReason()
    {
        Payment payment = NewPending();
        payment.Authorize("pi_fake_001", Now);

        Result result = payment.Void("merchant rejected", Later);

        result.IsSuccess.Should().BeTrue();
        payment.Status.Should().Be(PaymentStatus.Voided);
        payment.Reason.Should().Be("merchant rejected");
        payment.UpdatedAtUtc.Should().Be(Later);
    }

    [Fact]
    public void Void_FromCaptured_EscalatesToNeedsReview_NotVoided()
    {
        // Caso critico: el dinero YA salio de la tarjeta del cliente; no se puede "void". La saga pidio
        // compensacion (Cancelled/Failed) pero Payments NO auto-refunda — escala a NeedsReview para que
        // un humano decida. Consistente con OrderStateMachine.cs StockCommitFails_NeedsReview.
        Payment payment = NewPending();
        payment.Authorize("pi_fake_001", Now);
        payment.Capture(Now);

        Result result = payment.Void("late cancellation post-capture", Later);

        result.IsSuccess.Should().BeTrue();
        payment.Status.Should().Be(PaymentStatus.NeedsReview);
        payment.Reason.Should().Be("late cancellation post-capture");
    }

    [Fact]
    public void Void_FromVoided_IsIdempotentNoOp()
    {
        Payment payment = NewPending();
        payment.Authorize("pi_fake_001", Now);
        payment.Void("reason1", Now);

        Result result = payment.Void("reason2", Later);

        result.IsSuccess.Should().BeTrue();
        payment.Status.Should().Be(PaymentStatus.Voided);
        payment.Reason.Should().Be("reason1"); // razon original preservada
    }

    [Fact]
    public void Void_FromPending_ReturnsNotAuthorized()
    {
        Payment payment = NewPending();

        Result result = payment.Void("reason", Later);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(PaymentErrors.NotAuthorized);
    }

    // -------- Fail --------

    [Fact]
    public void Fail_FromPending_TransitionsToFailed()
    {
        Payment payment = NewPending();

        Result result = payment.Fail("card declined", Later);

        result.IsSuccess.Should().BeTrue();
        payment.Status.Should().Be(PaymentStatus.Failed);
        payment.Reason.Should().Be("card declined");
    }

    [Fact]
    public void Fail_FromAuthorized_TransitionsToFailed()
    {
        Payment payment = NewPending();
        payment.Authorize("pi_fake_001", Now);

        Result result = payment.Fail("gateway error", Later);

        result.IsSuccess.Should().BeTrue();
        payment.Status.Should().Be(PaymentStatus.Failed);
    }

    [Fact]
    public void Fail_FromFailed_IsIdempotentNoOp()
    {
        Payment payment = NewPending();
        payment.Fail("first reason", Now);

        Result result = payment.Fail("second reason", Later);

        result.IsSuccess.Should().BeTrue();
        payment.Status.Should().Be(PaymentStatus.Failed);
        payment.Reason.Should().Be("first reason");
    }

    [Fact]
    public void Fail_FromCaptured_ReturnsTerminalStateError()
    {
        Payment payment = NewPending();
        payment.Authorize("pi_fake_001", Now);
        payment.Capture(Now);

        Result result = payment.Fail("?", Later);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(PaymentErrors.TerminalState);
    }

    // -------- MarkNeedsReview --------

    [Fact]
    public void MarkNeedsReview_FromCaptured_TransitionsToNeedsReview()
    {
        Payment payment = NewPending();
        payment.Authorize("pi_fake_001", Now);
        payment.Capture(Now);

        Result result = payment.MarkNeedsReview("stock commit failed", Later);

        result.IsSuccess.Should().BeTrue();
        payment.Status.Should().Be(PaymentStatus.NeedsReview);
        payment.Reason.Should().Be("stock commit failed");
    }

    [Fact]
    public void MarkNeedsReview_FromNeedsReview_IsIdempotentNoOp()
    {
        Payment payment = NewPending();
        payment.Authorize("pi_fake_001", Now);
        payment.Capture(Now);
        payment.MarkNeedsReview("reason1", Now);

        Result result = payment.MarkNeedsReview("reason2", Later);

        result.IsSuccess.Should().BeTrue();
        payment.Status.Should().Be(PaymentStatus.NeedsReview);
        payment.Reason.Should().Be("reason1");
    }

    [Fact]
    public void MarkNeedsReview_FromAuthorized_ReturnsNotCaptured()
    {
        Payment payment = NewPending();
        payment.Authorize("pi_fake_001", Now);

        Result result = payment.MarkNeedsReview("?", Later);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(PaymentErrors.NotCaptured);
        payment.Status.Should().Be(PaymentStatus.Authorized);
    }

    // -------- MarkRefunded --------

    [Fact]
    public void MarkRefunded_FromCaptured_TransitionsToRefunded_SetsRefundId()
    {
        Payment payment = NewPending();
        payment.Authorize("pi_fake_001", Now);
        payment.Capture(Now);

        Result result = payment.MarkRefunded("re_fake_001", Later);

        result.IsSuccess.Should().BeTrue();
        payment.Status.Should().Be(PaymentStatus.Refunded);
        payment.ProviderRefundId.Should().Be("re_fake_001");
    }

    [Fact]
    public void MarkRefunded_FromNeedsReview_TransitionsToRefunded()
    {
        // Humano marca el NeedsReview como refundado manualmente.
        Payment payment = NewPending();
        payment.Authorize("pi_fake_001", Now);
        payment.Capture(Now);
        payment.MarkNeedsReview("reason", Now);

        Result result = payment.MarkRefunded("re_fake_002", Later);

        result.IsSuccess.Should().BeTrue();
        payment.Status.Should().Be(PaymentStatus.Refunded);
        payment.ProviderRefundId.Should().Be("re_fake_002");
    }

    [Fact]
    public void MarkRefunded_FromRefunded_SameId_IsIdempotentNoOp()
    {
        Payment payment = NewPending();
        payment.Authorize("pi_fake_001", Now);
        payment.Capture(Now);
        payment.MarkRefunded("re_fake_001", Now);

        Result result = payment.MarkRefunded("re_fake_001", Later);

        result.IsSuccess.Should().BeTrue();
        payment.Status.Should().Be(PaymentStatus.Refunded);
    }

    [Fact]
    public void MarkRefunded_FromRefunded_DifferentId_ReturnsConflict()
    {
        // Critico: nunca aceptar un segundo refund con otro id (defensa contra doble-refund).
        Payment payment = NewPending();
        payment.Authorize("pi_fake_001", Now);
        payment.Capture(Now);
        payment.MarkRefunded("re_fake_001", Now);

        Result result = payment.MarkRefunded("re_fake_002", Later);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(PaymentErrors.AlreadyRefundedWithDifferentId);
        payment.ProviderRefundId.Should().Be("re_fake_001");
    }

    [Fact]
    public void MarkRefunded_FromPending_ReturnsNotCaptured()
    {
        Payment payment = NewPending();

        Result result = payment.MarkRefunded("re_fake_001", Later);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(PaymentErrors.NotCaptured);
    }

    [Fact]
    public void MarkRefunded_FromAuthorized_ReturnsNotCaptured()
    {
        Payment payment = NewPending();
        payment.Authorize("pi_fake_001", Now);

        Result result = payment.MarkRefunded("re_fake_001", Later);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(PaymentErrors.NotCaptured);
    }
}
