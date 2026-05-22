namespace Rappix.Catalog.Domain.Catalogs;

/// <summary>Identificador fuertemente tipado de un catalogo (UUIDv7).</summary>
public readonly record struct CatalogId(Guid Value)
{
    /// <summary>Genera un nuevo identificador basado en UUIDv7.</summary>
    public static CatalogId New() => new(Guid.CreateVersion7());

    /// <inheritdoc />
    public override string ToString() => Value.ToString();
}
