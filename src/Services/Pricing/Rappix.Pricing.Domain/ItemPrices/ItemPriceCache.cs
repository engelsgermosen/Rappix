namespace Rappix.Pricing.Domain.ItemPrices;

/// <summary>
/// Cache local del precio base de un item, alimentada por el consumer de ItemCreated de Catalog. Permite
/// cotizar rapido y sirve de fallback cuando el gRPC a Catalog no responde. La fuente de verdad del precio
/// (y la comprabilidad/stock) sigue siendo el gRPC a Catalog; este cache es una optimizacion. PK = ItemId.
/// </summary>
public sealed class ItemPriceCache
{
    private ItemPriceCache()
    {
    }

    private ItemPriceCache(Guid itemId, Guid merchantId, decimal basePrice, string currency, DateTime updatedAtUtc)
    {
        ItemId = itemId;
        MerchantId = merchantId;
        BasePrice = basePrice;
        Currency = currency;
        UpdatedAtUtc = updatedAtUtc;
    }

    /// <summary>Item cacheado (clave primaria).</summary>
    public Guid ItemId { get; private set; }

    /// <summary>Merchant dueno del item.</summary>
    public Guid MerchantId { get; private set; }

    /// <summary>Precio base del item.</summary>
    public decimal BasePrice { get; private set; }

    /// <summary>Moneda ISO 4217 del precio.</summary>
    public string Currency { get; private set; } = null!;

    /// <summary>Momento de la ultima actualizacion del cache (UTC).</summary>
    public DateTime UpdatedAtUtc { get; private set; }

    /// <summary>Crea una entrada de cache.</summary>
    public static ItemPriceCache Create(Guid itemId, Guid merchantId, decimal basePrice, string currency, DateTime updatedAtUtc) =>
        new(itemId, merchantId, basePrice, currency, updatedAtUtc);

    /// <summary>Actualiza el precio cacheado (idempotente ante reentregas del evento).</summary>
    public void Update(Guid merchantId, decimal basePrice, string currency, DateTime updatedAtUtc)
    {
        MerchantId = merchantId;
        BasePrice = basePrice;
        Currency = currency;
        UpdatedAtUtc = updatedAtUtc;
    }
}
