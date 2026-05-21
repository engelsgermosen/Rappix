using FluentValidation;

namespace Rappix.Identity.Application.Users.UpdateProfile;

/// <summary>Validacion del comando de actualizacion de perfil.</summary>
internal sealed class UpdateProfileCommandValidator : AbstractValidator<UpdateProfileCommand>
{
    public UpdateProfileCommandValidator()
    {
        RuleFor(command => command.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(command => command.LastName).NotEmpty().MaximumLength(100);
    }
}
