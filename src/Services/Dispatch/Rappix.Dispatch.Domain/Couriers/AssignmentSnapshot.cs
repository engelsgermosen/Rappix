namespace Rappix.Dispatch.Domain.Couriers;

/// <summary>
/// Snapshot inmutable del pedido y el comercio al momento del claim del courier (Fase 13.6). Vive
/// embebido en <see cref="CourierAssignment"/> como value object OwnsOne (columnas con prefijo
/// <c>snapshot_*</c>); las lineas se persisten como JSONB. Lo construye <c>CourierRequestedConsumer</c>
/// a partir de los campos del <c>CourierRequestedIntegrationEvent</c> — Dispatch no llama a Orders ni
/// a Merchants en caliente, por eso el snapshot es "congelado" desde el momento del publish.
/// </summary>
/// <remarks>
/// Decisiones:
/// <list type="bullet">
/// <item>El courier ve <see cref="CustomerUserId"/> plano; el portal lo formatea como
///   "Cliente #xxxxxxxx" (primeros 8 chars del GUID). Cero datos personales en este snapshot.</item>
/// <item>Pickup hoy es solo lat/lng + <see cref="MerchantName"/> — no hay texto de calle porque
///   Merchants no lo modela. Documentado como follow-up en ADR 0003.</item>
/// <item>Lista vacia de <see cref="Lines"/> es valida (no rompe el GET) pero NO esperada en pedidos
///   nuevos: el aggregate Order siempre tiene >= 1 linea (validacion en <c>Order.Create</c>).</item>
/// </list>
/// </remarks>
public sealed record AssignmentSnapshot
{
    private AssignmentSnapshot(
        Guid customerUserId,
        string merchantName,
        double pickupLatitude,
        double pickupLongitude,
        string deliveryStreet,
        string? deliveryReference,
        double deliveryLatitude,
        double deliveryLongitude,
        decimal orderTotal,
        string orderCurrency,
        IReadOnlyList<AssignmentLineSnapshot> lines)
    {
        CustomerUserId = customerUserId;
        MerchantName = merchantName;
        PickupLatitude = pickupLatitude;
        PickupLongitude = pickupLongitude;
        DeliveryStreet = deliveryStreet;
        DeliveryReference = deliveryReference;
        DeliveryLatitude = deliveryLatitude;
        DeliveryLongitude = deliveryLongitude;
        OrderTotal = orderTotal;
        OrderCurrency = orderCurrency;
        Lines = lines;
    }

    // Constructor sin parametros para EF Core (materializacion del owned type).
    private AssignmentSnapshot()
    {
        Lines = [];
    }

    /// <summary>Cliente del pedido (snapshot).</summary>
    public Guid CustomerUserId { get; private init; }

    /// <summary>Nombre del comercio congelado al momento del claim.</summary>
    public string MerchantName { get; private init; } = string.Empty;

    /// <summary>Latitud del pickup (ubicacion fisica del comercio).</summary>
    public double PickupLatitude { get; private init; }

    /// <summary>Longitud del pickup.</summary>
    public double PickupLongitude { get; private init; }

    /// <summary>Calle / linea de direccion de entrega.</summary>
    public string DeliveryStreet { get; private init; } = string.Empty;

    /// <summary>Referencia opcional de la entrega (apto, piso, punto de referencia).</summary>
    public string? DeliveryReference { get; private init; }

    /// <summary>Latitud de entrega.</summary>
    public double DeliveryLatitude { get; private init; }

    /// <summary>Longitud de entrega.</summary>
    public double DeliveryLongitude { get; private init; }

    /// <summary>Total del pedido (snapshot del quote congelado).</summary>
    public decimal OrderTotal { get; private init; }

    /// <summary>Moneda ISO 4217 del total.</summary>
    public string OrderCurrency { get; private init; } = "DOP";

    /// <summary>Lineas del pedido (nombre + cantidad). Persistido como JSONB.</summary>
    public IReadOnlyList<AssignmentLineSnapshot> Lines { get; private init; }

    /// <summary>Construye un snapshot desde los campos del evento de integracion.</summary>
    public static AssignmentSnapshot Create(
        Guid customerUserId,
        string merchantName,
        double pickupLatitude,
        double pickupLongitude,
        string deliveryStreet,
        string? deliveryReference,
        double deliveryLatitude,
        double deliveryLongitude,
        decimal orderTotal,
        string orderCurrency,
        IReadOnlyList<AssignmentLineSnapshot> lines) =>
            new(customerUserId, merchantName ?? string.Empty, pickupLatitude, pickupLongitude,
                deliveryStreet ?? string.Empty, deliveryReference, deliveryLatitude, deliveryLongitude,
                orderTotal, orderCurrency ?? "DOP", lines ?? []);

    /// <summary>Snapshot vacio (defensivo) — para filas pre-Fase 13.6 que se rehidratan sin snapshot real.</summary>
    public static AssignmentSnapshot Empty { get; } =
        new(Guid.Empty, string.Empty, 0d, 0d, string.Empty, null, 0d, 0d, 0m, "DOP", []);
}

/// <summary>Item del snapshot del pedido (nombre + cantidad). Persistido como elemento del JSONB.</summary>
public sealed record AssignmentLineSnapshot(string ItemName, int Quantity);
