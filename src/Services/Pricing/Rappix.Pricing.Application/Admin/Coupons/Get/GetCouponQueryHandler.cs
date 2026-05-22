using MediatR;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.Pricing.Application.Abstractions;
using Rappix.Pricing.Application.Responses;
using Rappix.Pricing.Domain.Coupons;
using Rappix.Pricing.Domain.Errors;

namespace Rappix.Pricing.Application.Admin.Coupons.Get;

/// <summary>Obtiene un cupon por id para administracion.</summary>
internal sealed class GetCouponQueryHandler(ICouponRepository coupons)
    : IRequestHandler<GetCouponQuery, Result<CouponResponse>>
{
    public async Task<Result<CouponResponse>> Handle(GetCouponQuery query, CancellationToken cancellationToken)
    {
        Coupon? coupon = await coupons.GetByIdAsync(new CouponId(query.CouponId), cancellationToken);
        return coupon is null
            ? Result.Failure<CouponResponse>(CouponErrors.NotFound)
            : CouponResponse.From(coupon);
    }
}
