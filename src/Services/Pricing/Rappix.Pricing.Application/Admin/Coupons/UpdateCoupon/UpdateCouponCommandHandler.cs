using MediatR;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.BuildingBlocks.Core.Time;
using Rappix.Pricing.Application.Abstractions;
using Rappix.Pricing.Application.Responses;
using Rappix.Pricing.Domain.Coupons;
using Rappix.Pricing.Domain.Errors;

namespace Rappix.Pricing.Application.Admin.Coupons.UpdateCoupon;

/// <summary>Actualiza los campos editables de un cupon existente.</summary>
internal sealed class UpdateCouponCommandHandler(
    ICouponRepository coupons,
    IUnitOfWork unitOfWork,
    IDateTimeProvider clock)
    : IRequestHandler<UpdateCouponCommand, Result<CouponResponse>>
{
    public async Task<Result<CouponResponse>> Handle(UpdateCouponCommand command, CancellationToken cancellationToken)
    {
        Coupon? coupon = await coupons.GetByIdAsync(new CouponId(command.CouponId), cancellationToken);
        if (coupon is null)
        {
            return Result.Failure<CouponResponse>(CouponErrors.NotFound);
        }

        Result update = coupon.Update(
            command.DiscountType,
            command.Value,
            command.MaxUses,
            command.MinOrderAmount,
            command.ValidFromUtc,
            command.ValidUntilUtc,
            command.PerUserLimit,
            command.MerchantId,
            command.IsActive,
            clock.UtcNow);
        if (update.IsFailure)
        {
            return Result.Failure<CouponResponse>(update.Error);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return CouponResponse.From(coupon);
    }
}
