using FluentAssertions;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.Pricing.Domain.Common;
using Rappix.Pricing.Domain.Coupons;
using Rappix.Pricing.Domain.Errors;
using Rappix.Pricing.Domain.Quotes;

namespace Rappix.Pricing.Tests.Unit;

/// <summary>
/// Pruebas de la reversion del consumo de una cotizacion (compensacion de la saga) y de la des-redencion
/// del cupon: Revert solo es valido desde Consumed por el mismo pedido; es idempotente; la auditoria de
/// la redencion se conserva.
/// </summary>
public sealed class QuoteRevertTests
{
    private static readonly DateTime Now = new(2026, 5, 22, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid OrderId = Guid.CreateVersion7();
    private static readonly Guid OtherOrderId = Guid.CreateVersion7();

    [Fact]
    public void Revert_ConsumedBySameOrder_ReturnsToActive()
    {
        Quote quote = NewQuote();
        quote.Consume(Now.AddMinutes(1), OrderId);

        Result result = quote.Revert(OrderId, Now.AddMinutes(2));

        result.IsSuccess.Should().BeTrue();
        quote.Status.Should().Be(QuoteStatus.Active);
        quote.ConsumedAtUtc.Should().BeNull();
        quote.RevertedAtUtc.Should().Be(Now.AddMinutes(2));
    }

    [Fact]
    public void Revert_ConsumedByAnotherOrder_Fails()
    {
        Quote quote = NewQuote();
        quote.Consume(Now.AddMinutes(1), OrderId);

        quote.Revert(OtherOrderId, Now.AddMinutes(2)).Error.Should().Be(QuoteErrors.RevertNotAllowed);
    }

    [Fact]
    public void Revert_NeverConsumed_Fails()
    {
        Quote quote = NewQuote();

        quote.Revert(OrderId, Now.AddMinutes(1)).Error.Should().Be(QuoteErrors.RevertNotAllowed);
    }

    [Fact]
    public void Revert_AgainBySameOrder_IsIdempotent()
    {
        Quote quote = NewQuote();
        quote.Consume(Now.AddMinutes(1), OrderId);
        quote.Revert(OrderId, Now.AddMinutes(2));

        quote.Revert(OrderId, Now.AddMinutes(3)).IsSuccess.Should().BeTrue();
        quote.Status.Should().Be(QuoteStatus.Active);
    }

    [Fact]
    public void Reverted_CanBeConsumedAgain()
    {
        Quote quote = NewQuote();
        quote.Consume(Now.AddMinutes(1), OrderId);
        quote.Revert(OrderId, Now.AddMinutes(2));

        Result reconsume = quote.Consume(Now.AddMinutes(3), OtherOrderId);

        reconsume.IsSuccess.Should().BeTrue();
        quote.Status.Should().Be(QuoteStatus.Consumed);
        quote.ConsumedByOrderId.Should().Be(OtherOrderId);
        quote.RevertedAtUtc.Should().BeNull();
    }

    [Fact]
    public void Coupon_UnRedeem_DecrementsUsedCount_NotBelowZero()
    {
        Coupon coupon = Coupon.Create("WELCOME", DiscountType.Percentage, 10m, null, null,
            Now.AddDays(-1), Now.AddDays(30), null, null, Now).Value;
        coupon.Redeem(QuoteId.New(), Guid.CreateVersion7(), 10m, Now);
        coupon.UsedCount.Should().Be(1);

        coupon.UnRedeem(Now);
        coupon.UsedCount.Should().Be(0);

        coupon.UnRedeem(Now); // no baja de cero
        coupon.UsedCount.Should().Be(0);
    }

    [Fact]
    public void CouponRedemption_MarkReverted_PreservesAudit_AndIsIdempotent()
    {
        CouponRedemption redemption = CouponRedemption.Create(CouponId.New(), Guid.CreateVersion7(), QuoteId.New(), Now);

        redemption.MarkReverted("Saga compensation", Now.AddMinutes(5));

        redemption.IsReverted.Should().BeTrue();
        redemption.RevertedAtUtc.Should().Be(Now.AddMinutes(5));
        redemption.RevertReason.Should().Be("Saga compensation");

        redemption.MarkReverted("otra razon", Now.AddMinutes(10)); // idempotente: no sobreescribe
        redemption.RevertedAtUtc.Should().Be(Now.AddMinutes(5));
    }

    private static QuoteLine Line() =>
        QuoteLine.Create(Guid.CreateVersion7(), "Item", 100m, 0m, 1).Value;

    private static PriceBreakdown Breakdown(decimal total = 100m) =>
        new() { Subtotal = total, SurgeMultiplier = 1m, Total = total };

    private static Quote NewQuote() =>
        Quote.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), VerticalType.Food, "DOP", [Line()], Breakdown(), null, null, Now, 10).Value;
}
