using FluentValidation;

namespace Rappix.Identity.Application.Users.ChangePassword;

/// <summary>Validacion del comando de cambio de contrasena.</summary>
internal sealed class ChangePasswordCommandValidator : AbstractValidator<ChangePasswordCommand>
{
    public ChangePasswordCommandValidator()
    {
        RuleFor(command => command.CurrentPassword).NotEmpty();

        RuleFor(command => command.NewPassword)
            .NotEmpty()
            .MinimumLength(8)
            .MaximumLength(128)
            .Matches("[A-Za-z]").WithMessage("La contrasena debe contener al menos una letra.")
            .Matches("[0-9]").WithMessage("La contrasena debe contener al menos un numero.")
            .NotEqual(command => command.CurrentPassword).WithMessage("La nueva contrasena debe ser distinta de la actual.");
    }
}
