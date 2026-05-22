using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Rappix.Pricing.Application.Abstractions;
using Rappix.Pricing.Application.Configuration;
using Rappix.Pricing.Application.Pricing.Surge;
using Rappix.Pricing.Domain.Common;
using Rappix.Pricing.Domain.Surge;

namespace Rappix.Pricing.Tests.Unit;

/// <summary>Pruebas de la estrategia de surge (matching, factor de demanda, cap, conversion a hora local).</summary>
public sealed class ConfigurableSurgeStrategyTests
{
    private static readonly DateTime Now = new(2026, 5, 22, 16, 30, 0, DateTimeKind.Utc); // 12:30 local (offset -4)
    private const decimal Cap = 3.0m;

    [Fact]
    public async Task ResolveMultiplier_NoRules_ReturnsOne()
    {
        var strategy = Strategy(rules: []);

        decimal result = await strategy.ResolveMultiplierAsync(new SurgeContext(null, VerticalType.Food, Now), default);

        result.Should().Be(1.0m);
    }

    [Fact]
    public async Task ResolveMultiplier_MatchingRule_ReturnsItsMultiplier()
    {
        var strategy = Strategy(rules: [Rule(mult: 1.5m)]);

        decimal result = await strategy.ResolveMultiplierAsync(new SurgeContext(null, VerticalType.Food, Now), default);

        result.Should().Be(1.5m);
    }

    [Fact]
    public async Task ResolveMultiplier_AppliesDemandFactor()
    {
        var strategy = Strategy(rules: [Rule(mult: 1.5m)], demandFactor: 1.5m);

        decimal result = await strategy.ResolveMultiplierAsync(new SurgeContext(null, VerticalType.Food, Now), default);

        result.Should().Be(2.25m); // 1.5 * 1.5
    }

    [Fact]
    public async Task ResolveMultiplier_NeverExceedsCap()
    {
        var strategy = Strategy(rules: [Rule(mult: 2.5m)], demandFactor: 2.0m);

        decimal result = await strategy.ResolveMultiplierAsync(new SurgeContext(null, VerticalType.Food, Now), default);

        result.Should().Be(Cap); // 2.5 * 2.0 = 5.0 -> capado a 3.0
    }

    [Fact]
    public async Task ResolveMultiplier_HigherPriorityWins()
    {
        var strategy = Strategy(rules: [Rule(mult: 1.2m, priority: 1), Rule(mult: 1.4m, priority: 5)]);

        decimal result = await strategy.ResolveMultiplierAsync(new SurgeContext(null, VerticalType.Food, Now), default);

        result.Should().Be(1.4m);
    }

    [Fact]
    public async Task ResolveMultiplier_EvaluatesWindowInLocalTime()
    {
        // Regla 12-14 local. UtcNow 12:30 UTC -> 08:30 local (offset -4) -> fuera de la ventana -> sin surge.
        var utcMorning = new DateTime(2026, 5, 22, 12, 30, 0, DateTimeKind.Utc);
        var strategy = Strategy(rules: [Rule(mult: 1.5m)]);

        decimal result = await strategy.ResolveMultiplierAsync(new SurgeContext(null, VerticalType.Food, utcMorning), default);

        result.Should().Be(1.0m);
    }

    private static ConfigurableSurgeStrategy Strategy(SurgeRule[] rules, decimal demandFactor = 1.0m)
    {
        ISurgeRuleRepository repository = Substitute.For<ISurgeRuleRepository>();
        repository.GetActiveAsync(Arg.Any<CancellationToken>()).Returns(rules);

        var options = Options.Create(new PricingOptions
        {
            MaxSurgeMultiplier = Cap,
            DemandFactor = demandFactor,
            SurgeTimeZoneOffsetHours = -4,
        });

        return new ConfigurableSurgeStrategy(repository, options);
    }

    private static SurgeRule Rule(decimal mult, int priority = 0) =>
        SurgeRule.Create(null, null, 12, 14, mult, priority, Cap, Now).Value;
}
