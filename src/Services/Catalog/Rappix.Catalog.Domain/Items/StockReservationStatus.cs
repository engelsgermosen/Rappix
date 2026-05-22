namespace Rappix.Catalog.Domain.Items;

/// <summary>Estado de una reserva de stock (hold) tomada por un pedido durante su saga.</summary>
public enum StockReservationStatus
{
    /// <summary>Hold activo: las unidades estan apartadas pero aun no descontadas del stock fisico.</summary>
    Held = 0,

    /// <summary>Hold confirmado: las unidades se descontaron del stock fisico (decremento definitivo).</summary>
    Committed = 1,

    /// <summary>Hold liberado: las unidades volvieron al disponible (compensacion o expiracion por TTL).</summary>
    Released = 2,
}
