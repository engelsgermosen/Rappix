using FluentValidation;

namespace Rappix.Merchants.Application.Admin.UpdateCommission;

/// <summary>Validacion del ajuste de comision.</summary>
internal sealed class UpdateCommissionCommandValidator : AbstractValidator<UpdateCommissionCommand>
{
    public UpdateCommissionCommandValidator() =>
        RuleFor(command => command.CommissionPercentage).InclusiveBetween(0m, 100m);
}
