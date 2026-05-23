using Rappix.Dispatch.Domain.Couriers;

namespace Rappix.Dispatch.Application.Abstractions;

/// <summary>Acceso a la persistencia del agregado CourierProfile.</summary>
public interface ICourierRepository
{
    /// <summary>Marca un nuevo courier para insercion.</summary>
    void Add(CourierProfile courier);

    /// <summary>Obtiene un courier por Id (= UserId de Identity).</summary>
    Task<CourierProfile?> GetByIdAsync(CourierId id, CancellationToken cancellationToken);

    /// <summary>Idempotencia del consumer de UserRegistered.</summary>
    Task<bool> ExistsAsync(CourierId id, CancellationToken cancellationToken);

    /// <summary>
    /// Carga los couriers Online de los ids dados (filtro de seguridad post-GEOSEARCH para descartar
    /// los que cambiaron a Busy/Offline mientras estaban en el geo set de Redis).
    /// </summary>
    Task<IReadOnlyList<CourierProfile>> GetOnlineByIdsAsync(IEnumerable<CourierId> ids, CancellationToken cancellationToken);

    /// <summary>
    /// Lista todos los couriers Online con LastLocation conocida (para rehidratar Redis Geo al arranque).
    /// </summary>
    Task<IReadOnlyList<CourierProfile>> ListOnlineWithLocationAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Intento atomico de marcar el courier como Busy (claim de asignacion). Una sola sentencia
    /// UPDATE ... SET Status='Busy' WHERE Id=@id AND Status='Online' RETURNING Id. Devuelve true si
    /// el courier gano la carrera; false si otro consumer le gano (Status ya no es Online).
    /// </summary>
    Task<bool> TryClaimAsync(CourierId id, DateTime utcNow, CancellationToken cancellationToken);
}
