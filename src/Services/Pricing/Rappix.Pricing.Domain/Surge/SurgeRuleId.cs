namespace Rappix.Pricing.Domain.Surge;

/// <summary>Identificador fuertemente tipado de una regla de surge (UUIDv7).</summary>
public readonly record struct SurgeRuleId(Guid Value)
{
    /// <summary>Genera un nuevo identificador basado en UUIDv7.</summary>
    public static SurgeRuleId New() => new(Guid.CreateVersion7());

    /// <inheritdoc />
    public override string ToString() => Value.ToString();
}
