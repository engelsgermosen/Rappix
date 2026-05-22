using FluentAssertions;
using Rappix.Pricing.Domain.Coupons;

namespace Rappix.Pricing.Tests.Unit;

/// <summary>Pruebas del value object CouponCode (normalizacion y largo).</summary>
public sealed class CouponCodeTests
{
    [Fact]
    public void Create_NormalizesToUpperAndTrims()
    {
        CouponCode.Create("  welcome10 ").Value.Value.Should().Be("WELCOME10");
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("ab")]
    public void Create_EmptyOrTooShort_Fails(string code)
    {
        CouponCode.Create(code).IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Create_TooLong_Fails()
    {
        CouponCode.Create(new string('A', CouponCode.MaxLength + 1)).IsFailure.Should().BeTrue();
    }
}
