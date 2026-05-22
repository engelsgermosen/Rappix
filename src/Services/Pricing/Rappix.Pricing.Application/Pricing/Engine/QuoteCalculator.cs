using Rappix.Pricing.Domain.Coupons;
using Rappix.Pricing.Domain.Common;
using Rappix.Pricing.Domain.Quotes;

namespace Rappix.Pricing.Application.Pricing.Engine;

/// <summary>
/// Motor de calculo del precio de una cotizacion. Funcion pura y deterministica: las mismas entradas
/// producen siempre el mismo desglose. Es el corazon del servicio y esta cubierto por pruebas unitarias
/// exhaustivas.
///
/// Orden de calculo (decision de negocio, ADR-0005), porque el orden afecta el total:
///   1. subtotal       = suma de (precio + modificadores) x cantidad de cada linea.
///   2. surge          = subtotal x multiplicador (el adicional es subtotal x (mult - 1)).
///   3. descuentos     = cada directiva sobre el subtotal con surge, sumadas y capadas a ese subtotal.
///   4. fees           = servicio (% sobre el subtotal base) + envio (resuelto por vertical/distancia).
///   5. impuesto       = ITBIS sobre los componentes gravables (bienes netos + fees gravables; NUNCA la propina).
///   6. propina        = tal cual la indico el cliente.
///   7. total          = bienes netos + envio + servicio + impuesto + propina.
///
/// Politica de redondeo: se calcula en precision completa y se redondea cada componente UNA sola vez al
/// final, a la unidad menor (2 decimales) con redondeo bancario (ToEven). El total es exactamente la suma
/// de los componentes redondeados, de modo que el desglose siempre cuadra.
/// </summary>
public static class QuoteCalculator
{
    /// <summary>Calcula el desglose monetario completo a partir de las entradas resueltas.</summary>
    public static PriceBreakdown Calculate(QuoteCalculationInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        decimal subtotal = input.Lines.Sum(line => line.LineSubtotal);

        decimal multiplier = input.SurgeMultiplier;
        decimal surgedSubtotal = subtotal * multiplier;
        decimal surgeAmount = surgedSubtotal - subtotal;

        decimal rawDiscount = 0m;
        foreach (DiscountDirective directive in input.Discounts)
        {
            rawDiscount += directive.Type == DiscountType.Percentage
                ? surgedSubtotal * directive.Value / 100m
                : directive.Value;
        }

        decimal discount = Math.Clamp(rawDiscount, 0m, surgedSubtotal);
        decimal goodsNet = surgedSubtotal - discount;

        decimal serviceFee = subtotal * input.ServiceFeePercentage / 100m;
        decimal deliveryFee = Math.Max(0m, input.DeliveryFee);

        decimal taxableBase = goodsNet
            + (input.TaxAppliesToDelivery ? deliveryFee : 0m)
            + (input.TaxAppliesToServiceFee ? serviceFee : 0m);
        decimal tax = taxableBase * input.TaxPercentage / 100m;

        decimal tip = Math.Max(0m, input.Tip);

        // Redondeo unico al final: cada componente a la unidad menor; el total es la suma de los redondeados.
        decimal roundedSubtotal = Round(subtotal);
        decimal roundedSurge = Round(surgeAmount);
        decimal roundedDiscount = Round(discount);
        decimal roundedDelivery = Round(deliveryFee);
        decimal roundedService = Round(serviceFee);
        decimal roundedTax = Round(tax);
        decimal roundedTip = Round(tip);

        decimal total = roundedSubtotal + roundedSurge - roundedDiscount
            + roundedDelivery + roundedService + roundedTax + roundedTip;

        return new PriceBreakdown
        {
            Subtotal = roundedSubtotal,
            SurgeMultiplier = decimal.Round(multiplier, 4, MidpointRounding.ToEven),
            SurgeAmount = roundedSurge,
            DiscountAmount = roundedDiscount,
            DeliveryFee = roundedDelivery,
            ServiceFee = roundedService,
            Tax = roundedTax,
            Tip = roundedTip,
            Total = total,
        };
    }

    private static decimal Round(decimal value) =>
        decimal.Round(value, Money.MinorUnitDecimals, MidpointRounding.ToEven);
}
