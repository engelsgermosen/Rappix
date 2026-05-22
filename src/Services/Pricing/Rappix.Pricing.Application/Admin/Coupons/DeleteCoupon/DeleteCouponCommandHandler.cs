using MediatR;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.BuildingBlocks.Core.Time;
using Rappix.Pricing.Application.Abstractions;
using Rappix.Pricing.Domain.Coupons;
using Rappix.Pricing.Domain.Errors;

namespace Rappix.Pricing.Application.Admin.Coupons.DeleteCoupon;

/// <summary>Desactiva un cupon (borrado logico).</summary>
internal sealed class DeleteCouponCommandHandler(
    ICouponRepository coupons,
    IUnitOfWork unitOfWork,
    IDateTimeProvider clock)
    : IRequestHandler<DeleteCouponCommand, Result>
{
    public async Task<Result> Handle(DeleteCouponCommand command, CancellationToken cancellationToken)
    {
        Coupon? coupon = await coupons.GetByIdAsync(new CouponId(command.CouponId), cancellationToken);
        if (coupon is null)
        {
            return Result.Failure(CouponErrors.NotFound);
        }

        coupon.Deactivate(clock.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
