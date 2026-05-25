using Rappix.BuildingBlocks.Core.Domain;

namespace Rappix.Dispatch.Domain.Couriers;

/// <summary>
/// Asignacion historica de un courier a un pedido. Tabla por separado del aggregate
/// CourierProfile para conservar historial. La invariante "un courier solo tiene una
/// asignacion activa" la garantizan dos unique partial indexes en BD (sobre CourierId
/// y OrderId con ReleasedAtUtc IS NULL), red de seguridad ante carreras concurrentes.
/// </summary>
public sealed class CourierAssignment : Entity<Guid>
{
    private CourierAssignment()
    {
        Snapshot = AssignmentSnapshot.Empty;
    }

    private CourierAssignment(Guid id, CourierId courierId, Guid orderId, AssignmentSnapshot snapshot, DateTime assignedAtUtc)
        : base(id)
    {
        CourierId = courierId;
        OrderId = orderId;
        Snapshot = snapshot;
        AssignedAtUtc = assignedAtUtc;
    }

    /// <summary>Courier asignado.</summary>
    public CourierId CourierId { get; private set; }

    /// <summary>Pedido (sin FK; vive en otro servicio).</summary>
    public Guid OrderId { get; private set; }

    /// <summary>
    /// Snapshot congelado de los datos del pedido al momento del claim (Fase 13.6): pickup, delivery,
    /// nombre del comercio, total, lineas. Lo lee <c>GetCurrentAssignmentQuery</c> para que el courier
    /// vea las direcciones sin consultar Orders/Merchants.
    /// </summary>
    public AssignmentSnapshot Snapshot { get; private set; }

    /// <summary>Momento de la asignacion (UTC).</summary>
    public DateTime AssignedAtUtc { get; private set; }

    /// <summary>Momento de la liberacion (UTC). Null mientras la asignacion esta activa.</summary>
    public DateTime? ReleasedAtUtc { get; private set; }

    /// <summary>
    /// Razon de la liberacion: "delivered" | "cancelled" | "failed". Null mientras esta activa.
    /// </summary>
    public string? ReleaseReason { get; private set; }

    /// <summary>Indica si la asignacion sigue activa.</summary>
    public bool IsActive => ReleasedAtUtc is null;

    /// <summary>Crea una asignacion activa con su snapshot. La unicidad se valida por BD (indices parciales).</summary>
    public static CourierAssignment Create(CourierId courierId, Guid orderId, AssignmentSnapshot snapshot, DateTime assignedAtUtc) =>
        new(Guid.CreateVersion7(), courierId, orderId, snapshot, assignedAtUtc);

    /// <summary>Marca la asignacion como liberada con la razon dada. Idempotente: si ya estaba liberada, no-op.</summary>
    public void Release(DateTime releasedAtUtc, string reason)
    {
        if (!IsActive)
        {
            return;
        }

        ReleasedAtUtc = releasedAtUtc;
        ReleaseReason = reason;
    }
}
