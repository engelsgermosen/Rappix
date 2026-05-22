using Rappix.BuildingBlocks.Core.Domain;

namespace Rappix.Catalog.Domain.Catalogs;

/// <summary>Categoria dentro de un catalogo (entidad hija del agregado <see cref="MerchantCatalog"/>).</summary>
public sealed class Category : Entity<Guid>
{
    private Category()
    {
    }

    internal Category(Guid id, CatalogId catalogId, string name, int sortOrder)
        : base(id)
    {
        CatalogId = catalogId;
        Name = name;
        SortOrder = sortOrder;
    }

    /// <summary>Catalogo al que pertenece.</summary>
    public CatalogId CatalogId { get; private set; }

    /// <summary>Nombre de la categoria.</summary>
    public string Name { get; private set; } = null!;

    /// <summary>Orden de presentacion (menor primero).</summary>
    public int SortOrder { get; private set; }

    internal void Rename(string name) => Name = name;

    internal void Reorder(int sortOrder) => SortOrder = sortOrder;
}
