using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using Rappix.BuildingBlocks.Core.Results;

namespace Rappix.Identity.Domain.Users;

/// <summary>Value object que representa un email valido y normalizado en minusculas.</summary>
public sealed partial record Email
{
    private Email(string value) => Value = value;

    /// <summary>Email normalizado.</summary>
    public string Value { get; }

    /// <summary>Crea un email validando formato y normalizando a minusculas.</summary>
    [SuppressMessage(
        "Globalization",
        "CA1308:Normalize strings to uppercase",
        Justification = "Los emails se normalizan a minusculas por convencion para garantizar unicidad.")]
    public static Result<Email> Create(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return EmailErrors.Empty;
        }

        var normalized = input.Trim().ToLowerInvariant();
        if (normalized.Length > 256 || !EmailRegex().IsMatch(normalized))
        {
            return EmailErrors.Invalid;
        }

        return new Email(normalized);
    }

    /// <summary>Reconstituye un email ya validado (uso interno de EF Core al materializar desde la BD).</summary>
    public static Email FromTrusted(string value) => new(value);

    /// <inheritdoc />
    public override string ToString() => Value;

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
    private static partial Regex EmailRegex();
}
