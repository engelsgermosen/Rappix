using FluentAssertions;
using NSubstitute;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.Pricing.Application.Abstractions;
using Rappix.Pricing.Application.Pricing.Discounts;
using Rappix.Pricing.Domain.Coupons;
using Rappix.Pricing.Domain.Errors;

namespace Rappix.Pricing.Tests.Unit;

/// <summary>Pruebas de la regla de descuento por cupon (validacion al cotizar, errores tipados).</summary>
public sealed class CouponDiscountRuleTests
{
    private static readonly DateTime Now = new(2026, 5, 22, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Merchant = Guid.CreateVersion7();

    [Fact]
    public async Task Evaluate_NoCode_ReturnsNull()
    {
        ICouponRepository repository = Substitute.For<ICouponRepository>();

        Result<DiscountEvaluation?> result = await new CouponDiscountRule(repository).EvaluateAsync(Context(couponCode: null), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeNull();
    }

    [Fact]
    public async Task Evaluate_CouponNotFound_FailsNotFound()
    {
        ICouponRepository repository = Substitute.For<ICouponRepository>();
        repository.GetActiveByCodeAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((Coupon?)null);

        Result<DiscountEvaluation?> result = await new CouponDiscountRule(repository).EvaluateAsync(Context("MISSING"), default);

        result.Error.Should().Be(CouponErrors.NotFound);
    }

    [Fact]
    public async Task Evaluate_MerchantMismatch_Fails()
    {
        Coupon coupon = Coupon.Create("OTHER", DiscountType.Percentage, 10m, null, null, Now.AddDays(-1), Now.AddDays(30), null, Guid.CreateVersion7(), Now).Value;
        ICouponRepository repository = RepositoryWith(coupon);

        Result<DiscountEvaluation?> result = await new CouponDiscountRule(repository).EvaluateAsync(Context("OTHER"), default);

        result.Error.Should().Be(CouponErrors.MerchantMismatch);
    }

    [Fact]
    public async Task Evaluate_Expired_Fails()
    {
        Coupon coupon = Coupon.Create("OLD", DiscountType.Percentage, 10m, null, null, Now.AddDays(-30), Now.AddDays(-1), null, null, Now).Value;
        ICouponRepository repository = RepositoryWith(coupon);

        Result<DiscountEvaluation?> result = await new CouponDiscountRule(repository).EvaluateAsync(Context("OLD"), default);

        result.Error.Should().Be(CouponErrors.Expired);
    }

    [Fact]
    public async Task Evaluate_PerUserLimitReached_Fails()
    {
        Coupon coupon = Coupon.Create("PERUSER", DiscountType.Percentage, 10m, null, null, Now.AddDays(-1), Now.AddDays(30), 1, null, Now).Value;
        ICouponRepository repository = RepositoryWith(coupon, userRedemptions: 1);

        Result<DiscountEvaluation?> result = await new CouponDiscountRule(repository).EvaluateAsync(Context("PERUSER"), default);

        result.Error.Should().Be(CouponErrors.PerUserLimitReached);
    }

    [Fact]
    public async Task Evaluate_ValidCoupon_ReturnsEvaluationWithCouponId()
    {
        Coupon coupon = Coupon.Create("WELCOME10", DiscountType.Percentage, 10m, null, null, Now.AddDays(-1), Now.AddDays(30), null, null, Now).Value;
        ICouponRepository repository = RepositoryWith(coupon);

        Result<DiscountEvaluation?> result = await new CouponDiscountRule(repository).EvaluateAsync(Context("WELCOME10"), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.CouponId.Should().Be(coupon.Id);
        result.Value.Type.Should().Be(DiscountType.Percentage);
        result.Value.Value.Should().Be(10m);
    }

    private static ICouponRepository RepositoryWith(Coupon coupon, int userRedemptions = 0)
    {
        ICouponRepository repository = Substitute.For<ICouponRepository>();
        repository.GetActiveByCodeAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(coupon);
        repository.CountRedemptionsAsync(Arg.Any<CouponId>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(userRedemptions);
        return repository;
    }

    private static DiscountRuleContext Context(string? couponCode) =>
        new(Guid.CreateVersion7(), Merchant, 1000m, IsFirstOrder: false, couponCode, Now);
}
