using FluentAssertions;
using Rappix.Pricing.Domain.Common;
using Rappix.Pricing.Domain.Surge;

namespace Rappix.Pricing.Tests.Unit;

/// <summary>Pruebas del agregado SurgeRule (validacion y matching por zona, vertical y franja horaria).</summary>
public sealed class SurgeRuleTests
{
    private static readonly DateTime Now = new(2026, 5, 22, 12, 0, 0, DateTimeKind.Utc);
    private const decimal Cap = 3.0m;

    [Theory]
    [InlineData(14, 12)] // inicio >= fin
    [InlineData(0, 25)]  // fin > 24
    public void Create_InvalidHourRange_Fails(int start, int end)
    {
        SurgeRule.Create(null, null, start, end, 1.3m, 0, Cap, Now).IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Create_MultiplierBelowOne_Fails()
    {
        SurgeRule.Create(null, null, 12, 14, 0.5m, 0, Cap, Now).IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Create_MultiplierAboveCap_Fails()
    {
        SurgeRule.Create(null, null, 12, 14, 4.0m, 0, Cap, Now).IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Create_Valid_Succeeds()
    {
        SurgeRule.Create("Z1", VerticalType.Food, 12, 14, 1.3m, 1, Cap, Now).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Matches_GlobalZone_MatchesAnyZone()
    {
        SurgeRule rule = Rule(zone: null, vertical: null);

        rule.Matches("anything", VerticalType.Food, 13).Should().BeTrue();
    }

    [Fact]
    public void Matches_SpecificZone_OnlyThatZone()
    {
        SurgeRule rule = Rule(zone: "Z1", vertical: null);

        rule.Matches("Z1", VerticalType.Food, 13).Should().BeTrue();
        rule.Matches("Z2", VerticalType.Food, 13).Should().BeFalse();
    }

    [Fact]
    public void Matches_SpecificVertical_OnlyThatVertical()
    {
        SurgeRule rule = Rule(zone: null, vertical: VerticalType.Food);

        rule.Matches(null, VerticalType.Food, 13).Should().BeTrue();
        rule.Matches(null, VerticalType.Pharmacy, 13).Should().BeFalse();
    }

    [Theory]
    [InlineData(11, false)] // antes de la ventana
    [InlineData(12, true)]  // inicio inclusivo
    [InlineData(13, true)]
    [InlineData(14, false)] // fin exclusivo
    public void Matches_HourWindow_IsStartInclusiveEndExclusive(int hour, bool expected)
    {
        Rule(zone: null, vertical: null).Matches(null, VerticalType.Food, hour).Should().Be(expected);
    }

    [Fact]
    public void Deactivate_SetsInactiveAndDeleted()
    {
        SurgeRule rule = Rule();
        rule.Deactivate(Now);

        rule.IsActive.Should().BeFalse();
        rule.IsDeleted.Should().BeTrue();
    }

    private static SurgeRule Rule(string? zone = null, VerticalType? vertical = null, int start = 12, int end = 14, decimal mult = 1.3m) =>
        SurgeRule.Create(zone, vertical, start, end, mult, 0, Cap, Now).Value;
}
