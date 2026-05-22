using Rappix.Pricing.Domain.Surge;

namespace Rappix.Pricing.Application.Responses;

/// <summary>Respuesta de una regla de surge (vista de administracion).</summary>
public sealed record SurgeRuleResponse(
    Guid SurgeRuleId,
    string? ZoneId,
    string? Vertical,
    int StartHour,
    int EndHour,
    decimal Multiplier,
    int Priority,
    bool IsActive,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc)
{
    /// <summary>Proyecta el agregado regla de surge a su DTO.</summary>
    public static SurgeRuleResponse From(SurgeRule rule) =>
        new(
            rule.Id.Value,
            rule.ZoneId,
            rule.Vertical?.ToString(),
            rule.StartHour,
            rule.EndHour,
            rule.Multiplier,
            rule.Priority,
            rule.IsActive,
            rule.CreatedAtUtc,
            rule.UpdatedAtUtc);
}
