namespace Rappix.Catalog.Application.Responses;

/// <summary>
/// Respuesta de pricing/validacion de un item para otros servicios (via gRPC GetItemPricing).
/// IsPurchasable combina merchant activo + item disponible + stock (si lleva inventario).
/// </summary>
public sealed record ItemPricingResponse(
    Guid ItemId,
    Guid MerchantId,
    string Name,
    decimal PriceAmount,
    string Currency,
    bool IsAvailable,
    bool TracksInventory,
    int? StockQuantity,
    bool IsPurchasable);
