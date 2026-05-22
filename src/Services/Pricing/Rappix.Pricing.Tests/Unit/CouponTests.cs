using FluentAssertions;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.Pricing.Domain.Coupons;
using Rappix.Pricing.Domain.Coupons.Events;
using Rappix.Pricing.Domain.Errors;
using Rappix.Pricing.Domain.Quotes;

namespace Rappix.Pricing.Tests.Unit;

/// <summary>Pruebas del agregado Coupon (creacion, validacion de uso, calculo de descuento, redencion).</summary>
public sealed class CouponTests
{
    private static readonly DateTime Now = new(2026, 5, 22, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Create_PercentageOutOfRange_Fails()
    {
        Coupon.Create("WELCOME", DiscountType.Percentage, 150m, null, null, Now.AddDays(-1), Now.AddDays(30), null, null, Now)
            .IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Create_FixedNonPositive_Fails()
    {
        Coupon.Create("WELCOME", DiscountType.FixedAmount, 0m, null, null, Now.AddDays(-1), Now.AddDays(30), null, null, Now)
            .IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Create_InvalidDateRange_Fails()
    {
        Coupon.Create("WELCOME", DiscountType.Percentage, 10m, null, null, Now.AddDays(30), Now.AddDays(1), null, null, Now)
            .IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Create_Valid_Succeeds()
    {
        Coupon.Create("welcome10", DiscountType.Percentage, 10m, 100, 50m, Now.AddDays(-1), Now.AddDays(30), 2, null, Now)
            .IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void ValidateUsable_Inactive_Fails()
    {
        Coupon coupon = ValidCoupon();
        coupon.Deactivate(Now);

        coupon.ValidateUsable(1000m, Now, 0).Error.Should().Be(CouponErrors.Inactive);
    }

    [Fact]
    public void ValidateUsable_NotYetValid_Fails()
    {
        Coupon coupon = Coupon.Create("FUTURE", DiscountType.Percentage, 10m, null, null, Now.AddDays(5), Now.AddDays(30), null, null, Now).Value;

        coupon.ValidateUsable(1000m, Now, 0).Error.Should().Be(CouponErrors.NotYetValid);
    }

    [Fact]
    public void ValidateUsable_Expired_Fails()
    {
        Coupon coupon = Coupon.Create("OLD", DiscountType.Percentage, 10m, null, null, Now.AddDays(-30), Now.AddDays(-1), null, null, Now).Value;

        coupon.ValidateUsable(1000m, Now, 0).Error.Should().Be(CouponErrors.Expired);
    }

    [Fact]
    public void ValidateUsable_MaxUsesReached_Fails()
    {
        Coupon coupon = Coupon.Create("ONCE", DiscountType.Percentage, 10m, 1, null, Now.AddDays(-1), Now.AddDays(30), null, null, Now).Value;
        coupon.Redeem(QuoteId.New(), Guid.CreateVersion7(), 10m, Now);

        coupon.ValidateUsable(1000m, Now, 0).Error.Should().Be(CouponErrors.MaxUsesReached);
    }

    [Fact]
    public void ValidateUsable_MinOrderNotMet_Fails()
    {
        Coupon coupon = Coupon.Create("MIN500", DiscountType.Percentage, 10m, null, 500m, Now.AddDays(-1), Now.AddDays(30), null, null, Now).Value;

        coupon.ValidateUsable(100m, Now, 0).Error.Should().Be(CouponErrors.MinOrderNotMet);
    }

    [Fact]
    public void ValidateUsable_PerUserLimitReached_Fails()
    {
        Coupon coupon = Coupon.Create("PERUSER", DiscountType.Percentage, 10m, null, null, Now.AddDays(-1), Now.AddDays(30), 2, null, Now).Value;

        coupon.ValidateUsable(1000m, Now, userRedemptionCount: 2).Error.Should().Be(CouponErrors.PerUserLimitReached);
    }

    [Fact]
    public void ValidateUsable_Valid_Succeeds()
    {
        ValidCoupon().ValidateUsable(1000m, Now, 0).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void CalculateDiscount_Percentage_AppliesRate()
    {
        ValidCoupon(DiscountType.Percentage, 10m).CalculateDiscount(200m).Should().Be(20m);
    }

    [Fact]
    public void CalculateDiscount_Fixed_IsCappedAtBase()
    {
        ValidCoupon(DiscountType.FixedAmount, 150m).CalculateDiscount(100m).Should().Be(100m);
    }

    [Fact]
    public void Redeem_IncrementsUsedCount_AndRaisesEvent()
    {
        Coupon coupon = ValidCoupon();

        Result result = coupon.Redeem(QuoteId.New(), Guid.CreateVersion7(), 25m, Now);

        result.IsSuccess.Should().BeTrue();
        coupon.UsedCount.Should().Be(1);
        coupon.DomainEvents.Should().ContainSingle(domainEvent => domainEvent is CouponRedeemedDomainEvent);
    }

    [Fact]
    public void Redeem_AtMaxUses_Fails()
    {
        Coupon coupon = Coupon.Create("ONCE", DiscountType.Percentage, 10m, 1, null, Now.AddDays(-1), Now.AddDays(30), null, null, Now).Value;
        coupon.Redeem(QuoteId.New(), Guid.CreateVersion7(), 10m, Now);

        coupon.Redeem(QuoteId.New(), Guid.CreateVersion7(), 10m, Now).Error.Should().Be(CouponErrors.MaxUsesReached);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AppliesToMerchant_Global_AlwaysTrue(bool sameMerchant)
    {
        Coupon coupon = ValidCoupon(merchantId: null);
        Guid merchant = sameMerchant ? Guid.CreateVersion7() : Guid.CreateVersion7();

        coupon.AppliesToMerchant(merchant).Should().BeTrue();
    }

    [Fact]
    public void AppliesToMerchant_Specific_OnlyMatchingMerchant()
    {
        Guid merchant = Guid.CreateVersion7();
        Coupon coupon = ValidCoupon(merchantId: merchant);

        coupon.AppliesToMerchant(merchant).Should().BeTrue();
        coupon.AppliesToMerchant(Guid.CreateVersion7()).Should().BeFalse();
    }

    private static Coupon ValidCoupon(
        DiscountType type = DiscountType.Percentage,
        decimal value = 10m,
        Guid? merchantId = null) =>
        Coupon.Create("WELCOME10", type, value, maxUses: null, minOrderAmount: null,
            Now.AddDays(-1), Now.AddDays(30), perUserLimit: null, merchantId, Now).Value;
}
