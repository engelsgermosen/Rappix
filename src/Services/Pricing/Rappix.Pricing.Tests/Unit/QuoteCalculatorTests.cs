using FluentAssertions;
using Rappix.Pricing.Application.Pricing.Engine;
using Rappix.Pricing.Domain.Coupons;
using Rappix.Pricing.Domain.Quotes;

namespace Rappix.Pricing.Tests.Unit;

/// <summary>
/// Pruebas exhaustivas del corazon del servicio: el motor de calculo. Cada escenario fija las entradas y
/// compara contra un desglose calculado a mano, cubriendo el orden de operaciones, cada componente, el
/// tope de descuento, la base gravable, la propina no gravable, el cap implicito y la politica de redondeo.
/// </summary>
public sealed class QuoteCalculatorTests
{
    [Fact]
    public void Calculate_FullScenario_MatchesHandComputedBreakdown()
    {
        // Subtotal = 100*2 + (50+10)*1 = 260; surge x1.5 -> +130; 10% desc sobre 390 -> 39;
        // service 5% sobre 260 -> 13; delivery 100; tax 18% sobre (351+100+13)=464 -> 83.52; tip 25.
        QuoteCalculationInput input = Input(
            [Line(100m, 0m, 2), Line(50m, 10m, 1)],
            surge: 1.5m,
            discounts: [new DiscountDirective("Coupon", DiscountType.Percentage, 10m)],
            deliveryFee: 100m,
            serviceFeePct: 5m,
            taxPct: 18m,
            tip: 25m);

        PriceBreakdown result = QuoteCalculator.Calculate(input);

        result.Subtotal.Should().Be(260m);
        result.SurgeMultiplier.Should().Be(1.5m);
        result.SurgeAmount.Should().Be(130m);
        result.DiscountAmount.Should().Be(39m);
        result.DeliveryFee.Should().Be(100m);
        result.ServiceFee.Should().Be(13m);
        result.Tax.Should().Be(83.52m);
        result.Tip.Should().Be(25m);
        result.Total.Should().Be(572.52m);
    }

    [Fact]
    public void Calculate_TotalIsAlwaysTheSumOfRoundedComponents()
    {
        QuoteCalculationInput input = Input(
            [Line(133.33m, 0m, 3)],
            surge: 1.3m,
            discounts: [new DiscountDirective("First", DiscountType.Percentage, 15m)],
            deliveryFee: 75m,
            serviceFeePct: 5m,
            taxPct: 18m,
            tip: 12.5m);

        PriceBreakdown result = QuoteCalculator.Calculate(input);

        decimal expectedTotal = result.Subtotal + result.SurgeAmount - result.DiscountAmount
            + result.DeliveryFee + result.ServiceFee + result.Tax + result.Tip;
        result.Total.Should().Be(expectedTotal);
    }

    [Fact]
    public void Calculate_NoSurge_ProducesZeroSurgeAmount()
    {
        PriceBreakdown result = QuoteCalculator.Calculate(Input([Line(100m, 0m, 1)], surge: 1.0m));

        result.Subtotal.Should().Be(100m);
        result.SurgeAmount.Should().Be(0m);
        result.Total.Should().Be(100m);
    }

    [Fact]
    public void Calculate_SurgeAddsMultiplierMinusOneTimesSubtotal()
    {
        PriceBreakdown result = QuoteCalculator.Calculate(Input([Line(200m, 0m, 1)], surge: 1.4m));

        result.SurgeAmount.Should().Be(80m); // 200 * 0.4
        result.Total.Should().Be(280m);
    }

    [Fact]
    public void Calculate_FixedDiscount_IsCappedAtSurgedSubtotal()
    {
        // Subtotal 100, sin surge; cupon fijo de 150 -> descuento topado a 100 (bienes netos = 0).
        PriceBreakdown result = QuoteCalculator.Calculate(Input(
            [Line(100m, 0m, 1)],
            discounts: [new DiscountDirective("Coupon", DiscountType.FixedAmount, 150m)]));

        result.DiscountAmount.Should().Be(100m);
        result.Total.Should().Be(0m);
    }

    [Fact]
    public void Calculate_StackedDiscounts_SumAndAreCapped()
    {
        // 60% + 60% = 120% sobre 100 -> topado a 100.
        PriceBreakdown result = QuoteCalculator.Calculate(Input(
            [Line(100m, 0m, 1)],
            discounts:
            [
                new DiscountDirective("First", DiscountType.Percentage, 60m),
                new DiscountDirective("Coupon", DiscountType.Percentage, 60m),
            ]));

        result.DiscountAmount.Should().Be(100m);
        result.Total.Should().Be(0m);
    }

    [Fact]
    public void Calculate_ServiceFee_IsPercentageOfBaseSubtotal()
    {
        // Service fee se calcula sobre el subtotal base (260), no sobre el subtotal con surge.
        PriceBreakdown result = QuoteCalculator.Calculate(Input(
            [Line(260m, 0m, 1)], surge: 2.0m, serviceFeePct: 5m));

        result.ServiceFee.Should().Be(13m); // 260 * 0.05, no 520 * 0.05
    }

    [Fact]
    public void Calculate_TipIsNeverTaxed()
    {
        // tax 18% solo sobre el subtotal (100); delivery/service no gravables; propina 50 fuera del impuesto.
        PriceBreakdown result = QuoteCalculator.Calculate(Input(
            [Line(100m, 0m, 1)],
            taxPct: 18m,
            taxDelivery: false,
            taxService: false,
            tip: 50m));

        result.Tax.Should().Be(18m); // 100 * 0.18, no (100+50) * 0.18
        result.Tip.Should().Be(50m);
        result.Total.Should().Be(168m);
    }

    [Fact]
    public void Calculate_TaxableComponents_RespectFlags()
    {
        // goodsNet 100 + delivery 50 + service 10; tax 10% solo sobre bienes + delivery (service no gravable).
        QuoteCalculationInput input = Input(
            [Line(100m, 0m, 1)],
            deliveryFee: 50m,
            serviceFeePct: 10m,
            taxPct: 10m,
            taxDelivery: true,
            taxService: false);

        PriceBreakdown result = QuoteCalculator.Calculate(input);

        result.ServiceFee.Should().Be(10m);
        result.Tax.Should().Be(15m); // (100 + 50) * 0.10
    }

    [Theory]
    [InlineData(0.125, 0.12)] // ToEven: 0.12 (par) en lugar de 0.13
    [InlineData(0.135, 0.14)] // ToEven: 0.14 (par) en lugar de 0.13
    [InlineData(0.145, 0.14)] // ToEven: 0.14 (par) en lugar de 0.15
    public void Calculate_RoundsSubtotalWithBankersRounding(decimal unitPrice, decimal expectedSubtotal)
    {
        // Aisla el redondeo del subtotal: sin surge, fees, impuesto ni propina.
        PriceBreakdown result = QuoteCalculator.Calculate(Input([Line(unitPrice, 0m, 1)]));

        result.Subtotal.Should().Be(expectedSubtotal);
        result.Total.Should().Be(expectedSubtotal);
    }

    [Fact]
    public void Calculate_SumsLineModifiersAndQuantities()
    {
        // (precio 80 + modificadores 20) x 3 = 300, mas (precio 10) x 5 = 50 -> subtotal 350.
        PriceBreakdown result = QuoteCalculator.Calculate(Input([Line(80m, 20m, 3), Line(10m, 0m, 5)]));

        result.Subtotal.Should().Be(350m);
    }

    [Fact]
    public void Calculate_TipOnlyOrder_TotalIsJustTip()
    {
        PriceBreakdown result = QuoteCalculator.Calculate(Input([Line(0m, 0m, 1)], tip: 30m));

        result.Subtotal.Should().Be(0m);
        result.Total.Should().Be(30m);
    }

    private static QuoteLine Line(decimal unitPrice, decimal modifierTotal, int quantity) =>
        QuoteLine.Create(Guid.CreateVersion7(), "Item", unitPrice, modifierTotal, quantity).Value;

    private static QuoteCalculationInput Input(
        IReadOnlyList<QuoteLine> lines,
        decimal surge = 1.0m,
        IReadOnlyList<DiscountDirective>? discounts = null,
        decimal deliveryFee = 0m,
        decimal serviceFeePct = 0m,
        decimal taxPct = 0m,
        bool taxDelivery = true,
        bool taxService = true,
        decimal tip = 0m) =>
        new()
        {
            Lines = lines,
            SurgeMultiplier = surge,
            Discounts = discounts ?? [],
            DeliveryFee = deliveryFee,
            ServiceFeePercentage = serviceFeePct,
            TaxPercentage = taxPct,
            TaxAppliesToDelivery = taxDelivery,
            TaxAppliesToServiceFee = taxService,
            Tip = tip,
        };
}
