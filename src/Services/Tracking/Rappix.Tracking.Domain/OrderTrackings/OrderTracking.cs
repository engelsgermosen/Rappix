using Rappix.BuildingBlocks.Core.Domain;

namespace Rappix.Tracking.Domain.OrderTrackings;

/// <summary>
/// Read model por pedido: la unica fila por OrderId que mantiene Tracking. Proyecta eventos de Orders
/// (OrderSubmitted, OrderAccepted, OrderCancelled, OrderFailed, OrderCompleted) y Dispatch
/// (CourierAssigned, CourierLocationUpdated, OrderDelivered) al estado que el cliente ve en vivo.
/// </summary>
/// <remarks>
/// Decisiones de diseno:
/// <list type="bullet">
/// <item><c>OrderId</c> como <see cref="Guid"/> plano (no strongly-typed): la identidad ES la del
///   pedido externo; sin reglas de invariante sobre el id, no aporta strongly-typed.</item>
/// <item>NO emite <c>IDomainEvent</c> ni hereda <see cref="AggregateRoot{TId}"/>: read model puro,
///   no publica nada al bus. Hereda de <see cref="Entity{TId}"/> solo para reusar la igualdad por Id.</item>
/// <item>Concurrencia optimista por columna shadow <c>xmin</c> (configurada en EF). Las carreras
///   tipicas son location vs terminal (el terminal gana porque borra <c>CourierActiveOrder</c>);
///   xmin atrapa el caso raro de dos consumidores procesando el mismo evento (que la inbox EF
///   ya deberia bloquear, pero defensa en profundidad).</item>
/// <item>Todos los <c>Apply*</c> son idempotentes: si el estado destino ya esta alcanzado, no-op.
///   Necesario para soportar <c>OrderDelivered</c> + <c>OrderCompleted</c> consecutivos.</item>
/// <item><see cref="ApplyLocation"/> descarta updates con timestamp menor o igual al ultimo
///   conocido — evita pintar saltos hacia atras al cliente si los eventos llegan fuera de orden.</item>
/// </list>
/// </remarks>
public sealed class OrderTracking : Entity<Guid>
{
    private OrderTracking()
    {
    }

    private OrderTracking(
        Guid orderId,
        Guid customerUserId,
        Guid merchantId,
        double pickupLat,
        double pickupLng,
        double deliveryLat,
        double deliveryLng,
        DateTime utcNow)
        : base(orderId)
    {
        CustomerUserId = customerUserId;
        MerchantId = merchantId;
        CurrentStatus = TrackingStatus.Placed;
        PickupLat = pickupLat;
        PickupLng = pickupLng;
        DeliveryLat = deliveryLat;
        DeliveryLng = deliveryLng;
        CreatedAtUtc = utcNow;
        UpdatedAtUtc = utcNow;
    }

    /// <summary>Cliente que realizo el pedido — usado para el check de ownership en Subscribe y GET.</summary>
    public Guid CustomerUserId { get; private set; }

    /// <summary>Merchant del pedido (cache de OrderSubmitted).</summary>
    public Guid MerchantId { get; private set; }

    /// <summary>Estado actual visible al cliente.</summary>
    public TrackingStatus CurrentStatus { get; private set; }

    /// <summary>Razon textual cuando CurrentStatus es <see cref="TrackingStatus.Cancelled"/> o <see cref="TrackingStatus.Failed"/>.</summary>
    public string? StatusReason { get; private set; }

    /// <summary>Courier actualmente asignado (null hasta el primer <see cref="ApplyCourierAssigned"/>).</summary>
    public Guid? LastCourierId { get; private set; }

    /// <summary>Latitud ultima reportada por el courier asignado.</summary>
    public double? LastCourierLat { get; private set; }

    /// <summary>Longitud ultima reportada por el courier asignado.</summary>
    public double? LastCourierLng { get; private set; }

    /// <summary>Timestamp del ultimo location update aceptado.</summary>
    public DateTime? LastLocationAtUtc { get; private set; }

    /// <summary>Latitud del pickup (cache de OrderSubmitted).</summary>
    public double PickupLat { get; private set; }

    /// <summary>Longitud del pickup (cache de OrderSubmitted).</summary>
    public double PickupLng { get; private set; }

    /// <summary>Latitud del delivery (cache de OrderSubmitted).</summary>
    public double DeliveryLat { get; private set; }

    /// <summary>Longitud del delivery (cache de OrderSubmitted).</summary>
    public double DeliveryLng { get; private set; }

    /// <summary>Creacion (UTC).</summary>
    public DateTime CreatedAtUtc { get; private set; }

    /// <summary>Ultima actualizacion (UTC).</summary>
    public DateTime UpdatedAtUtc { get; private set; }

    /// <summary>
    /// Crea el read model desde un OrderSubmitted (lo invoca OrderSubmittedConsumer). El status
    /// arranca en <see cref="TrackingStatus.Placed"/> y se guardan pickup/delivery para que el
    /// cliente pueda renderizar el mapa al hidratarse.
    /// </summary>
    public static OrderTracking FromOrderSubmitted(
        Guid orderId,
        Guid customerUserId,
        Guid merchantId,
        double pickupLat,
        double pickupLng,
        double deliveryLat,
        double deliveryLng,
        DateTime utcNow) =>
            new(orderId, customerUserId, merchantId, pickupLat, pickupLng, deliveryLat, deliveryLng, utcNow);

    /// <summary>
    /// Aplica el evento OrderAccepted (merchant acepto). Idempotente: si ya esta en
    /// <see cref="TrackingStatus.MerchantAccepted"/> o posterior, no-op.
    /// </summary>
    public void ApplyMerchantAccepted(DateTime acceptedAtUtc)
    {
        if (CurrentStatus >= TrackingStatus.MerchantAccepted)
        {
            return;
        }

        CurrentStatus = TrackingStatus.MerchantAccepted;
        UpdatedAtUtc = acceptedAtUtc;
    }

    /// <summary>
    /// Aplica el evento CourierAssigned (Dispatch claim atomico exitoso). Captura el courierId
    /// y avanza el status. Idempotente: si ya esta CourierAssigned con el mismo courier, no-op.
    /// </summary>
    public void ApplyCourierAssigned(Guid courierId, DateTime utcNow)
    {
        if (CurrentStatus >= TrackingStatus.CourierAssigned && LastCourierId == courierId)
        {
            return;
        }

        LastCourierId = courierId;
        CurrentStatus = TrackingStatus.CourierAssigned;
        UpdatedAtUtc = utcNow;
    }

    /// <summary>
    /// Aplica un location update del courier asignado. Devuelve <c>false</c> si el timestamp es
    /// menor o igual al ultimo aceptado (descarte de stale/out-of-order) — el consumer NO debe
    /// hacer SaveChanges ni push SignalR en ese caso.
    /// </summary>
    public bool ApplyLocation(Guid courierId, double lat, double lng, DateTime reportedAtUtc)
    {
        // Si el courier reportado no coincide con el asignado, ignora (carrera entre asignaciones).
        if (LastCourierId != courierId)
        {
            return false;
        }

        // Stale: el evento llego despues que otro mas reciente.
        if (LastLocationAtUtc is not null && reportedAtUtc <= LastLocationAtUtc.Value)
        {
            return false;
        }

        LastCourierLat = lat;
        LastCourierLng = lng;
        LastLocationAtUtc = reportedAtUtc;
        UpdatedAtUtc = reportedAtUtc;
        return true;
    }

    /// <summary>
    /// Aplica un terminal de entrega (OrderDelivered de Dispatch u OrderCompleted de Orders).
    /// Idempotente: si ya esta en un terminal, no-op (no degrada Delivered a otro estado).
    /// </summary>
    public void ApplyDelivered(DateTime utcNow)
    {
        if (IsTerminal())
        {
            return;
        }

        CurrentStatus = TrackingStatus.Delivered;
        UpdatedAtUtc = utcNow;
    }

    /// <summary>
    /// Aplica el evento OrderCancelled. Idempotente: si ya esta en un terminal, no-op.
    /// </summary>
    public void ApplyCancelled(string reason, DateTime utcNow)
    {
        if (IsTerminal())
        {
            return;
        }

        CurrentStatus = TrackingStatus.Cancelled;
        StatusReason = reason;
        UpdatedAtUtc = utcNow;
    }

    /// <summary>
    /// Aplica el evento OrderFailed. Idempotente: si ya esta en un terminal, no-op.
    /// </summary>
    public void ApplyFailed(string reason, DateTime utcNow)
    {
        if (IsTerminal())
        {
            return;
        }

        CurrentStatus = TrackingStatus.Failed;
        StatusReason = reason;
        UpdatedAtUtc = utcNow;
    }

    private bool IsTerminal() =>
        CurrentStatus is TrackingStatus.Delivered or TrackingStatus.Cancelled or TrackingStatus.Failed;
}
