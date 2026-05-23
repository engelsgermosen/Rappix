using Rappix.BuildingBlocks.Core.Domain;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.Orders.Domain.Abstractions;
using Rappix.Orders.Domain.Common;
using Rappix.Orders.Domain.Errors;
using Rappix.Orders.Domain.Orders.Events;

namespace Rappix.Orders.Domain.Orders;

/// <summary>
/// Agregado raiz del pedido: lo que cliente y merchant ven. Guarda un snapshot inmutable de las lineas y el
/// desglose congelado del quote, la direccion de entrega y un <see cref="Orders.OrderStatus"/> que ESPEJA
/// el estado de la saga (la mecanica distribuida vive en OrderState, separado). Las transiciones de estado
/// las aplica la proyeccion al reaccionar a los eventos de la saga.
/// </summary>
public sealed class Order : AggregateRoot<OrderId>, IHasDomainEvents
{
    private readonly List<OrderLine> _lines = [];

    private Order()
    {
    }

    private Order(
        OrderId id,
        Guid customerUserId,
        Guid merchantId,
        Guid merchantOwnerUserId,
        Guid quoteId,
        string vertical,
        string currency,
        decimal subtotal,
        decimal deliveryFee,
        decimal serviceFee,
        decimal tax,
        decimal tip,
        decimal discountAmount,
        decimal totalAmount,
        DeliveryAddress deliveryAddress,
        double pickupLatitude,
        double pickupLongitude,
        DateTime utcNow)
        : base(id)
    {
        CustomerUserId = customerUserId;
        MerchantId = merchantId;
        MerchantOwnerUserId = merchantOwnerUserId;
        QuoteId = quoteId;
        Vertical = vertical;
        Currency = currency;
        Subtotal = subtotal;
        DeliveryFee = deliveryFee;
        ServiceFee = serviceFee;
        Tax = tax;
        Tip = tip;
        DiscountAmount = discountAmount;
        TotalAmount = totalAmount;
        DeliveryAddress = deliveryAddress;
        PickupLatitude = pickupLatitude;
        PickupLongitude = pickupLongitude;
        Status = OrderStatus.Submitted;
        CreatedAtUtc = utcNow;
    }

    /// <summary>Cliente que realizo el pedido.</summary>
    public Guid CustomerUserId { get; private set; }

    /// <summary>Merchant del pedido (Id de la entidad Merchant).</summary>
    public Guid MerchantId { get; private set; }

    /// <summary>Usuario (Identity) propietario del merchant. Autoriza accept/reject del merchant (vs el sub del JWT).</summary>
    public Guid MerchantOwnerUserId { get; private set; }

    /// <summary>Cotizacion congelada que respalda el pedido.</summary>
    public Guid QuoteId { get; private set; }

    /// <summary>Vertical del pedido (snapshot, texto).</summary>
    public string Vertical { get; private set; } = null!;

    /// <summary>Moneda ISO 4217 de todos los montos.</summary>
    public string Currency { get; private set; } = null!;

    /// <summary>Subtotal de las lineas (snapshot del quote).</summary>
    public decimal Subtotal { get; private set; }

    /// <summary>Tarifa de envio (snapshot).</summary>
    public decimal DeliveryFee { get; private set; }

    /// <summary>Tarifa de servicio (snapshot).</summary>
    public decimal ServiceFee { get; private set; }

    /// <summary>Impuestos (snapshot).</summary>
    public decimal Tax { get; private set; }

    /// <summary>Propina (snapshot).</summary>
    public decimal Tip { get; private set; }

    /// <summary>Descuento aplicado (snapshot).</summary>
    public decimal DiscountAmount { get; private set; }

    /// <summary>Total a cobrar (snapshot del quote congelado).</summary>
    public decimal TotalAmount { get; private set; }

    /// <summary>Direccion de entrega (value object embebido).</summary>
    public DeliveryAddress DeliveryAddress { get; private set; } = null!;

    /// <summary>
    /// Latitud del pickup (ubicacion fisica del comercio) congelada al crear el pedido. La saga la
    /// propaga en CourierRequestedIntegrationEvent para que Dispatch (Fase 6) haga matching geo.
    /// </summary>
    public double PickupLatitude { get; private set; }

    /// <summary>Longitud del pickup congelada al crear el pedido.</summary>
    public double PickupLongitude { get; private set; }

    /// <summary>Estado del pedido (espejo del estado de la saga).</summary>
    public OrderStatus Status { get; private set; }

    /// <summary>Razon de cancelacion o fallo, si aplica.</summary>
    public string? CancellationReason { get; private set; }

    /// <summary>Momento de creacion/envio (UTC).</summary>
    public DateTime CreatedAtUtc { get; private set; }

    /// <summary>Momento en que el merchant acepto (UTC).</summary>
    public DateTime? AcceptedAtUtc { get; private set; }

    /// <summary>Momento en que paso a en curso (UTC).</summary>
    public DateTime? InProgressAtUtc { get; private set; }

    /// <summary>Momento de finalizacion (UTC).</summary>
    public DateTime? CompletedAtUtc { get; private set; }

    /// <summary>Momento en que se cancelo o fallo (UTC).</summary>
    public DateTime? ClosedAtUtc { get; private set; }

    /// <summary>Ultima actualizacion (UTC).</summary>
    public DateTime? UpdatedAtUtc { get; private set; }

    /// <summary>Lineas (snapshot inmutable de los items pedidos).</summary>
    public IReadOnlyCollection<OrderLine> Lines => _lines.AsReadOnly();

    /// <summary>
    /// Crea un pedido con el snapshot del quote congelado y eleva <see cref="OrderSubmittedDomainEvent"/>
    /// (que arranca la saga). Falla si no hay lineas.
    /// </summary>
    public static Result<Order> Create(
        Guid customerUserId,
        Guid merchantId,
        Guid merchantOwnerUserId,
        Guid quoteId,
        string vertical,
        string currency,
        IReadOnlyList<OrderLine> lines,
        decimal subtotal,
        decimal deliveryFee,
        decimal serviceFee,
        decimal tax,
        decimal tip,
        decimal discountAmount,
        decimal totalAmount,
        DeliveryAddress deliveryAddress,
        double pickupLatitude,
        double pickupLongitude,
        DateTime utcNow)
    {
        if (lines.Count == 0)
        {
            return Result.Failure<Order>(OrderErrors.NoLines);
        }

        var order = new Order(
            OrderId.New(), customerUserId, merchantId, merchantOwnerUserId, quoteId, vertical, currency,
            subtotal, deliveryFee, serviceFee, tax, tip, discountAmount, totalAmount, deliveryAddress,
            pickupLatitude, pickupLongitude, utcNow);
        order._lines.AddRange(lines);

        order.RaiseDomainEvent(new OrderSubmittedDomainEvent(
            order.Id, customerUserId, merchantId, quoteId, totalAmount, currency,
            deliveryAddress.Street, deliveryAddress.Latitude, deliveryAddress.Longitude,
            pickupLatitude, pickupLongitude));

        return order;
    }

    /// <summary>Indica si el pedido esta en un estado terminal (no cambiara mas).</summary>
    public bool IsTerminal => Status is OrderStatus.Completed or OrderStatus.Cancelled or OrderStatus.Failed or OrderStatus.NeedsReview;

    /// <summary>Indica si el pedido puede cancelarse por el cliente (antes del punto de no retorno).</summary>
    public bool IsCancellable => Status is OrderStatus.Submitted or OrderStatus.AwaitingMerchant or OrderStatus.AwaitingPayment or OrderStatus.AwaitingCourier;

    /// <summary>
    /// Aplica una transicion de estado proveniente de la saga (la usa la proyeccion). Idempotente: re-aplicar
    /// el mismo estado no rompe nada; los timestamps se fijan en su primera ocurrencia. No retrocede desde un
    /// estado terminal.
    /// </summary>
    public void ApplyStatus(OrderStatus status, DateTime utcNow, string? reason = null)
    {
        if (IsTerminal)
        {
            return;
        }

        Status = status;
        UpdatedAtUtc = utcNow;

        switch (status)
        {
            case OrderStatus.AwaitingPayment:
                AcceptedAtUtc ??= utcNow;
                break;
            case OrderStatus.InProgress:
                InProgressAtUtc ??= utcNow;
                break;
            case OrderStatus.Completed:
                CompletedAtUtc ??= utcNow;
                break;
            case OrderStatus.Cancelled or OrderStatus.Failed:
                ClosedAtUtc ??= utcNow;
                CancellationReason = reason;
                break;
            default:
                break;
        }
    }
}
