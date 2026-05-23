using Rappix.Tracking.Domain.OrderTrackings;

namespace Rappix.Tracking.Application.Abstractions;

/// <summary>
/// Escritura del read model: lo usan los consumers para crear el row al recibir OrderSubmitted
/// y luego actualizarlo en cada transicion. Las cargas devuelven el aggregate tracking (no el
/// snapshot inmutable de <see cref="IOrderTrackingReadRepository"/>).
/// </summary>
public interface IOrderTrackingRepository
{
    /// <summary>Recupera el aggregate por su <c>OrderId</c> (null si aun no se ha proyectado).</summary>
    Task<OrderTracking?> GetByOrderIdAsync(Guid orderId, CancellationToken cancellationToken);

    /// <summary>Lo marca para insercion al proximo SaveChanges.</summary>
    void Add(OrderTracking tracking);
}
