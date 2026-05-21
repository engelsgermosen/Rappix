using NetTopologySuite.Geometries;
using Rappix.BuildingBlocks.Core.Domain;
using Rappix.BuildingBlocks.Core.Results;

namespace Rappix.Merchants.Domain.Merchants;

/// <summary>
/// Zona de cobertura de un merchant. Discriminada por <see cref="Type"/>: o un poligono custom
/// (<see cref="Polygon"/>, geometry) o un circulo (<see cref="Center"/> geography + <see cref="RadiusMeters"/>).
/// SRID 4326 (WGS 84).
/// </summary>
public sealed class ServiceArea : Entity<Guid>
{
    /// <summary>SRID estandar global lat/lng.</summary>
    public const int Srid = 4326;

    private ServiceArea()
    {
    }

    private ServiceArea(MerchantId merchantId, ServiceAreaType type, Polygon? polygon, Point? center, int? radiusMeters, DateTime createdAtUtc)
        : base(Guid.CreateVersion7())
    {
        MerchantId = merchantId;
        Type = type;
        Polygon = polygon;
        Center = center;
        RadiusMeters = radiusMeters;
        CreatedAtUtc = createdAtUtc;
    }

    /// <summary>Merchant propietario.</summary>
    public MerchantId MerchantId { get; private set; }

    /// <summary>Discriminador de forma.</summary>
    public ServiceAreaType Type { get; private set; }

    /// <summary>Poligono (solo si Type es Polygon). Columna geometry(Polygon,4326).</summary>
    public Polygon? Polygon { get; private set; }

    /// <summary>Centro del circulo (solo si Type es Circle). Columna geography(Point,4326).</summary>
    public Point? Center { get; private set; }

    /// <summary>Radio en metros (solo si Type es Circle).</summary>
    public int? RadiusMeters { get; private set; }

    /// <summary>Momento de creacion (UTC).</summary>
    public DateTime CreatedAtUtc { get; private set; }

    internal static Result<ServiceArea> CreatePolygon(MerchantId merchantId, Polygon polygon, DateTime utcNow)
    {
        if (polygon.IsEmpty || !polygon.IsValid)
        {
            return ServiceAreaErrors.InvalidPolygon;
        }

        if (polygon.SRID != Srid)
        {
            return ServiceAreaErrors.WrongSrid;
        }

        return new ServiceArea(merchantId, ServiceAreaType.Polygon, polygon, center: null, radiusMeters: null, utcNow);
    }

    internal static Result<ServiceArea> CreateCircle(MerchantId merchantId, Point center, int radiusMeters, DateTime utcNow)
    {
        if (center.IsEmpty)
        {
            return ServiceAreaErrors.InvalidCenter;
        }

        if (center.SRID != Srid)
        {
            return ServiceAreaErrors.WrongSrid;
        }

        if (radiusMeters is < 100 or > 50000)
        {
            return ServiceAreaErrors.InvalidRadius;
        }

        return new ServiceArea(merchantId, ServiceAreaType.Circle, polygon: null, center, radiusMeters, utcNow);
    }
}
