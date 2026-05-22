using Rappix.BuildingBlocks.Core.Results;
using Rappix.Pricing.Domain.Coupons;

namespace Rappix.Pricing.Application.Pricing.Discounts;

/// <summary>
/// Regla de descuento (Strategy). Evalua si aplica al contexto y, si aplica, devuelve una evaluacion con
/// el tipo y valor del descuento (el monto lo calcula el QuoteCalculator sobre el subtotal con surge).
/// Un fallo significa que el descuento solicitado es invalido (p. ej. cupon expirado) y la cotizacion debe
/// fallar con ese error tipado; un exito con valor null significa que la regla no aplica.
/// </summary>
public interface IDiscountRule
{
    /// <summary>Evalua la regla. Exito con null = no aplica; exito con evaluacion = aplica; fallo = error tipado.</summary>
    Task<Result<DiscountEvaluation?>> EvaluateAsync(DiscountRuleContext context, CancellationToken cancellationToken);
}

/// <summary>Contexto de evaluacion de descuentos.</summary>
/// <param name="CustomerUserId">Cliente que cotiza.</param>
/// <param name="MerchantId">Merchant del pedido.</param>
/// <param name="Subtotal">Subtotal de bienes (antes de surge), para validar el monto minimo del cupon.</param>
/// <param name="IsFirstOrder">Si es la primera compra del cliente (lo provee el caller; Orders en el futuro).</param>
/// <param name="CouponCode">Codigo de cupon solicitado (opcional).</param>
/// <param name="UtcNow">Momento actual (UTC).</param>
public sealed record DiscountRuleContext(
    Guid CustomerUserId,
    Guid MerchantId,
    decimal Subtotal,
    bool IsFirstOrder,
    string? CouponCode,
    DateTime UtcNow);

/// <summary>Resultado de una regla que aplica.</summary>
/// <param name="Source">Origen legible del descuento.</param>
/// <param name="Type">Tipo de descuento (porcentaje o monto fijo).</param>
/// <param name="Value">Valor (porcentaje 0-100 o monto fijo).</param>
/// <param name="CouponId">Id del cupon, si la regla es de cupon (para redimirlo al consumir).</param>
/// <param name="CouponCode">Codigo del cupon, si aplica.</param>
public sealed record DiscountEvaluation(
    string Source,
    DiscountType Type,
    decimal Value,
    CouponId? CouponId = null,
    string? CouponCode = null);
