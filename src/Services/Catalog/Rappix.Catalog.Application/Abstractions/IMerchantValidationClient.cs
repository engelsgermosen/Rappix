namespace Rappix.Catalog.Application.Abstractions;

/// <summary>
/// Puerto hacia el servicio Merchants (gRPC). La implementacion (Infraestructura) envuelve el cliente
/// gRPC con reintentos y circuit breaker; si el servicio no responde, <see cref="MerchantValidation.ServiceAvailable"/>
/// es false y el llamador hace fallback al estado local cacheado (Catalog.IsEnabled).
/// </summary>
public interface IMerchantValidationClient
{
    /// <summary>Consulta el estado autoritativo de un merchant en el servicio Merchants.</summary>
    Task<MerchantValidation> ValidateAsync(Guid merchantId, CancellationToken cancellationToken);
}

/// <summary>Resultado de validar un merchant contra el servicio Merchants.</summary>
/// <param name="ServiceAvailable">False si la llamada gRPC fallo (circuito abierto / timeout): usar fallback local.</param>
/// <param name="Exists">Si el merchant existe (solo valido cuando ServiceAvailable es true).</param>
/// <param name="IsActive">Si el merchant esta activo (solo valido cuando ServiceAvailable es true).</param>
public sealed record MerchantValidation(bool ServiceAvailable, bool Exists, bool IsActive)
{
    /// <summary>Resultado cuando el servicio Merchants no esta disponible.</summary>
    public static readonly MerchantValidation Unavailable = new(ServiceAvailable: false, Exists: false, IsActive: false);
}
