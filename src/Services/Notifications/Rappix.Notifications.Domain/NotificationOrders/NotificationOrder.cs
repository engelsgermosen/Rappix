using Rappix.BuildingBlocks.Core.Domain;

namespace Rappix.Notifications.Domain.NotificationOrders;

/// <summary>
/// Proyeccion local por pedido con los IDs minimos necesarios para resolver destinatarios cuando
/// los eventos del pedido NO los llevan. Se crea desde <c>OrderSubmittedIntegrationEvent</c> (que
/// si lleva <c>CustomerUserId + MerchantId</c>) y se completa con
/// <c>CourierAssignedIntegrationEvent</c> al asignar courier. Los consumers de <c>OrderAccepted</c>,
/// <c>OrderCancelled</c>, <c>OrderFailed</c> y <c>OrderDelivered</c> leen de aqui en vez de
/// extender los contratos de integracion.
/// </summary>
/// <remarks>
/// El <see cref="Entity{TId}.Id"/> ES el <c>OrderId</c> externo (relacion 1-1). Si llega un evento
/// del pedido SIN proyeccion previa (caso edge: OrderAccepted antes que OrderSubmittedConsumer
/// terminara de proyectar, o evento previo a la entrada en operacion de Notifications), el consumer
/// loguea Warning y persiste la notificacion como <c>Failed</c> con <c>errorReason="NotificationOrder missing"</c>
/// sin relanzar (no se quiere generar redelivery loop por algo informativo).
/// </remarks>
public sealed class NotificationOrder : Entity<Guid>
{
    // EF necesita ctor sin parametros para materializar.
    private NotificationOrder() { }

    private NotificationOrder(Guid orderId, Guid customerUserId, Guid merchantId, DateTime utcNow)
        : base(orderId)
    {
        CustomerUserId = customerUserId;
        MerchantId = merchantId;
        CreatedAtUtc = utcNow;
        UpdatedAtUtc = utcNow;
    }

    /// <summary>Cliente del pedido (cache de OrderSubmitted).</summary>
    public Guid CustomerUserId { get; private set; }

    /// <summary>Merchant del pedido (cache de OrderSubmitted). Se resuelve a OwnerUserId via <c>MerchantContact</c>.</summary>
    public Guid MerchantId { get; private set; }

    /// <summary>UserId del courier asignado (null hasta el primer <see cref="SetCourier"/>).</summary>
    public Guid? CourierUserId { get; private set; }

    /// <summary>Creacion (UTC) — momento del proyectado desde OrderSubmitted.</summary>
    public DateTime CreatedAtUtc { get; private set; }

    /// <summary>Ultima actualizacion (UTC).</summary>
    public DateTime UpdatedAtUtc { get; private set; }

    /// <summary>Crea la proyeccion desde un OrderSubmitted.</summary>
    public static NotificationOrder Create(Guid orderId, Guid customerUserId, Guid merchantId, DateTime utcNow) =>
        new(orderId, customerUserId, merchantId, utcNow);

    /// <summary>
    /// Setea el courier asignado. Idempotente con el mismo courierId; si el courierId cambia,
    /// sobreescribe (caso teorico de re-asignacion — Dispatch hoy no lo soporta pero defensivo).
    /// </summary>
    public void SetCourier(Guid courierUserId, DateTime utcNow)
    {
        if (CourierUserId == courierUserId)
        {
            return;
        }

        CourierUserId = courierUserId;
        UpdatedAtUtc = utcNow;
    }
}
