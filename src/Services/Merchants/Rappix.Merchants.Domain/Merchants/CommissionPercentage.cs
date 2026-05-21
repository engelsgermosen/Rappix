using System.Globalization;
using Rappix.BuildingBlocks.Core.Results;

namespace Rappix.Merchants.Domain.Merchants;

/// <summary>Value object: porcentaje de comision negociado (0-100, 2 decimales).</summary>
public sealed record CommissionPercentage
{
    private CommissionPercentage(decimal value) => Value = value;

    /// <summary>Comision por defecto al crear un merchant.</summary>
    public static readonly CommissionPercentage Default = new(15.00m);

    /// <summary>Valor del porcentaje.</summary>
    public decimal Value { get; }

    /// <summary>Crea una comision validando el rango [0, 100] y redondeando a 2 decimales.</summary>
    public static Result<CommissionPercentage> Create(decimal value)
    {
        if (value is < 0m or > 100m)
        {
            return CommissionErrors.OutOfRange;
        }

        return new CommissionPercentage(decimal.Round(value, 2));
    }

    /// <summary>Reconstituye una comision ya validada (uso interno de EF Core).</summary>
    public static CommissionPercentage FromTrusted(decimal value) => new(value);

    /// <inheritdoc />
    public override string ToString() => Value.ToString("0.00", CultureInfo.InvariantCulture);
}
