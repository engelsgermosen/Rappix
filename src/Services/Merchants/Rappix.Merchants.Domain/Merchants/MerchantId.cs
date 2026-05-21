namespace Rappix.Merchants.Domain.Merchants;

/// <summary>Identificador fuertemente tipado de un merchant (UUIDv7).</summary>
public readonly record struct MerchantId(Guid Value)
{
    /// <summary>Genera un nuevo identificador basado en UUIDv7.</summary>
    public static MerchantId New() => new(Guid.CreateVersion7());

    /// <inheritdoc />
    public override string ToString() => Value.ToString();
}
