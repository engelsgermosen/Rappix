namespace Rappix.Pricing.Application.Abstractions;

/// <summary>
/// Puerto hacia el servicio Catalog (gRPC). La implementacion (Infraestructura) envuelve el cliente gRPC
/// con reintentos y circuit breaker; si el servicio no responde, <see cref="CatalogItemPricing.ServiceAvailable"/>
/// es false y el llamador hace fallback al cache local de precios (ItemPriceCache).
/// </summary>
public interface ICatalogPricingClient
{
    /// <summary>Consulta precio y comprabilidad de un item en Catalog.</summary>
    Task<CatalogItemPricing> GetItemPricingAsync(Guid itemId, CancellationToken cancellationToken);
}

/// <summary>Resultado de consultar el pricing de un item a Catalog.</summary>
/// <param name="ServiceAvailable">False si la llamada gRPC fallo (circuito abierto / timeout): usar fallback al cache.</param>
/// <param name="Found">Si el item existe (solo valido cuando ServiceAvailable es true).</param>
/// <param name="ItemId">Identificador del item.</param>
/// <param name="MerchantId">Merchant dueno del item.</param>
/// <param name="Name">Nombre del item.</param>
/// <param name="PriceAmount">Precio base del item.</param>
/// <param name="Currency">Moneda ISO 4217 del precio.</param>
/// <param name="IsPurchasable">Si el item es comprable (merchant activo + disponible + con stock).</param>
public sealed record CatalogItemPricing(
    bool ServiceAvailable,
    bool Found,
    Guid ItemId,
    Guid MerchantId,
    string Name,
    decimal PriceAmount,
    string Currency,
    bool IsPurchasable)
{
    /// <summary>Resultado cuando Catalog no esta disponible.</summary>
    public static readonly CatalogItemPricing Unavailable =
        new(ServiceAvailable: false, Found: false, Guid.Empty, Guid.Empty, string.Empty, 0m, "DOP", IsPurchasable: false);
}
