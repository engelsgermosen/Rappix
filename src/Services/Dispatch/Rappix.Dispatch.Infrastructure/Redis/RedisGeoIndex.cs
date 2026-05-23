using Rappix.Dispatch.Application.Abstractions;
using Rappix.Dispatch.Domain.Couriers;
using StackExchange.Redis;

namespace Rappix.Dispatch.Infrastructure.Redis;

/// <summary>
/// Implementacion de <see cref="IRedisGeoIndex"/> sobre StackExchange.Redis. Usa el comando GEO de
/// Redis (sorted set + geohash interno). Solo couriers Online con LastLocation estan aqui — el
/// ciclo de vida (add/remove) lo controlan los handlers de Application.
/// </summary>
internal sealed class RedisGeoIndex(IConnectionMultiplexer redis) : IRedisGeoIndex
{
    private const string GeoKey = "dispatch:couriers:geo";

    private readonly IDatabase _database = redis.GetDatabase();

    public Task AddOrUpdateAsync(CourierId courierId, double latitude, double longitude, CancellationToken cancellationToken) =>
        // GEOADD usa (longitude, latitude) en ese orden (X = lng, Y = lat).
        _database.GeoAddAsync(GeoKey, new GeoEntry(longitude, latitude, courierId.Value.ToString()));

    public Task RemoveAsync(CourierId courierId, CancellationToken cancellationToken) =>
        _database.GeoRemoveAsync(GeoKey, courierId.Value.ToString());

    public async Task<IReadOnlyList<NearbyCourier>> SearchNearbyAsync(
        double latitude, double longitude, double radiusMeters, int limit, CancellationToken cancellationToken)
    {
        GeoRadiusResult[] results = await _database.GeoSearchAsync(
            key: GeoKey,
            longitude: longitude,
            latitude: latitude,
            shape: new GeoSearchCircle(radiusMeters, GeoUnit.Meters),
            count: limit,
            options: GeoRadiusOptions.WithCoordinates | GeoRadiusOptions.WithDistance);

        var candidates = new List<NearbyCourier>(results.Length);
        foreach (GeoRadiusResult result in results)
        {
            if (!Guid.TryParse(result.Member.ToString(), out Guid id) || result.Position is null)
            {
                continue;
            }

            candidates.Add(new NearbyCourier(
                CourierId: CourierId.FromUserId(id),
                Latitude: result.Position.Value.Latitude,
                Longitude: result.Position.Value.Longitude,
                DistanceMeters: result.Distance ?? 0d));
        }

        return candidates;
    }
}
