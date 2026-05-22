using FluentValidation;
using Rappix.Pricing.Domain.Coupons;

namespace Rappix.Pricing.Application.Admin.Coupons.CreateCoupon;

/// <summary>Validacion del comando de alta de cupon.</summary>
internal sealed class CreateCouponCommandValidator : AbstractValidator<CreateCouponCommand>
{
    public CreateCouponCommandValidator()
    {
        RuleFor(command => command.Code).NotEmpty().MaximumLength(CouponCode.MaxLength);
        RuleFor(command => command.Value).GreaterThan(0m);
        RuleFor(command => command.ValidUntilUtc).GreaterThan(command => command.ValidFromUtc);
        RuleFor(command => command.MaxUses).GreaterThan(0).When(command => command.MaxUses.HasValue);
        RuleFor(command => command.PerUserLimit).GreaterThan(0).When(command => command.PerUserLimit.HasValue);
        RuleFor(command => command.MinOrderAmount).GreaterThanOrEqualTo(0m).When(command => command.MinOrderAmount.HasValue);
    }
}
