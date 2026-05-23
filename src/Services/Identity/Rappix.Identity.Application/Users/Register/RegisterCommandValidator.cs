using FluentValidation;

namespace Rappix.Identity.Application.Users.Register;

/// <summary>Validacion del comando de registro.</summary>
internal sealed class RegisterCommandValidator : AbstractValidator<RegisterCommand>
{
    public RegisterCommandValidator()
    {
        RuleFor(command => command.Email)
            .NotEmpty()
            .MaximumLength(256);

        RuleFor(command => command.Password)
            .NotEmpty()
            .MinimumLength(8)
            .MaximumLength(128)
            .Matches("[A-Za-z]").WithMessage("La contrasena debe contener al menos una letra.")
            .Matches("[0-9]").WithMessage("La contrasena debe contener al menos un numero.");

        RuleFor(command => command.FirstName)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(command => command.LastName)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(command => command.AccountType)
            .Must(BeWhitelisted)
            .When(command => command.AccountType is not null)
            .WithMessage("accountType debe ser 'Customer', 'Merchant' o 'Courier'.");
    }

    // Whitelist explicito: Admin queda fuera del self-registration (lo escala el operador).
    // Tipos validos: Customer (default), Merchant, Courier.
    private static bool BeWhitelisted(string? accountType) =>
        string.Equals(accountType, "Customer", StringComparison.OrdinalIgnoreCase)
        || string.Equals(accountType, "Merchant", StringComparison.OrdinalIgnoreCase)
        || string.Equals(accountType, "Courier", StringComparison.OrdinalIgnoreCase);
}
