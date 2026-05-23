using FluentValidation;

namespace Rappix.Merchants.Application.Merchants.SetPickupLocation;

/// <summary>Validacion del comando: lat/lng en rangos validos.</summary>
internal sealed class SetPickupLocationCommandValidator : AbstractValidator<SetPickupLocationCommand>
{
    public SetPickupLocationCommandValidator()
    {
        RuleFor(command => command.Latitude).InclusiveBetween(-90, 90);
        RuleFor(command => command.Longitude).InclusiveBetween(-180, 180);
    }
}
