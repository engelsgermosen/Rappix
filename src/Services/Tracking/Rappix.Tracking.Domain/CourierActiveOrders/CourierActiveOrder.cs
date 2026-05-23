using Rappix.BuildingBlocks.Core.Domain;

namespace Rappix.Tracking.Domain.CourierActiveOrders;

/// <summary>
/// Mapping <see cref="Entity{TId}.Id"/>=CourierId -> OrderId que permite al
/// <c>CourierLocationUpdatedConsumer</c> resolver "que pedido es de este courier" en un PK lookup,
/// sin escanear <c>OrderTracking</c> por <c>LastCourierId</c>. Fase 6 (Dispatch) garantiza
/// invariante 1↔1 (claim atomico + unique partial index en <c>dispatch.courier_assignments</c>);
/// aqui se refleja como <c>CourierId</c> PK + <c>OrderId</c> con unique index.
/// </summary>
/// <remarks>
/// El ciclo de vida es minimo: <see cref="Create"/> al recibir CourierAssigned y DELETE al recibir
/// el terminal del pedido (Delivered/Cancelled/Failed). Sin "filas activas con ReleasedAtUtc" —
/// Dispatch ya audita el historico de asignaciones; Tracking proyecta solo el estado vigente.
/// </remarks>
public sealed class CourierActiveOrder : Entity<Guid>
{
    private CourierActiveOrder()
    {
    }

    private CourierActiveOrder(Guid courierId, Guid orderId, DateTime utcNow)
        : base(courierId)
    {
        OrderId = orderId;
        AssignedAtUtc = utcNow;
    }

    /// <summary>Pedido que el courier tiene asignado en este momento.</summary>
    public Guid OrderId { get; private set; }

    /// <summary>Momento en que se proyecto la asignacion.</summary>
    public DateTime AssignedAtUtc { get; private set; }

    /// <summary>Crea el mapping. Lo invoca CourierAssignedConsumer.</summary>
    public static CourierActiveOrder Create(Guid courierId, Guid orderId, DateTime utcNow) =>
        new(courierId, orderId, utcNow);

    /// <summary>
    /// Reasigna este courier a un pedido distinto. Cubre la carrera: courier termina pedido A y
    /// recibe asignacion a pedido B antes de que el consumer terminal del A borre la fila.
    /// Idempotente: si <c>OrderId</c> ya coincide, solo refresca el timestamp.
    /// </summary>
    public void UpdateAssignment(Guid orderId, DateTime utcNow)
    {
        OrderId = orderId;
        AssignedAtUtc = utcNow;
    }
}
