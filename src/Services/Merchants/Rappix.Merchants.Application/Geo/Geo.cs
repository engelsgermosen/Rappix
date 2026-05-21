using NetTopologySuite;
using NetTopologySuite.Geometries;
using Rappix.Merchants.Domain.Merchants;

namespace Rappix.Merchants.Application.Geo;

/// <summary>
/// Construccion centralizada de geometrias con SRID 4326 y orden de ejes correcto (X = longitud,
/// Y = latitud). Evita errores de lat/lng invertidos dispersos por el codigo.
/// </summary>
public static class GeoFactory
{
    private static readonly GeometryFactory Factory =
        NtsGeometryServices.Instance.CreateGeometryFactory(srid: ServiceArea.Srid);

    /// <summary>Crea un Point (lng = X, lat = Y) con SRID 4326.</summary>
    public static Point CreatePoint(double latitude, double longitude) =>
        Factory.CreatePoint(new Coordinate(longitude, latitude));

    /// <summary>
    /// Crea un Polygon cerrado a partir de un anillo de coordenadas [longitud, latitud].
    /// El anillo se cierra automaticamente si el ultimo punto no coincide con el primero.
    /// </summary>
    public static Polygon CreatePolygon(IReadOnlyList<double[]> ring)
    {
        Coordinate[] coordinates = BuildClosedRing(ring);
        return Factory.CreatePolygon(coordinates);
    }

    private static Coordinate[] BuildClosedRing(IReadOnlyList<double[]> ring)
    {
        var coordinates = new List<Coordinate>(ring.Count + 1);
        foreach (double[] pair in ring)
        {
            // pair = [longitud, latitud]
            coordinates.Add(new Coordinate(pair[0], pair[1]));
        }

        if (coordinates.Count > 0 && !coordinates[0].Equals2D(coordinates[^1]))
        {
            coordinates.Add(coordinates[0].Copy());
        }

        return [.. coordinates];
    }
}
