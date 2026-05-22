namespace Rappix.Orders.Domain.Orders;

/// <summary>
/// Estado del pedido visible para cliente/merchant. Espejo simplificado del estado de la saga (que tiene
/// estados internos adicionales como ValidatingQuote, ReservingStock, Committing y los de compensacion).
/// </summary>
public enum OrderStatus
{
    /// <summary>Enviado por el cliente; la saga valida la cotizacion y reserva stock.</summary>
    Submitted = 0,

    /// <summary>Esperando que el merchant acepte o rechace.</summary>
    AwaitingMerchant = 1,

    /// <summary>Aceptado; esperando el cobro del pago.</summary>
    AwaitingPayment = 2,

    /// <summary>Pagado; esperando la asignacion de un courier.</summary>
    AwaitingCourier = 3,

    /// <summary>En curso (courier asignado, stock confirmado): preparacion y entrega.</summary>
    InProgress = 4,

    /// <summary>Completado (entregado). Estado terminal feliz.</summary>
    Completed = 5,

    /// <summary>Cancelado (rechazo, timeout, sin courier o cancelacion del cliente). Terminal.</summary>
    Cancelled = 6,

    /// <summary>Fallido (fallo de pago, stock o quote). Terminal.</summary>
    Failed = 7,

    /// <summary>Requiere intervencion manual (p. ej. fallo al confirmar stock tras cobrar). Terminal.</summary>
    NeedsReview = 8,
}
