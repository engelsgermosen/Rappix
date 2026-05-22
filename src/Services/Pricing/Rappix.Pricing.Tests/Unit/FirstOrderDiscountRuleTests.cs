using FluentAssertions;
using Microsoft.Extensions.Options;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.Pricing.Application.Configuration;
using Rappix.Pricing.Application.Pricing.Discounts;
using Rappix.Pricing.Domain.Coupons;

namespace Rappix.Pricing.Tests.Unit;

/// <summary>Pruebas de la regla de descuento de primera compra.</summary>
public sealed class FirstOrderDiscountRuleTests
{
    [Fact]
    public async Task Evaluate_FirstOrderEnabled_ReturnsConfiguredDiscount()
    {
        Result<DiscountEvaluation?> result = await Rule().EvaluateAsync(Context(isFirstOrder: true), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Type.Should().Be(DiscountType.Percentage);
        result.Value.Value.Should().Be(15m);
    }

    [Fact]
    public async Task Evaluate_NotFirstOrder_ReturnsNull()
    {
        (await Rule().EvaluateAsync(Context(isFirstOrder: false), default)).Value.Should().BeNull();
    }

    [Fact]
    public async Task Evaluate_Disabled_ReturnsNull()
    {
        (await Rule(enabled: false).EvaluateAsync(Context(isFirstOrder: true), default)).Value.Should().BeNull();
    }

    private static FirstOrderDiscountRule Rule(bool enabled = true, decimal value = 15m)
    {
        var options = Options.Create(new PricingOptions
        {
            FirstOrderDiscount = new FirstOrderDiscountOptions { Enabled = enabled, Type = DiscountType.Percentage, Value = value },
        });
        return new FirstOrderDiscountRule(options);
    }

    private static DiscountRuleContext Context(bool isFirstOrder) =>
        new(Guid.CreateVersion7(), Guid.CreateVersion7(), 100m, isFirstOrder, null, DateTime.UtcNow);
}
