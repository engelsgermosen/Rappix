using Rappix.Dispatch.Domain.Couriers;

namespace Rappix.Dispatch.Application.Abstractions;

/// <summary>
/// Indice geoespacial Redis para el matching de couriers. Abstrae StackExchange.Redis para que
/// los handlers no dependan del cliente concreto y los tests unitarios puedan mockearla.
/// Key del sorted set: <c>dispatch:couriers:geo</c>.
/// </summary>
public interface IRedisGeoIndex
{
    /// <summary>
    /// Agrega o actualiza la coord del courier en el geo set (GEOADD). Si el courier ya estaba
    /// en el set, sobrescribe la posicion.
    /// </summary>
    Task AddOrUpdateAsync(CourierId courierId, double latitude, double longitude, CancellationToken cancellationToken);

    /// <summary>
    /// Quita al courier del geo set (ZREM). Llamarlo cuando pasa a Busy u Offline.
    /// </summary>
    Task RemoveAsync(CourierId courierId, CancellationToken cancellationToken);

    /// <summary>
    /// Busca couriers en un radio dado (GEOSEARCH FROMLONLAT BYRADIUS m ASC COUNT N).
    /// </summary>
    Task<IReadOnlyList<NearbyCourier>> SearchNearbyAsync(
        double latitude, double longitude, double radiusMeters, int limit, CancellationToken cancellationToken);
}

/// <summary>Courier candidato devuelto por GEOSEARCH (con su distancia y coords).</summary>
public sealed record NearbyCourier(CourierId CourierId, double Latitude, double Longitude, double DistanceMeters);
