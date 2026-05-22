using System.Globalization;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.Pricing.Domain.Errors;

namespace Rappix.Pricing.Domain.Common;

/// <summary>Value object: porcentaje en el rango [0, 100] con 2 decimales (ITBIS, service fee, descuentos %).</summary>
public sealed record Percentage
{
    private Percentage(decimal value) => Value = value;

    /// <summary>Cero por ciento.</summary>
    public static readonly Percentage Zero = new(0m);

    /// <summary>Valor del porcentaje (0-100).</summary>
    public decimal Value { get; }

    /// <summary>Crea un porcentaje validando el rango [0, 100] y redondeando a 2 decimales.</summary>
    public static Result<Percentage> Create(decimal value) =>
        value is < 0m or > 100m
            ? Result.Failure<Percentage>(PercentageErrors.OutOfRange)
            : new Percentage(decimal.Round(value, 2));

    /// <summary>Reconstituye un porcentaje ya validado (uso interno y de configuracion).</summary>
    public static Percentage FromTrusted(decimal value) => new(value);

    /// <summary>Fraccion equivalente (p. ej. 18 -> 0.18).</summary>
    public decimal AsFraction => Value / 100m;

    /// <inheritdoc />
    public override string ToString() => Value.ToString("0.00", CultureInfo.InvariantCulture);
}
