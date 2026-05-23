using Microsoft.EntityFrameworkCore;
using Rappix.Dispatch.Application.Abstractions;
using Rappix.Dispatch.Domain.Couriers;

namespace Rappix.Dispatch.Infrastructure.Persistence;

/// <summary>Repositorio de <see cref="CourierProfile"/> sobre el DbContext.</summary>
internal sealed class CourierRepository(DispatchDbContext db) : ICourierRepository
{
    public void Add(CourierProfile courier) => db.CourierProfiles.Add(courier);

    public Task<CourierProfile?> GetByIdAsync(CourierId id, CancellationToken cancellationToken) =>
        db.CourierProfiles.FirstOrDefaultAsync(courier => courier.Id == id, cancellationToken);

    public Task<bool> ExistsAsync(CourierId id, CancellationToken cancellationToken) =>
        db.CourierProfiles.AnyAsync(courier => courier.Id == id, cancellationToken);

    public async Task<IReadOnlyList<CourierProfile>> GetOnlineByIdsAsync(IEnumerable<CourierId> ids, CancellationToken cancellationToken)
    {
        // Filtro de seguridad post-GEOSEARCH: aunque Redis Geo solo deberia contener couriers Online,
        // una desincronizacion (p. ej. Redis flush sin rehidratar) podria devolver un courier ya Busy.
        Guid[] guidIds = [.. ids.Select(id => id.Value)];
        return await db.CourierProfiles
            .AsNoTracking()
            .Where(courier => courier.Status == CourierStatus.Online && guidIds.Contains(courier.Id.Value))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CourierProfile>> ListOnlineWithLocationAsync(CancellationToken cancellationToken) =>
        await db.CourierProfiles
            .AsNoTracking()
            .Where(courier => courier.Status == CourierStatus.Online && courier.LastLocation != null)
            .ToListAsync(cancellationToken);

    public async Task<bool> TryClaimAsync(CourierId id, DateTime utcNow, CancellationToken cancellationToken)
    {
        // Claim atomico: una sola sentencia UPDATE ... WHERE Status='Online' RETURNING (...).
        // El predicado WHERE Status='Online' ES el concurrency token: dos consumers compitiendo por
        // el mismo courier, solo uno actualiza la fila (rows == 1); el otro recibe 0 y prueba con el
        // siguiente candidato. xmin queda para los otros paths (perfil/vehiculo/location).
        int rows = await db.CourierProfiles
            .Where(courier => courier.Id == id && courier.Status == CourierStatus.Online)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(courier => courier.Status, CourierStatus.Busy)
                    .SetProperty(courier => courier.UpdatedAtUtc, utcNow),
                cancellationToken);

        return rows == 1;
    }
}
