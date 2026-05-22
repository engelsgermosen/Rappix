using MediatR;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.Pricing.Application.Responses;
using Rappix.Pricing.Domain.Coupons;

namespace Rappix.Pricing.Application.Admin.Coupons.UpdateCoupon;

/// <summary>Actualiza un cupon (admin). El codigo es inmutable.</summary>
public sealed record UpdateCouponCommand(
    Guid CouponId,
    DiscountType DiscountType,
    decimal Value,
    int? MaxUses,
    decimal? MinOrderAmount,
    DateTime ValidFromUtc,
    DateTime ValidUntilUtc,
    int? PerUserLimit,
    Guid? MerchantId,
    bool IsActive) : IRequest<Result<CouponResponse>>;
