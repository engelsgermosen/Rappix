using Rappix.BuildingBlocks.Core.Results;

namespace Rappix.Pricing.Domain.Errors;

/// <summary>Errores del value object Money.</summary>
public static class MoneyErrors
{
    /// <summary>Monto negativo.</summary>
    public static readonly Error NegativeAmount =
        Error.Validation("Pricing.Money.NegativeAmount", "El monto no puede ser negativo.");

    /// <summary>Moneda invalida.</summary>
    public static readonly Error InvalidCurrency =
        Error.Validation("Pricing.Money.InvalidCurrency", "La moneda debe ser un codigo ISO 4217 de 3 letras.");
}

/// <summary>Errores del value object Percentage.</summary>
public static class PercentageErrors
{
    /// <summary>Porcentaje fuera del rango [0, 100].</summary>
    public static readonly Error OutOfRange =
        Error.Validation("Pricing.Percentage.OutOfRange", "El porcentaje debe estar entre 0 y 100.");
}

/// <summary>Errores del agregado cotizacion (Quote) y sus lineas.</summary>
public static class QuoteErrors
{
    /// <summary>Cotizacion no encontrada.</summary>
    public static readonly Error NotFound =
        Error.NotFound("Pricing.Quote.NotFound", "Cotizacion no encontrada.");

    /// <summary>La cotizacion expiro.</summary>
    public static readonly Error Expired =
        Error.Conflict("Pricing.Quote.Expired", "La cotizacion expiro. Solicite una nueva.");

    /// <summary>La cotizacion ya fue consumida por un pedido.</summary>
    public static readonly Error AlreadyConsumed =
        Error.Conflict("Pricing.Quote.AlreadyConsumed", "La cotizacion ya fue consumida.");

    /// <summary>La cotizacion no tiene lineas.</summary>
    public static readonly Error NoLines =
        Error.Validation("Pricing.Quote.NoLines", "La cotizacion debe incluir al menos una linea.");

    /// <summary>Rango de expiracion invalido.</summary>
    public static readonly Error InvalidExpiration =
        Error.Validation("Pricing.Quote.InvalidExpiration", "La expiracion debe ser posterior a la creacion.");

    /// <summary>El conflicto de concurrencia al guardar (xmin de un cupon).</summary>
    public static readonly Error ConcurrencyConflict =
        Error.Conflict("Pricing.Quote.ConcurrencyConflict", "Conflicto de concurrencia al consumir la cotizacion. Reintente.");

    /// <summary>El merchant no esta activo y no puede cotizarse.</summary>
    public static readonly Error MerchantInactive =
        Error.Conflict("Pricing.Quote.MerchantInactive", "El merchant no esta activo; no se puede cotizar.");

    /// <summary>El merchant no existe.</summary>
    public static readonly Error MerchantNotFound =
        Error.NotFound("Pricing.Quote.MerchantNotFound", "Merchant no encontrado.");
}

/// <summary>Errores de las lineas de la cotizacion.</summary>
public static class QuoteLineErrors
{
    /// <summary>Cantidad invalida (debe ser positiva).</summary>
    public static readonly Error InvalidQuantity =
        Error.Validation("Pricing.QuoteLine.InvalidQuantity", "La cantidad de cada linea debe ser positiva.");

    /// <summary>Precio unitario negativo.</summary>
    public static readonly Error NegativeUnitPrice =
        Error.Validation("Pricing.QuoteLine.NegativeUnitPrice", "El precio unitario no puede ser negativo.");

    /// <summary>Total de modificadores negativo.</summary>
    public static readonly Error NegativeModifierTotal =
        Error.Validation("Pricing.QuoteLine.NegativeModifierTotal", "El total de modificadores no puede ser negativo.");

    /// <summary>El item no es comprable (merchant inactivo, item oculto o sin stock).</summary>
    public static readonly Error ItemNotPurchasable =
        Error.Conflict("Pricing.QuoteLine.ItemNotPurchasable", "Uno de los items no esta disponible para la compra.");

    /// <summary>No se pudo resolver el precio del item (Catalog no respondio y no hay precio cacheado).</summary>
    public static readonly Error PriceUnavailable =
        Error.Conflict("Pricing.QuoteLine.PriceUnavailable", "No se pudo obtener el precio de uno de los items.");
}

/// <summary>Errores del agregado cupon.</summary>
public static class CouponErrors
{
    /// <summary>Cupon no encontrado.</summary>
    public static readonly Error NotFound =
        Error.NotFound("Pricing.Coupon.NotFound", "Cupon no encontrado.");

    /// <summary>Codigo de cupon obligatorio.</summary>
    public static readonly Error CodeRequired =
        Error.Validation("Pricing.Coupon.CodeRequired", "El codigo del cupon es obligatorio.");

    /// <summary>Codigo de cupon demasiado largo.</summary>
    public static readonly Error CodeTooLong =
        Error.Validation("Pricing.Coupon.CodeTooLong", "El codigo del cupon es demasiado largo.");

    /// <summary>Valor de descuento invalido.</summary>
    public static readonly Error InvalidValue =
        Error.Validation("Pricing.Coupon.InvalidValue", "El valor del descuento es invalido para el tipo de cupon.");

    /// <summary>Rango de vigencia invalido.</summary>
    public static readonly Error InvalidDateRange =
        Error.Validation("Pricing.Coupon.InvalidDateRange", "La fecha de inicio debe ser anterior a la de fin.");

    /// <summary>Limite de usos invalido.</summary>
    public static readonly Error InvalidMaxUses =
        Error.Validation("Pricing.Coupon.InvalidMaxUses", "El maximo de usos debe ser positivo.");

    /// <summary>Limite por usuario invalido.</summary>
    public static readonly Error InvalidPerUserLimit =
        Error.Validation("Pricing.Coupon.InvalidPerUserLimit", "El limite por usuario debe ser positivo.");

    /// <summary>Monto minimo de orden invalido.</summary>
    public static readonly Error InvalidMinOrderAmount =
        Error.Validation("Pricing.Coupon.InvalidMinOrderAmount", "El monto minimo de orden no puede ser negativo.");

    /// <summary>El cupon esta inactivo.</summary>
    public static readonly Error Inactive =
        Error.Conflict("Pricing.Coupon.Inactive", "El cupon no esta activo.");

    /// <summary>El cupon expiro.</summary>
    public static readonly Error Expired =
        Error.Conflict("Pricing.Coupon.Expired", "El cupon expiro.");

    /// <summary>El cupon aun no es valido.</summary>
    public static readonly Error NotYetValid =
        Error.Conflict("Pricing.Coupon.NotYetValid", "El cupon aun no esta vigente.");

    /// <summary>El cupon alcanzo su maximo de usos.</summary>
    public static readonly Error MaxUsesReached =
        Error.Conflict("Pricing.Coupon.MaxUsesReached", "El cupon alcanzo su maximo de usos.");

    /// <summary>El pedido no cumple el monto minimo del cupon.</summary>
    public static readonly Error MinOrderNotMet =
        Error.Conflict("Pricing.Coupon.MinOrderNotMet", "El pedido no alcanza el monto minimo para usar el cupon.");

    /// <summary>El cliente alcanzo su limite de usos del cupon.</summary>
    public static readonly Error PerUserLimitReached =
        Error.Conflict("Pricing.Coupon.PerUserLimitReached", "Ya usaste este cupon el maximo de veces permitido.");

    /// <summary>El cupon es de otro merchant.</summary>
    public static readonly Error MerchantMismatch =
        Error.Conflict("Pricing.Coupon.MerchantMismatch", "El cupon no aplica a este merchant.");

    /// <summary>Ya existe un cupon con ese codigo.</summary>
    public static Error DuplicateCode(string code) =>
        Error.Conflict("Pricing.Coupon.DuplicateCode", $"Ya existe un cupon con el codigo '{code}'.");
}

/// <summary>Errores del agregado regla de surge.</summary>
public static class SurgeErrors
{
    /// <summary>Regla de surge no encontrada.</summary>
    public static readonly Error NotFound =
        Error.NotFound("Pricing.SurgeRule.NotFound", "Regla de surge no encontrada.");

    /// <summary>Rango horario invalido.</summary>
    public static readonly Error InvalidHourRange =
        Error.Validation("Pricing.SurgeRule.InvalidHourRange", "El rango horario debe cumplir 0 <= inicio < fin <= 24.");

    /// <summary>Multiplicador invalido.</summary>
    public static readonly Error InvalidMultiplier =
        Error.Validation("Pricing.SurgeRule.InvalidMultiplier", "El multiplicador de surge debe ser >= 1.0 y <= el cap configurado.");
}
