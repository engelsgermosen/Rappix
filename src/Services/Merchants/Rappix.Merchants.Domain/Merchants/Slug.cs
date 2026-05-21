using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using Rappix.BuildingBlocks.Core.Results;

namespace Rappix.Merchants.Domain.Merchants;

/// <summary>Value object: identificador legible unico (minusculas, numeros y guiones).</summary>
public sealed partial record Slug
{
    private Slug(string value) => Value = value;

    /// <summary>Valor normalizado del slug.</summary>
    public string Value { get; }

    /// <summary>Crea un slug validando formato (3-60 chars, lowercase-dash) y normalizando.</summary>
    [SuppressMessage(
        "Globalization",
        "CA1308:Normalize strings to uppercase",
        Justification = "Los slugs se normalizan a minusculas por convencion de URL.")]
    public static Result<Slug> Create(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return SlugErrors.Empty;
        }

        string normalized = input.Trim().ToLowerInvariant();
        if (normalized.Length is < 3 or > 60 || !SlugRegex().IsMatch(normalized))
        {
            return SlugErrors.Invalid;
        }

        return new Slug(normalized);
    }

    /// <summary>Reconstituye un slug ya validado (uso interno de EF Core).</summary>
    public static Slug FromTrusted(string value) => new(value);

    /// <summary>Genera un slug placeholder unico por usuario (para el Draft creado por el consumer).</summary>
    public static Slug CreatePlaceholder(Guid ownerUserId) => new($"mi-negocio-{ownerUserId:N}");

    /// <inheritdoc />
    public override string ToString() => Value;

    [GeneratedRegex("^[a-z0-9]+(?:-[a-z0-9]+)*$")]
    private static partial Regex SlugRegex();
}
