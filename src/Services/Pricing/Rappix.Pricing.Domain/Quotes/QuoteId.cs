namespace Rappix.Pricing.Domain.Quotes;

/// <summary>Identificador fuertemente tipado de una cotizacion (UUIDv7).</summary>
public readonly record struct QuoteId(Guid Value)
{
    /// <summary>Genera un nuevo identificador basado en UUIDv7.</summary>
    public static QuoteId New() => new(Guid.CreateVersion7());

    /// <inheritdoc />
    public override string ToString() => Value.ToString();
}
