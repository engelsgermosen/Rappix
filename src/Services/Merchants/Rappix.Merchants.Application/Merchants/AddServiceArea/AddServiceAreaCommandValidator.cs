using FluentValidation;
using Rappix.Merchants.Domain.Merchants;

namespace Rappix.Merchants.Application.Merchants.AddServiceArea;

/// <summary>Validacion del comando de alta de zona de cobertura.</summary>
internal sealed class AddServiceAreaCommandValidator : AbstractValidator<AddServiceAreaCommand>
{
    public AddServiceAreaCommandValidator()
    {
        RuleFor(command => command.Type)
            .Must(value => Enum.TryParse<ServiceAreaType>(value, ignoreCase: true, out _))
            .WithMessage("type debe ser 'Polygon' o 'Circle'.");

        When(IsPolygon, () =>
        {
            RuleFor(command => command.Polygon)
                .NotNull()
                .Must(ring => ring is { Count: >= 3 } && ring.All(point => point.Length == 2))
                .WithMessage("polygon requiere al menos 3 puntos [longitud, latitud].");
        });

        When(IsCircle, () =>
        {
            RuleFor(command => command.CenterLatitude).NotNull().InclusiveBetween(-90, 90);
            RuleFor(command => command.CenterLongitude).NotNull().InclusiveBetween(-180, 180);
            RuleFor(command => command.RadiusMeters).NotNull().InclusiveBetween(100, 50000);
        });
    }

    private static bool IsPolygon(AddServiceAreaCommand command) =>
        string.Equals(command.Type, nameof(ServiceAreaType.Polygon), StringComparison.OrdinalIgnoreCase);

    private static bool IsCircle(AddServiceAreaCommand command) =>
        string.Equals(command.Type, nameof(ServiceAreaType.Circle), StringComparison.OrdinalIgnoreCase);
}
