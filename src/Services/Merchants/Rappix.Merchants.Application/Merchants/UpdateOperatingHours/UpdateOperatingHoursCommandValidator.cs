using System.Globalization;
using FluentValidation;

namespace Rappix.Merchants.Application.Merchants.UpdateOperatingHours;

/// <summary>Validacion del comando de actualizacion de horarios.</summary>
internal sealed class UpdateOperatingHoursCommandValidator : AbstractValidator<UpdateOperatingHoursCommand>
{
    public UpdateOperatingHoursCommandValidator()
    {
        RuleForEach(command => command.Hours).ChildRules(hours =>
        {
            hours.RuleFor(range => range.DayOfWeek)
                .Must(value => Enum.TryParse<DayOfWeek>(value, ignoreCase: true, out _))
                .WithMessage("dia invalido (Monday..Sunday).");

            hours.RuleFor(range => range.OpensAt).Must(BeTime).WithMessage("apertura invalida (HH:mm).");
            hours.RuleFor(range => range.ClosesAt).Must(BeTime).WithMessage("cierre invalido (HH:mm).");
        });
    }

    private static bool BeTime(string value) =>
        TimeOnly.TryParse(value, CultureInfo.InvariantCulture, out _);
}
