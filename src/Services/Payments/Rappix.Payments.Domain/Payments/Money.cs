using Rappix.BuildingBlocks.Core.Results;

namespace Rappix.Payments.Domain.Payments;

/// <summary>
/// Monto + moneda ISO 4217 para cobros. A diferencia del <c>Money</c> de Pricing (que ofrece
/// aritmetica para el motor de cotizacion), aqui solo se valida y se persiste: Payments no compone
/// montos, solo los pasa al gateway y los compara. Persistido como <c>decimal(19,4)</c> (convencion
/// global del repo aplicada en <c>ConfigureConventions</c>).
/// </summary>
public sealed record Money
{
    private Money(decimal amount, string currency)
    {
        Amount = amount;
        Currency = currency;
    }

    /// <summary>Monto a cobrar. Estrictamente positivo (no se cobran montos cero o negativos).</summary>
    public decimal Amount { get; init; }

    /// <summary>Codigo de moneda ISO 4217 (3 letras ASCII mayusculas, e.g. "DOP", "USD").</summary>
    public string Currency { get; init; } = null!;

    /// <summary>Crea un <see cref="Money"/> validando monto positivo y moneda con 3 letras ASCII.</summary>
    public static Result<Money> Create(decimal amount, string currency)
    {
        if (amount <= 0m)
        {
            return Result.Failure<Money>(MoneyErrors.NonPositive);
        }

        if (string.IsNullOrWhiteSpace(currency))
        {
            return Result.Failure<Money>(MoneyErrors.InvalidCurrency);
        }

        string normalized = currency.Trim().ToUpperInvariant();
        return normalized.Length != 3 || !normalized.All(char.IsAsciiLetterUpper)
            ? Result.Failure<Money>(MoneyErrors.InvalidCurrency)
            : new Money(amount, normalized);
    }

    /// <summary>Reconstituye desde valores ya validados (uso de EF al materializar <c>OwnsOne</c>).</summary>
    public static Money FromTrusted(decimal amount, string currency) => new(amount, currency);
}
