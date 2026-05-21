using Rappix.Merchants.Domain.Merchants;

namespace Rappix.Merchants.Application.Responses;

/// <summary>Vista publica de un merchant (sin datos sensibles: comision, RNC, owner).</summary>
public sealed record PublicMerchantResponse(
    Guid Id,
    string Name,
    string Slug,
    string? Description,
    string VerticalType,
    decimal? AverageRating,
    int TotalReviews)
{
    /// <summary>Proyecta la vista publica.</summary>
    public static PublicMerchantResponse From(Merchant merchant) => new(
        merchant.Id.Value,
        merchant.Name,
        merchant.Slug.Value,
        merchant.Description,
        merchant.VerticalType.ToString(),
        merchant.AverageRating,
        merchant.TotalReviews);
}

/// <summary>Elemento ligero de la lista de busqueda por cercania.</summary>
public sealed record NearbyMerchantResponse(
    Guid Id,
    string Name,
    string Slug,
    string VerticalType,
    decimal? AverageRating,
    int TotalReviews)
{
    /// <summary>Proyecta un item de cercania.</summary>
    public static NearbyMerchantResponse From(Merchant merchant) => new(
        merchant.Id.Value,
        merchant.Name,
        merchant.Slug.Value,
        merchant.VerticalType.ToString(),
        merchant.AverageRating,
        merchant.TotalReviews);
}
