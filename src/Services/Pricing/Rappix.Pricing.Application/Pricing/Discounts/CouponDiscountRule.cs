using Rappix.BuildingBlocks.Core.Results;
using Rappix.Pricing.Application.Abstractions;
using Rappix.Pricing.Domain.Coupons;
using Rappix.Pricing.Domain.Errors;

namespace Rappix.Pricing.Application.Pricing.Discounts;

/// <summary>
/// Descuento por cupon: valida el codigo solicitado (existe, activo, vigente, sin exceder MaxUses, cumple
/// el monto minimo y el limite por usuario, aplica al merchant) y devuelve su tipo/valor. El uso (UsedCount)
/// no se incrementa al cotizar (eso ocurre al consumir la cotizacion: evita quemar usos en cotizaciones
/// abandonadas). Un cupon invalido hace fallar la cotizacion con un error Pricing.Coupon.*.
/// </summary>
internal sealed class CouponDiscountRule(ICouponRepository coupons) : IDiscountRule
{
    public async Task<Result<DiscountEvaluation?>> EvaluateAsync(DiscountRuleContext context, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(context.CouponCode))
        {
            return Result.Success<DiscountEvaluation?>(null);
        }

        Result<CouponCode> code = CouponCode.Create(context.CouponCode);
        if (code.IsFailure)
        {
            return Result.Failure<DiscountEvaluation?>(code.Error);
        }

        Coupon? coupon = await coupons.GetActiveByCodeAsync(code.Value.Value, cancellationToken);
        if (coupon is null)
        {
            return Result.Failure<DiscountEvaluation?>(CouponErrors.NotFound);
        }

        if (!coupon.AppliesToMerchant(context.MerchantId))
        {
            return Result.Failure<DiscountEvaluation?>(CouponErrors.MerchantMismatch);
        }

        int userRedemptions = await coupons.CountRedemptionsAsync(coupon.Id, context.CustomerUserId, cancellationToken);

        Result usable = coupon.ValidateUsable(context.Subtotal, context.UtcNow, userRedemptions);
        if (usable.IsFailure)
        {
            return Result.Failure<DiscountEvaluation?>(usable.Error);
        }

        var evaluation = new DiscountEvaluation(
            $"Coupon:{coupon.Code.Value}", coupon.DiscountType, coupon.Value, coupon.Id, coupon.Code.Value);
        return Result.Success<DiscountEvaluation?>(evaluation);
    }
}
