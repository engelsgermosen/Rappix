using System.Globalization;
using Rappix.Merchants.Domain.Merchants;

namespace Rappix.Merchants.Application.Responses;

/// <summary>Una zona de cobertura: poligono (anillo de [lng,lat]) o circulo (centro + radio).</summary>
public sealed record ServiceAreaResponse(
    Guid Id,
    string Type,
    IReadOnlyList<double[]>? Polygon,
    double? CenterLatitude,
    double? CenterLongitude,
    int? RadiusMeters)
{
    /// <summary>Proyecta una ServiceArea de dominio.</summary>
    public static ServiceAreaResponse From(ServiceArea area)
    {
        if (area.Type == ServiceAreaType.Polygon && area.Polygon is not null)
        {
            IReadOnlyList<double[]> ring = [.. area.Polygon.ExteriorRing.Coordinates.Select(c => new[] { c.X, c.Y })];
            return new ServiceAreaResponse(area.Id, nameof(ServiceAreaType.Polygon), ring, null, null, null);
        }

        return new ServiceAreaResponse(
            area.Id,
            nameof(ServiceAreaType.Circle),
            Polygon: null,
            area.Center?.Y,
            area.Center?.X,
            area.RadiusMeters);
    }
}

/// <summary>Un rango horario.</summary>
public sealed record OperatingHoursResponse(string DayOfWeek, string OpensAt, string ClosesAt)
{
    /// <summary>Proyecta un OperatingHours de dominio.</summary>
    public static OperatingHoursResponse From(OperatingHours hours) => new(
        hours.DayOfWeek.ToString(),
        hours.OpensAt.ToString("HH:mm", CultureInfo.InvariantCulture),
        hours.ClosesAt.ToString("HH:mm", CultureInfo.InvariantCulture));
}

/// <summary>Vista completa de un merchant (owner / admin).</summary>
public sealed record MerchantResponse(
    Guid Id,
    Guid OwnerUserId,
    string Name,
    string Slug,
    string? Rnc,
    string? Description,
    string VerticalType,
    string Status,
    decimal CommissionPercentage,
    decimal? AverageRating,
    int TotalReviews,
    string? LogoObjectKey,
    DateTime CreatedAtUtc,
    IReadOnlyList<ServiceAreaResponse> ServiceAreas,
    IReadOnlyList<OperatingHoursResponse> OperatingHours)
{
    /// <summary>Proyecta el agregado completo.</summary>
    public static MerchantResponse From(Merchant merchant) => new(
        merchant.Id.Value,
        merchant.OwnerUserId,
        merchant.Name,
        merchant.Slug.Value,
        merchant.Rnc?.Value,
        merchant.Description,
        merchant.VerticalType.ToString(),
        merchant.Status.ToString(),
        merchant.CommissionPercentage.Value,
        merchant.AverageRating,
        merchant.TotalReviews,
        merchant.LogoObjectKey,
        merchant.CreatedAtUtc,
        [.. merchant.ServiceAreas.Select(ServiceAreaResponse.From)],
        [.. merchant.OperatingHours.Select(OperatingHoursResponse.From)]);
}
