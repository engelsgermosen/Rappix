using Rappix.Merchants.Domain.Merchants;

namespace Rappix.Merchants.Application.Responses;

/// <summary>Estado de activacion de un merchant, para validaciones internas entre servicios (gRPC).</summary>
public sealed record MerchantActivationResponse(
    bool Found,
    bool IsActive,
    string Status,
    string Name,
    string Slug,
    string VerticalType)
{
    /// <summary>Respuesta para un merchant inexistente.</summary>
    public static readonly MerchantActivationResponse NotFoundResult =
        new(false, false, "NotFound", string.Empty, string.Empty, string.Empty);

    /// <summary>Proyecta el estado de activacion del agregado.</summary>
    public static MerchantActivationResponse From(Merchant merchant) => new(
        true,
        merchant.IsActive,
        merchant.Status.ToString(),
        merchant.Name,
        merchant.Slug.Value,
        merchant.VerticalType.ToString());
}
