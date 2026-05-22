namespace Rappix.Catalog.Domain.Items;

/// <summary>Identificador fuertemente tipado de un item (UUIDv7).</summary>
public readonly record struct ItemId(Guid Value)
{
    /// <summary>Genera un nuevo identificador basado en UUIDv7.</summary>
    public static ItemId New() => new(Guid.CreateVersion7());

    /// <inheritdoc />
    public override string ToString() => Value.ToString();
}
