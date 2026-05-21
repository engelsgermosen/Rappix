using FluentValidation;

namespace Rappix.Identity.Application.Users.ResendConfirmation;

/// <summary>Validacion del comando de reenvio de confirmacion.</summary>
internal sealed class ResendConfirmationCommandValidator : AbstractValidator<ResendConfirmationCommand>
{
    public ResendConfirmationCommandValidator()
    {
        RuleFor(command => command.Email).NotEmpty().MaximumLength(256);
    }
}
