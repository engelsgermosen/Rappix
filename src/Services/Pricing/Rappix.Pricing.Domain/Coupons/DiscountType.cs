namespace Rappix.Pricing.Domain.Coupons;

/// <summary>Tipo de descuento de un cupon o regla.</summary>
public enum DiscountType
{
    /// <summary>Porcentaje (0-100) sobre el monto base.</summary>
    Percentage = 0,

    /// <summary>Monto fijo en la moneda del pedido.</summary>
    FixedAmount = 1,
}
