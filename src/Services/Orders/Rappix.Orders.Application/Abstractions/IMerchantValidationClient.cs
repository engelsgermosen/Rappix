namespace Rappix.Orders.Application.Abstractions;

/// <summary>
/// Cliente del servicio Merchants (gRPC): resuelve datos basicos de un comercio por su id, incluido el
/// <see cref="MerchantInfo.OwnerUserId"/> (el usuario de Identity que lo administra), que Orders persiste en
/// el pedido para autorizar las acciones del merchant (accept/reject) contra el sub del JWT.
/// </summary>
public interface IMerchantValidationClient
{
    /// <summary>Obtiene el merchant por id; ServiceAvailable=false indica que gRPC no respondio.</summary>
    Task<MerchantInfo> GetAsync(Guid merchantId, CancellationToken cancellationToken);
}

/// <summary>Datos basicos de un comercio resueltos desde Merchants.</summary>
public sealed record MerchantInfo(bool ServiceAvailable, bool Found, bool IsActive, Guid OwnerUserId)
{
    /// <summary>Resultado cuando el servicio Merchants no esta disponible.</summary>
    public static readonly MerchantInfo Unavailable = new(ServiceAvailable: false, Found: false, IsActive: false, OwnerUserId: Guid.Empty);
}
