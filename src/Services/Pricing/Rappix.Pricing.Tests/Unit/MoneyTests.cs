using FluentAssertions;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.Pricing.Domain.Common;

namespace Rappix.Pricing.Tests.Unit;

/// <summary>Pruebas del value object Money (validacion, aritmetica segura en moneda, redondeo bancario).</summary>
public sealed class MoneyTests
{
    [Fact]
    public void Create_NegativeAmount_Fails()
    {
        Result<Money> result = Money.Create(-1m);

        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
    }

    [Theory]
    [InlineData("US")]
    [InlineData("DOLLAR")]
    [InlineData("D1P")]
    public void Create_InvalidCurrency_Fails(string currency)
    {
        Money.Create(10m, currency).IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Create_NullCurrency_DefaultsToDop()
    {
        Money.Create(10m, null).Value.Currency.Should().Be("DOP");
    }

    [Fact]
    public void Create_LowercaseCurrency_IsNormalizedToUpper()
    {
        Money.Create(10m, "usd").Value.Currency.Should().Be("USD");
    }

    [Fact]
    public void Add_SameCurrency_SumsAmounts()
    {
        Money sum = Money.Create(10m).Value.Add(Money.Create(5m).Value);

        sum.Amount.Should().Be(15m);
    }

    [Fact]
    public void Subtract_SameCurrency_SubtractsAmounts()
    {
        Money.Create(10m).Value.Subtract(Money.Create(4m).Value).Amount.Should().Be(6m);
    }

    [Fact]
    public void Multiply_ScalesAmount()
    {
        Money.Create(10m).Value.Multiply(2.5m).Amount.Should().Be(25m);
    }

    [Fact]
    public void Percent_AppliesPercentage()
    {
        Money.Create(200m).Value.Percent(18m).Amount.Should().Be(36m);
    }

    [Fact]
    public void Add_DifferentCurrencies_Throws()
    {
        Money usd = Money.Create(10m, "USD").Value;
        Money dop = Money.Create(5m, "DOP").Value;

        Action act = () => usd.Add(dop);

        act.Should().Throw<InvalidOperationException>();
    }

    [Theory]
    [InlineData(0.125, 0.12)]
    [InlineData(0.135, 0.14)]
    public void RoundToMinorUnit_UsesBankersRounding(decimal amount, decimal expected)
    {
        Money.FromTrusted(amount, "DOP").RoundToMinorUnit().Amount.Should().Be(expected);
    }

    [Fact]
    public void Zero_HasZeroAmount()
    {
        Money.Zero().Amount.Should().Be(0m);
    }
}
