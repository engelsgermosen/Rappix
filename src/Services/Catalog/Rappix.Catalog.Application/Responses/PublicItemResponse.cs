using Rappix.Catalog.Domain.Items;

namespace Rappix.Catalog.Application.Responses;

/// <summary>Vista publica de un item (sin banderas internas; solo lo visible al cliente).</summary>
public sealed record PublicItemResponse(
    Guid Id,
    Guid MerchantId,
    Guid? CategoryId,
    string Name,
    string? Description,
    decimal PriceAmount,
    string Currency,
    string? PhotoObjectKey,
    IReadOnlyDictionary<string, string> Attributes,
    IReadOnlyList<ModifierResponse> Modifiers)
{
    /// <summary>Proyecta el agregado item a su vista publica.</summary>
    public static PublicItemResponse From(Item item) => new(
        item.Id.Value,
        item.MerchantId,
        item.CategoryId,
        item.Name,
        item.Description,
        item.Price.Amount,
        item.Price.Currency,
        item.PhotoObjectKey,
        new Dictionary<string, string>(item.Attributes, StringComparer.OrdinalIgnoreCase),
        [.. item.Modifiers.Select(ModifierResponse.From)]);
}
