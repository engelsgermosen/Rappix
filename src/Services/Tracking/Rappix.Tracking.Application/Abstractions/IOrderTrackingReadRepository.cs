namespace Rappix.Tracking.Application.Abstractions;

/// <summary>
/// Lectura del read model con <c>AsNoTracking()</c>. La separa del repo de escritura para que el
/// hub (Subscribe) y el endpoint REST (GET) no carguen el change-tracker en cada request.
/// </summary>
public interface IOrderTrackingReadRepository
{
    /// <summary>
    /// Devuelve el snapshot del tracking o <c>null</c> si no existe (el cliente reintenta o recibe
    /// "no autorizado" segun el caller).
    /// </summary>
    Task<OrderTrackingSnapshot?> GetSnapshotAsync(Guid orderId, CancellationToken cancellationToken);
}
