using MediatR;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.BuildingBlocks.Core.Time;
using Rappix.Pricing.Application.Abstractions;
using Rappix.Pricing.Application.Responses;
using Rappix.Pricing.Domain.Coupons;
using Rappix.Pricing.Domain.Errors;

namespace Rappix.Pricing.Application.Admin.Coupons.CreateCoupon;

/// <summary>Crea un cupon validando codigo unico (no borrado) e invariantes del agregado.</summary>
internal sealed class CreateCouponCommandHandler(
    ICouponRepository coupons,
    IUnitOfWork unitOfWork,
    IDateTimeProvider clock)
    : IRequestHandler<CreateCouponCommand, Result<CouponResponse>>
{
    public async Task<Result<CouponResponse>> Handle(CreateCouponCommand command, CancellationToken cancellationToken)
    {
        Result<CouponCode> code = CouponCode.Create(command.Code);
        if (code.IsFailure)
        {
            return Result.Failure<CouponResponse>(code.Error);
        }

        if (await coupons.ExistsByCodeAsync(code.Value.Value, cancellationToken))
        {
            return Result.Failure<CouponResponse>(CouponErrors.DuplicateCode(code.Value.Value));
        }

        Result<Coupon> coupon = Coupon.Create(
            command.Code,
            command.DiscountType,
            command.Value,
            command.MaxUses,
            command.MinOrderAmount,
            command.ValidFromUtc,
            command.ValidUntilUtc,
            command.PerUserLimit,
            command.MerchantId,
            clock.UtcNow);
        if (coupon.IsFailure)
        {
            return Result.Failure<CouponResponse>(coupon.Error);
        }

        coupons.Add(coupon.Value);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return CouponResponse.From(coupon.Value);
    }
}
