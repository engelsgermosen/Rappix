using FluentValidation;

namespace Rappix.Merchants.Application.Admin.Reject;

/// <summary>Validacion del rechazo.</summary>
internal sealed class RejectMerchantCommandValidator : AbstractValidator<RejectMerchantCommand>
{
    public RejectMerchantCommandValidator() =>
        RuleFor(command => command.Reason).NotEmpty().MaximumLength(500);
}
