using System.Text.RegularExpressions;
using Rappix.BuildingBlocks.Core.Results;

namespace Rappix.Merchants.Domain.Merchants;

/// <summary>Value object: RNC dominicano (9 digitos empresa, u 11 de cedula).</summary>
public sealed partial record Rnc
{
    private Rnc(string value) => Value = value;

    /// <summary>Solo digitos.</summary>
    public string Value { get; }

    /// <summary>Crea un RNC quitando separadores y validando longitud (9 u 11 digitos).</summary>
    public static Result<Rnc> Create(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return RncErrors.Empty;
        }

        string digits = NonDigits().Replace(input, string.Empty);
        if (digits.Length is not 9 and not 11)
        {
            return RncErrors.Invalid;
        }

        return new Rnc(digits);
    }

    /// <summary>Reconstituye un RNC ya validado (uso interno de EF Core).</summary>
    public static Rnc FromTrusted(string value) => new(value);

    /// <inheritdoc />
    public override string ToString() => Value;

    [GeneratedRegex(@"\D")]
    private static partial Regex NonDigits();
}
