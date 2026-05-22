namespace Rappix.Pricing.Application.Abstractions;

/// <summary>
/// Puerto hacia el servicio Merchants (gRPC). La implementacion (Infraestructura) envuelve el cliente gRPC
/// con resiliencia; si el servicio no responde, <see cref="MerchantPricingInfo.ServiceAvailable"/> es false
/// y el llamador procede sin bloquear la cotizacion (fallback documentado en ADR-0005).
/// </summary>
public interface IMerchantPricingClient
{
    /// <summary>Consulta el estado y vertical de un merchant en el servicio Merchants.</summary>
    Task<MerchantPricingInfo> GetAsync(Guid merchantId, CancellationToken cancellationToken);
}

/// <summary>Resultado de consultar un merchant a Merchants.</summary>
/// <param name="ServiceAvailable">False si la llamada gRPC fallo (circuito abierto / timeout): usar fallback.</param>
/// <param name="Found">Si el merchant existe (solo valido cuando ServiceAvailable es true).</param>
/// <param name="IsActive">Si el merchant esta activo (solo valido cuando ServiceAvailable es true).</param>
/// <param name="VerticalType">Vertical del merchant (Food, Pharmacy, Grocery, Parcel), si se conoce.</param>
public sealed record MerchantPricingInfo(bool ServiceAvailable, bool Found, bool IsActive, string VerticalType)
{
    /// <summary>Resultado cuando el servicio Merchants no esta disponible.</summary>
    public static readonly MerchantPricingInfo Unavailable =
        new(ServiceAvailable: false, Found: false, IsActive: false, VerticalType: string.Empty);
}
