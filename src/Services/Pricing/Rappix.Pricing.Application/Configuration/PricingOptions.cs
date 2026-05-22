using Rappix.Pricing.Domain.Coupons;

namespace Rappix.Pricing.Application.Configuration;

/// <summary>
/// Parametros del motor de pricing (seccion "Pricing"). Toda la politica monetaria es configurable:
/// tarifas, impuesto (ITBIS), cap de surge, factor de demanda (stand-in del futuro Dispatch) y el
/// descuento de primera compra. Los porcentajes estan en escala 0-100.
/// </summary>
public sealed class PricingOptions
{
    /// <summary>Nombre de la seccion de configuracion.</summary>
    public const string SectionName = "Pricing";

    /// <summary>Moneda de las cotizaciones (ISO 4217).</summary>
    public string Currency { get; set; } = "DOP";

    /// <summary>Minutos de vigencia de una cotizacion desde su creacion.</summary>
    public int QuoteExpiryMinutes { get; set; } = 10;

    /// <summary>Porcentaje de tarifa de servicio de la plataforma sobre el subtotal (0-100).</summary>
    public decimal ServiceFeePercentage { get; set; } = 5.0m;

    /// <summary>Porcentaje de impuesto (ITBIS RD) sobre los componentes gravables (0-100).</summary>
    public decimal TaxPercentage { get; set; } = 18.0m;

    /// <summary>Si la tarifa de envio es gravable.</summary>
    public bool TaxAppliesToDelivery { get; set; } = true;

    /// <summary>Si la tarifa de servicio es gravable.</summary>
    public bool TaxAppliesToServiceFee { get; set; } = true;

    /// <summary>Cap de seguridad del multiplicador de surge (hard limit). Nunca se supera.</summary>
    public decimal MaxSurgeMultiplier { get; set; } = 3.0m;

    /// <summary>
    /// Factor de demanda que multiplica al surge base por horario/zona. Hoy es un stand-in configurable
    /// de la senal de demanda real que proveera Dispatch en una fase futura (ADR-0005). Default 1.0.
    /// </summary>
    public decimal DemandFactor { get; set; } = 1.0m;

    /// <summary>Desfase horario (horas) para evaluar las franjas de surge en hora local (RD = AST = -4).</summary>
    public int SurgeTimeZoneOffsetHours { get; set; } = -4;

    /// <summary>Permite acumular el descuento de primera compra con el de un cupon.</summary>
    public bool AllowStackingCouponWithFirstOrder { get; set; } = true;

    /// <summary>Tarifas de envio por familia de vertical.</summary>
    public DeliveryOptions Delivery { get; set; } = new();

    /// <summary>Descuento de primera compra.</summary>
    public FirstOrderDiscountOptions FirstOrderDiscount { get; set; } = new();
}

/// <summary>Tarifas de envio por familia de vertical (base + tarifa por km).</summary>
public sealed class DeliveryOptions
{
    /// <summary>Tarifa estandar (Food, Grocery, Pharmacy).</summary>
    public DeliveryRate Standard { get; set; } = new() { BaseFee = 60m, PerKm = 15m };

    /// <summary>Tarifa de paqueteria (Parcel), tipicamente mas alta.</summary>
    public DeliveryRate Parcel { get; set; } = new() { BaseFee = 80m, PerKm = 25m };
}

/// <summary>Tarifa de envio: monto base mas tarifa por kilometro.</summary>
public sealed class DeliveryRate
{
    /// <summary>Tarifa base.</summary>
    public decimal BaseFee { get; set; }

    /// <summary>Tarifa por kilometro recorrido.</summary>
    public decimal PerKm { get; set; }
}

/// <summary>Configuracion del descuento de primera compra.</summary>
public sealed class FirstOrderDiscountOptions
{
    /// <summary>Si el descuento de primera compra esta habilitado.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Tipo de descuento (porcentaje o monto fijo).</summary>
    public DiscountType Type { get; set; } = DiscountType.Percentage;

    /// <summary>Valor del descuento (porcentaje 0-100 o monto fijo, segun el tipo).</summary>
    public decimal Value { get; set; } = 15.0m;
}
