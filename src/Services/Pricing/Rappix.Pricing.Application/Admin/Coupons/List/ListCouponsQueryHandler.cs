using MediatR;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.Pricing.Application.Abstractions;
using Rappix.Pricing.Application.Responses;
using Rappix.Pricing.Domain.Coupons;

namespace Rappix.Pricing.Application.Admin.Coupons.List;

/// <summary>Lista los cupones para administracion.</summary>
internal sealed class ListCouponsQueryHandler(ICouponRepository coupons)
    : IRequestHandler<ListCouponsQuery, Result<IReadOnlyList<CouponResponse>>>
{
    public async Task<Result<IReadOnlyList<CouponResponse>>> Handle(ListCouponsQuery query, CancellationToken cancellationToken)
    {
        IReadOnlyList<Coupon> all = await coupons.ListAsync(cancellationToken);
        IReadOnlyList<CouponResponse> response = [.. all.Select(CouponResponse.From)];
        return Result.Success(response);
    }
}
