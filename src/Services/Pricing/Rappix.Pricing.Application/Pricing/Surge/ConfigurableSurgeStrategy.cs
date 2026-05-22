using Microsoft.Extensions.Options;
using Rappix.Pricing.Application.Abstractions;
using Rappix.Pricing.Application.Configuration;
using Rappix.Pricing.Domain.Surge;

namespace Rappix.Pricing.Application.Pricing.Surge;

/// <summary>
/// Surge dinamico por zona + franja horaria, multiplicado por un factor de demanda configurable y capado
/// por un hard limit. Las reglas se gestionan por admin (tabla, no hardcode). El factor de demanda es hoy
/// un stand-in de la senal real (pedidos vs couriers) que proveera Dispatch en una fase futura (ADR-0005).
/// </summary>
internal sealed class ConfigurableSurgeStrategy(
    ISurgeRuleRepository surgeRules,
    IOptions<PricingOptions> options)
    : ISurgeStrategy
{
    public async Task<decimal> ResolveMultiplierAsync(SurgeContext context, CancellationToken cancellationToken)
    {
        PricingOptions config = options.Value;
        int localHour = ToLocalHour(context.UtcNow, config.SurgeTimeZoneOffsetHours);

        IReadOnlyList<SurgeRule> rules = await surgeRules.GetActiveAsync(cancellationToken);

        SurgeRule? match = rules
            .Where(rule => rule.Matches(context.ZoneId, context.Vertical, localHour))
            .OrderByDescending(rule => rule.Priority)
            .ThenByDescending(rule => rule.Multiplier)
            .FirstOrDefault();

        decimal baseMultiplier = match?.Multiplier ?? 1.0m;
        decimal demandFactor = config.DemandFactor <= 0m ? 1.0m : config.DemandFactor;

        decimal multiplier = baseMultiplier * demandFactor;
        return Math.Clamp(multiplier, 1.0m, config.MaxSurgeMultiplier);
    }

    private static int ToLocalHour(DateTime utcNow, int offsetHours)
    {
        DateTime utc = DateTime.SpecifyKind(utcNow, DateTimeKind.Utc);
        return utc.AddHours(offsetHours).Hour;
    }
}
