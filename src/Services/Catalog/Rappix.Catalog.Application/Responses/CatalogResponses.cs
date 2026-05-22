using Rappix.Catalog.Domain.Catalogs;

namespace Rappix.Catalog.Application.Responses;

/// <summary>Una categoria del catalogo.</summary>
public sealed record CategoryResponse(Guid Id, string Name, int SortOrder)
{
    /// <summary>Proyecta una categoria de dominio.</summary>
    public static CategoryResponse From(Category category) => new(category.Id, category.Name, category.SortOrder);
}

/// <summary>Vista del catalogo de un merchant (owner).</summary>
public sealed record CatalogResponse(
    Guid Id,
    Guid MerchantId,
    string VerticalType,
    bool IsEnabled,
    IReadOnlyList<CategoryResponse> Categories)
{
    /// <summary>Proyecta el agregado catalogo (categorias ordenadas por SortOrder).</summary>
    public static CatalogResponse From(MerchantCatalog catalog) => new(
        catalog.Id.Value,
        catalog.MerchantId,
        catalog.VerticalType.ToString(),
        catalog.IsEnabled,
        [.. catalog.Categories
            .OrderBy(category => category.SortOrder)
            .ThenBy(category => category.Name, StringComparer.OrdinalIgnoreCase)
            .Select(CategoryResponse.From)]);
}
