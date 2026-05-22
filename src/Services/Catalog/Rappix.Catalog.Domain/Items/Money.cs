using Rappix.BuildingBlocks.Core.Results;

namespace Rappix.Catalog.Domain.Items;

/// <summary>
/// Value object de dinero (monto + moneda ISO 4217). Tipo de referencia para mapearse con OwnsOne
/// a dos columnas de la tabla de items. El monto se redondea a 2 decimales.
/// </summary>
public sealed record Money
{
    /// <summary>Moneda por defecto (peso dominicano).</summary>
    public const string DefaultCurrency = "DOP";

    private Money(decimal amount, string currency)
    {
        Amount = amount;
        Currency = currency;
    }

    /// <summary>Monto.</summary>
    public decimal Amount { get; private set; }

    /// <summary>Codigo de moneda ISO 4217 (3 letras mayusculas).</summary>
    public string Currency { get; private set; } = null!;

    /// <summary>Crea un monto validando que no sea negativo y que la moneda sea un codigo de 3 letras.</summary>
    public static Result<Money> Create(decimal amount, string? currency = DefaultCurrency)
    {
        if (amount < 0)
        {
            return Result.Failure<Money>(MoneyErrors.NegativeAmount);
        }

        string code = string.IsNullOrWhiteSpace(currency)
            ? DefaultCurrency
            : currency.Trim().ToUpperInvariant();

        return code.Length != 3 || !code.All(char.IsAsciiLetterUpper)
            ? Result.Failure<Money>(MoneyErrors.InvalidCurrency)
            : new Money(decimal.Round(amount, 2), code);
    }

    /// <summary>Reconstituye un monto ya validado (uso interno de EF Core).</summary>
    public static Money FromTrusted(decimal amount, string currency) => new(amount, currency);

    /// <inheritdoc />
    public override string ToString() => $"{Amount:0.00} {Currency}";
}
