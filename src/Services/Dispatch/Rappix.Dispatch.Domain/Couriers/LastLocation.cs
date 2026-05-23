using Rappix.BuildingBlocks.Core.Results;

namespace Rappix.Dispatch.Domain.Couriers;

/// <summary>
/// Ultima ubicacion reportada por el courier (value object). Mapeada como OwnsOne en el aggregate.
/// Es null hasta el primer POST /me/location. La fuente de verdad para matching es Redis Geo;
/// esta copia en BD permite rehidratar Redis tras reinicio + queries de auditoria.
/// </summary>
public sealed record LastLocation
{
    private LastLocation(double latitude, double longitude, DateTime reportedAtUtc)
    {
        Latitude = latitude;
        Longitude = longitude;
        ReportedAtUtc = reportedAtUtc;
    }

    /// <summary>Latitud (-90, 90).</summary>
    public double Latitude { get; }

    /// <summary>Longitud (-180, 180).</summary>
    public double Longitude { get; }

    /// <summary>Momento del reporte (UTC).</summary>
    public DateTime ReportedAtUtc { get; }

    /// <summary>Construye una LastLocation validando rangos de lat/lng.</summary>
    public static Result<LastLocation> Create(double latitude, double longitude, DateTime reportedAtUtc)
    {
        if (latitude is < -90 or > 90 || longitude is < -180 or > 180)
        {
            return CourierErrors.InvalidLocation;
        }

        return new LastLocation(latitude, longitude, reportedAtUtc);
    }
}
