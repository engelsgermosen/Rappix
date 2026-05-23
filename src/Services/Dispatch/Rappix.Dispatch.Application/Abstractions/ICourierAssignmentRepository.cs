using Rappix.Dispatch.Domain.Couriers;

namespace Rappix.Dispatch.Application.Abstractions;

/// <summary>Acceso a las asignaciones historicas courier->pedido.</summary>
public interface ICourierAssignmentRepository
{
    /// <summary>Marca una nueva asignacion para insercion.</summary>
    void Add(CourierAssignment assignment);

    /// <summary>Asignacion activa para un pedido (ReleasedAtUtc IS NULL) o null si no existe.</summary>
    Task<CourierAssignment?> GetActiveByOrderAsync(Guid orderId, CancellationToken cancellationToken);

    /// <summary>Asignacion activa para un courier o null si no existe.</summary>
    Task<CourierAssignment?> GetActiveByCourierAsync(CourierId courierId, CancellationToken cancellationToken);
}
