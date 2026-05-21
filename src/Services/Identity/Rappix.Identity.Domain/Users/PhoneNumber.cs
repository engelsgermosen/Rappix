using System.Text.RegularExpressions;
using Rappix.BuildingBlocks.Core.Results;

namespace Rappix.Identity.Domain.Users;

/// <summary>Value object que representa un telefono normalizado en formato E.164.</summary>
public sealed partial record PhoneNumber
{
    private PhoneNumber(string value) => Value = value;

    /// <summary>Telefono normalizado (digitos con prefijo + opcional).</summary>
    public string Value { get; }

    /// <summary>Crea un telefono validando formato y quitando espacios, guiones y parentesis.</summary>
    public static Result<PhoneNumber> Create(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return PhoneErrors.Empty;
        }

        var normalized = SeparatorChars().Replace(input.Trim(), string.Empty);
        if (!E164Format().IsMatch(normalized))
        {
            return PhoneErrors.Invalid;
        }

        return new PhoneNumber(normalized);
    }

    /// <summary>Reconstituye un telefono ya validado (uso interno de EF Core al materializar desde la BD).</summary>
    public static PhoneNumber FromTrusted(string value) => new(value);

    /// <inheritdoc />
    public override string ToString() => Value;

    [GeneratedRegex(@"[\s\-()]")]
    private static partial Regex SeparatorChars();

    [GeneratedRegex(@"^\+?[1-9]\d{7,14}$")]
    private static partial Regex E164Format();
}
