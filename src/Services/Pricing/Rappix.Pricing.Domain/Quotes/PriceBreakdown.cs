namespace Rappix.Pricing.Domain.Quotes;

/// <summary>
/// Desglose monetario completo de una cotizacion (value object embebido en la cotizacion). Todos los
/// montos comparten la moneda de la cotizacion y estan redondeados a la unidad menor por el calculador.
/// El total es exactamente la suma de los componentes (transparencia: el desglose siempre cuadra).
/// Orden de calculo (ADR-0005): subtotal -> surge -> descuento -> fees -> impuesto -> propina -> total.
/// </summary>
public sealed record PriceBreakdown
{
    /// <summary>Suma de (precio + modificadores) x cantidad de cada linea, antes de surge.</summary>
    public decimal Subtotal { get; init; }

    /// <summary>Multiplicador de surge aplicado (1.0 = sin surge).</summary>
    public decimal SurgeMultiplier { get; init; }

    /// <summary>Monto adicional por surge = subtotal x (multiplicador - 1).</summary>
    public decimal SurgeAmount { get; init; }

    /// <summary>Descuento total aplicado (cupon + primera compra), nunca mayor que el subtotal con surge.</summary>
    public decimal DiscountAmount { get; init; }

    /// <summary>Tarifa de envio (base por vertical + distancia x tarifa por km).</summary>
    public decimal DeliveryFee { get; init; }

    /// <summary>Tarifa de servicio de la plataforma (porcentaje configurable sobre el subtotal).</summary>
    public decimal ServiceFee { get; init; }

    /// <summary>Impuesto (ITBIS) sobre los componentes gravables.</summary>
    public decimal Tax { get; init; }

    /// <summary>Propina para el courier (input del cliente; no gravable, no comisionable).</summary>
    public decimal Tip { get; init; }

    /// <summary>Total a cobrar = subtotal + surge - descuento + envio + servicio + impuesto + propina.</summary>
    public decimal Total { get; init; }
}
