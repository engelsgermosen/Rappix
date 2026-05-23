using Rappix.Merchants.Domain.Merchants;

namespace Rappix.Merchants.Application.Responses;

/// <summary>
/// Estado de activacion de un merchant, para validaciones internas entre servicios (gRPC).
/// Incluye PickupLocation para que Orders la propague en CourierRequested (Fase 6).
/// </summary>
public sealed record MerchantActivationResponse(
    bool Found,
    bool IsActive,
    string Status,
    string Name,
    string Slug,
    string VerticalType,
    string OwnerUserId,
    double PickupLatitude,
    double PickupLongitude,
    bool HasPickupLocation)
{
    /// <summary>Respuesta para un merchant inexistente.</summary>
    public static readonly MerchantActivationResponse NotFoundResult =
        new(false, false, "NotFound", string.Empty, string.Empty, string.Empty, string.Empty, 0d, 0d, false);

    /// <summary>Proyecta el estado de activacion del agregado.</summary>
    public static MerchantActivationResponse From(Merchant merchant) => new(
        true,
        merchant.IsActive,
        merchant.Status.ToString(),
        merchant.Name,
        merchant.Slug.Value,
        merchant.VerticalType.ToString(),
        merchant.OwnerUserId.ToString(),
        // Y = lat, X = lng (axis order del Point construido por GeoFactory).
        merchant.PickupLocation?.Y ?? 0d,
        merchant.PickupLocation?.X ?? 0d,
        merchant.PickupLocation is not null);
}
