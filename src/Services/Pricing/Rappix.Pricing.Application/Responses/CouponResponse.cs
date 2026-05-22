using Rappix.Pricing.Domain.Coupons;

namespace Rappix.Pricing.Application.Responses;

/// <summary>Respuesta de un cupon (vista de administracion).</summary>
public sealed record CouponResponse(
    Guid CouponId,
    string Code,
    string DiscountType,
    decimal Value,
    int? MaxUses,
    int UsedCount,
    decimal? MinOrderAmount,
    DateTime ValidFromUtc,
    DateTime ValidUntilUtc,
    int? PerUserLimit,
    Guid? MerchantId,
    bool IsActive,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc)
{
    /// <summary>Proyecta el agregado cupon a su DTO.</summary>
    public static CouponResponse From(Coupon coupon) =>
        new(
            coupon.Id.Value,
            coupon.Code.Value,
            coupon.DiscountType.ToString(),
            coupon.Value,
            coupon.MaxUses,
            coupon.UsedCount,
            coupon.MinOrderAmount,
            coupon.ValidFromUtc,
            coupon.ValidUntilUtc,
            coupon.PerUserLimit,
            coupon.MerchantId,
            coupon.IsActive,
            coupon.CreatedAtUtc,
            coupon.UpdatedAtUtc);
}
