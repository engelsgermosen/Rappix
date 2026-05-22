using FluentAssertions;
using Rappix.Pricing.Domain.Common;

namespace Rappix.Pricing.Tests.Unit;

/// <summary>Pruebas del value object Percentage.</summary>
public sealed class PercentageTests
{
    [Theory]
    [InlineData(-0.01)]
    [InlineData(100.01)]
    public void Create_OutOfRange_Fails(decimal value)
    {
        Percentage.Create(value).IsFailure.Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(18)]
    [InlineData(100)]
    public void Create_InRange_Succeeds(decimal value)
    {
        Percentage.Create(value).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void AsFraction_DividesByHundred()
    {
        Percentage.Create(18m).Value.AsFraction.Should().Be(0.18m);
    }
}
