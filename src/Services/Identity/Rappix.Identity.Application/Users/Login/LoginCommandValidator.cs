using FluentValidation;

namespace Rappix.Identity.Application.Users.Login;

/// <summary>Validacion del comando de login.</summary>
internal sealed class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(command => command.Identifier).NotEmpty();
        RuleFor(command => command.Password).NotEmpty();
    }
}
