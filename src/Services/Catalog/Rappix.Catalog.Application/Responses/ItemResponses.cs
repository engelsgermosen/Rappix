using Rappix.Catalog.Domain.Items;

namespace Rappix.Catalog.Application.Responses;

/// <summary>Una opcion de un grupo de modificadores.</summary>
public sealed record ModifierOptionResponse(Guid Id, string Name, decimal PriceDelta)
{
    /// <summary>Proyecta una opcion de dominio.</summary>
    public static ModifierOptionResponse From(ModifierOption option) => new(option.Id, option.Name, option.PriceDelta);
}

/// <summary>Un grupo de modificadores con sus opciones.</summary>
public sealed record ModifierResponse(
    Guid Id,
    string Name,
    bool IsRequired,
    int MinSelections,
    int MaxSelections,
    IReadOnlyList<ModifierOptionResponse> Options)
{
    /// <summary>Proyecta un grupo de modificadores de dominio.</summary>
    public static ModifierResponse From(Modifier modifier) => new(
        modifier.Id,
        modifier.Name,
        modifier.IsRequired,
        modifier.MinSelections,
        modifier.MaxSelections,
        [.. modifier.Options.Select(ModifierOptionResponse.From)]);
}

/// <summary>Vista completa de un item (owner).</summary>
public sealed record ItemResponse(
    Guid Id,
    Guid MerchantId,
    Guid? CategoryId,
    string Name,
    string? Description,
    decimal PriceAmount,
    string Currency,
    bool IsAvailable,
    bool TracksInventory,
    string? PhotoObjectKey,
    IReadOnlyDictionary<string, string> Attributes,
    IReadOnlyList<ModifierResponse> Modifiers,
    DateTime CreatedAtUtc)
{
    /// <summary>Proyecta el agregado item.</summary>
    public static ItemResponse From(Item item) => new(
        item.Id.Value,
        item.MerchantId,
        item.CategoryId,
        item.Name,
        item.Description,
        item.Price.Amount,
        item.Price.Currency,
        item.IsAvailable,
        item.TracksInventory,
        item.PhotoObjectKey,
        new Dictionary<string, string>(item.Attributes, StringComparer.OrdinalIgnoreCase),
        [.. item.Modifiers.Select(ModifierResponse.From)],
        item.CreatedAtUtc);
}

/// <summary>Nivel de stock de un item.</summary>
public sealed record StockResponse(Guid ItemId, int Quantity, bool IsDepleted)
{
    /// <summary>Proyecta un nivel de stock de dominio.</summary>
    public static StockResponse From(StockLevel stock) => new(stock.Id.Value, stock.Quantity, stock.IsDepleted);
}
