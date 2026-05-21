using FluentValidation;
using Rappix.Merchants.Domain.Merchants;

namespace Rappix.Merchants.Application.Merchants.UpdateProfile;

/// <summary>Validacion del comando de actualizacion de perfil.</summary>
internal sealed class UpdateMerchantProfileCommandValidator : AbstractValidator<UpdateMerchantProfileCommand>
{
    public UpdateMerchantProfileCommandValidator()
    {
        RuleFor(command => command.Name).NotEmpty().MaximumLength(150);
        RuleFor(command => command.Slug).NotEmpty();
        RuleFor(command => command.Description).MaximumLength(2000);
        RuleFor(command => command.VerticalType)
            .Must(value => Enum.TryParse<VerticalType>(value, ignoreCase: true, out _))
            .WithMessage("vertical invalido (Food, Pharmacy, Grocery o Parcel).");
    }
}
