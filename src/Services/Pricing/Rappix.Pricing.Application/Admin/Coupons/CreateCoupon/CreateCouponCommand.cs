using MediatR;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.Pricing.Application.Responses;
using Rappix.Pricing.Domain.Coupons;

namespace Rappix.Pricing.Application.Admin.Coupons.CreateCoupon;

/// <summary>Crea un cupon (admin).</summary>
public sealed record CreateCouponCommand(
    string Code,
    DiscountType DiscountType,
    decimal Value,
    int? MaxUses,
    decimal? MinOrderAmount,
    DateTime ValidFromUtc,
    DateTime ValidUntilUtc,
    int? PerUserLimit,
    Guid? MerchantId) : IRequest<Result<CouponResponse>>;
