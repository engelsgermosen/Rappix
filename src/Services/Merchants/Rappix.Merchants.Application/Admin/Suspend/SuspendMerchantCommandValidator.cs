using FluentValidation;

namespace Rappix.Merchants.Application.Admin.Suspend;

/// <summary>Validacion de la suspension.</summary>
internal sealed class SuspendMerchantCommandValidator : AbstractValidator<SuspendMerchantCommand>
{
    public SuspendMerchantCommandValidator() =>
        RuleFor(command => command.Reason).NotEmpty().MaximumLength(500);
}
