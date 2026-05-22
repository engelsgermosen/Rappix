using System.Globalization;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.Pricing.Domain.Errors;

namespace Rappix.Pricing.Domain.Common;

/// <summary>
/// Value object de dinero (monto + moneda ISO 4217). Tipo de trabajo del motor de calculo: ofrece
/// aritmetica segura en moneda (suma, resta, multiplicacion por escalar) sin redondear en cada paso.
/// El redondeo a la unidad menor (centavos) lo aplica el QuoteCalculator una sola vez al final
/// (politica de redondeo bancario documentada en ADR-0005). Nunca usar float/double para dinero.
/// </summary>
public sealed record Money
{
    /// <summary>Moneda por defecto (peso dominicano).</summary>
    public const string DefaultCurrency = "DOP";

    /// <summary>Decimales de la unidad menor de la moneda (centavos).</summary>
    public const int MinorUnitDecimals = 2;

    private Money(decimal amount, string currency)
    {
        Amount = amount;
        Currency = currency;
    }

    /// <summary>Monto (precision completa; el redondeo final es responsabilidad del calculador).</summary>
    public decimal Amount { get; }

    /// <summary>Codigo de moneda ISO 4217 (3 letras mayusculas).</summary>
    public string Currency { get; } = null!;

    /// <summary>Crea un monto validando que no sea negativo y que la moneda sea un codigo de 3 letras.</summary>
    public static Result<Money> Create(decimal amount, string? currency = DefaultCurrency)
    {
        if (amount < 0m)
        {
            return Result.Failure<Money>(MoneyErrors.NegativeAmount);
        }

        string code = string.IsNullOrWhiteSpace(currency)
            ? DefaultCurrency
            : currency.Trim().ToUpperInvariant();

        return code.Length != 3 || !code.All(char.IsAsciiLetterUpper)
            ? Result.Failure<Money>(MoneyErrors.InvalidCurrency)
            : new Money(amount, code);
    }

    /// <summary>Cero en la moneda indicada.</summary>
    public static Money Zero(string currency = DefaultCurrency) => new(0m, currency);

    /// <summary>Reconstituye un monto ya validado (uso interno y del calculador).</summary>
    public static Money FromTrusted(decimal amount, string currency) => new(amount, currency);

    /// <summary>Suma dos montos de la misma moneda.</summary>
    public Money Add(Money other)
    {
        EnsureSameCurrency(other);
        return new Money(Amount + other.Amount, Currency);
    }

    /// <summary>Resta otro monto (se asume no negativo en el dominio de pricing).</summary>
    public Money Subtract(Money other)
    {
        EnsureSameCurrency(other);
        return new Money(Amount - other.Amount, Currency);
    }

    /// <summary>Multiplica por un escalar (cantidad, multiplicador de surge, etc.).</summary>
    public Money Multiply(decimal factor) => new(Amount * factor, Currency);

    /// <summary>Aplica un porcentaje (0-100) sobre el monto.</summary>
    public Money Percent(decimal percentage) => new(Amount * percentage / 100m, Currency);

    /// <summary>Redondea a la unidad menor (2 decimales) con redondeo bancario (ToEven).</summary>
    public Money RoundToMinorUnit() =>
        new(decimal.Round(Amount, MinorUnitDecimals, MidpointRounding.ToEven), Currency);

    /// <summary>Indica si el monto es cero o negativo.</summary>
    public bool IsZeroOrLess => Amount <= 0m;

    private void EnsureSameCurrency(Money other)
    {
        if (!string.Equals(Currency, other.Currency, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"No se pueden operar montos de monedas distintas ({Currency} vs {other.Currency}).");
        }
    }

    /// <inheritdoc />
    public override string ToString() => $"{Amount.ToString("0.00", CultureInfo.InvariantCulture)} {Currency}";
}
