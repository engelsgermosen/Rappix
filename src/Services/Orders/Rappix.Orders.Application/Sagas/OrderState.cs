using MassTransit;
using Rappix.Orders.Domain.Orders;

namespace Rappix.Orders.Application.Sagas;

/// <summary>
/// Instancia de la saga del pedido (la mecanica distribuida, separada del agregado Order). Persistida por
/// el repositorio EF Core de MassTransit en rappix_orders. CorrelationId = OrderId. Guarda los escalares que
/// la saga necesita para orquestar (consumir quote, cobrar, pedir courier) y los tokens de los timeouts
/// programados. La concurrencia se controla con xmin (token de version mapeado en la configuracion EF).
/// </summary>
public sealed class OrderState : SagaStateMachineInstance
{
    /// <summary>Clave de correlacion (igual al OrderId).</summary>
    public Guid CorrelationId { get; set; }

    /// <summary>Estado actual de la saga (persistido como texto).</summary>
    public string CurrentState { get; set; } = null!;

    // --- Datos de orquestacion (del OrderSubmittedIntegrationEvent) ---

    /// <summary>Cotizacion a consumir/revertir.</summary>
    public Guid QuoteId { get; set; }

    /// <summary>Cliente del pedido (para el cobro).</summary>
    public Guid CustomerUserId { get; set; }

    /// <summary>Merchant del pedido (para el courier).</summary>
    public Guid MerchantId { get; set; }

    /// <summary>Total a cobrar (del quote congelado).</summary>
    public decimal TotalAmount { get; set; }

    /// <summary>Moneda ISO 4217.</summary>
    public string Currency { get; set; } = null!;

    /// <summary>Latitud de entrega (para el courier).</summary>
    public double DeliveryLatitude { get; set; }

    /// <summary>Longitud de entrega (para el courier).</summary>
    public double DeliveryLongitude { get; set; }

    /// <summary>Latitud del pickup (ubicacion fisica del comercio); la saga la pasa a CourierRequested.</summary>
    public double PickupLatitude { get; set; }

    /// <summary>Longitud del pickup.</summary>
    public double PickupLongitude { get; set; }

    // --- Snapshot del pedido para propagar a CourierRequested (Fase 13.6) ---

    /// <summary>Nombre del comercio (snapshot del OrderSubmitted). Nullable para sagas pre-13.6.</summary>
    public string? MerchantName { get; set; }

    /// <summary>Calle de entrega (snapshot). Nullable para sagas pre-13.6.</summary>
    public string? DeliveryStreet { get; set; }

    /// <summary>Referencia opcional de la direccion de entrega.</summary>
    public string? DeliveryReference { get; set; }

    /// <summary>
    /// Lineas del pedido serializadas como JSON: <c>[{"itemName": "...", "quantity": N}]</c>.
    /// Nullable para sagas pre-13.6. Texto plano (no jsonb) — la saga lo reescribe entero, no se queryea.
    /// </summary>
    public string? LinesJson { get; set; }

    // --- Tracking ---

    /// <summary>Indica si el pago se capturo (determina si una compensacion debe reembolsar).</summary>
    public bool PaymentCaptured { get; set; }

    /// <summary>Identificador del pago capturado, si lo hubo.</summary>
    public Guid? PaymentId { get; set; }

    /// <summary>Courier asignado, si lo hubo.</summary>
    public Guid? CourierId { get; set; }

    /// <summary>Estado terminal al que aterriza la compensacion: Cancelled o Failed.</summary>
    public OrderStatus CompensationTerminal { get; set; }

    /// <summary>Razon de cancelacion/fallo (se propaga al pedido y a los eventos terminales).</summary>
    public string? Reason { get; set; }

    // --- Tokens de timeouts programados (para poder cancelarlos al salir del estado) ---

    /// <summary>Token del timeout de aceptacion del merchant.</summary>
    public Guid? MerchantTimeoutTokenId { get; set; }

    /// <summary>Token del timeout de pago.</summary>
    public Guid? PaymentTimeoutTokenId { get; set; }

    /// <summary>Token del timeout de courier.</summary>
    public Guid? CourierTimeoutTokenId { get; set; }
}
