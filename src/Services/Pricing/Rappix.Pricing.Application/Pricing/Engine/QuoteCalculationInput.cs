using Rappix.Pricing.Domain.Coupons;
using Rappix.Pricing.Domain.Quotes;

namespace Rappix.Pricing.Application.Pricing.Engine;

/// <summary>
/// Entradas ya resueltas del calculo de una cotizacion. El handler resuelve precios (gRPC a Catalog),
/// surge (estrategia), descuentos (reglas) y envio (estrategia por vertical); el <see cref="QuoteCalculator"/>
/// hace toda la aritmetica de forma pura y deterministica a partir de estas entradas.
/// </summary>
public sealed record QuoteCalculationInput
{
    /// <summary>Lineas del carrito con su precio unitario, modificadores y cantidad resueltos.</summary>
    public required IReadOnlyList<QuoteLine> Lines { get; init; }

    /// <summary>Multiplicador de surge ya resuelto y capado (1.0 = sin surge).</summary>
    public required decimal SurgeMultiplier { get; init; }

    /// <summary>Directivas de descuento a aplicar sobre el subtotal con surge.</summary>
    public required IReadOnlyList<DiscountDirective> Discounts { get; init; }

    /// <summary>Tarifa de envio ya resuelta por la estrategia del vertical.</summary>
    public required decimal DeliveryFee { get; init; }

    /// <summary>Porcentaje de tarifa de servicio sobre el subtotal (0-100).</summary>
    public required decimal ServiceFeePercentage { get; init; }

    /// <summary>Porcentaje de impuesto (ITBIS) sobre los componentes gravables (0-100).</summary>
    public required decimal TaxPercentage { get; init; }

    /// <summary>Si la tarifa de envio es gravable.</summary>
    public required bool TaxAppliesToDelivery { get; init; }

    /// <summary>Si la tarifa de servicio es gravable.</summary>
    public required bool TaxAppliesToServiceFee { get; init; }

    /// <summary>Propina del cliente (no gravable, no comisionable).</summary>
    public required decimal Tip { get; init; }
}

/// <summary>Directiva de descuento: aplica un porcentaje o monto fijo sobre la base de descuento.</summary>
/// <param name="Source">Origen legible (p. ej. "FirstOrder" o "Coupon:WELCOME10").</param>
/// <param name="Type">Tipo de descuento (porcentaje o monto fijo).</param>
/// <param name="Value">Valor (porcentaje 0-100 o monto fijo).</param>
public sealed record DiscountDirective(string Source, DiscountType Type, decimal Value);
