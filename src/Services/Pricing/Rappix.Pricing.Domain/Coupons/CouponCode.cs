using Rappix.BuildingBlocks.Core.Results;
using Rappix.Pricing.Domain.Errors;

namespace Rappix.Pricing.Domain.Coupons;

/// <summary>Value object: codigo de cupon normalizado (mayusculas, sin espacios laterales, 3-40 caracteres).</summary>
public sealed record CouponCode
{
    /// <summary>Largo maximo del codigo.</summary>
    public const int MaxLength = 40;

    /// <summary>Largo minimo del codigo.</summary>
    public const int MinLength = 3;

    private CouponCode(string value) => Value = value;

    /// <summary>Texto del codigo (normalizado).</summary>
    public string Value { get; } = null!;

    /// <summary>Crea un codigo normalizando a mayusculas y validando el largo.</summary>
    public static Result<CouponCode> Create(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return Result.Failure<CouponCode>(CouponErrors.CodeRequired);
        }

        string normalized = code.Trim().ToUpperInvariant();

        if (normalized.Length < MinLength)
        {
            return Result.Failure<CouponCode>(CouponErrors.CodeRequired);
        }

        return normalized.Length > MaxLength
            ? Result.Failure<CouponCode>(CouponErrors.CodeTooLong)
            : new CouponCode(normalized);
    }

    /// <summary>Reconstituye un codigo ya validado (uso interno de EF Core).</summary>
    public static CouponCode FromTrusted(string value) => new(value);

    /// <inheritdoc />
    public override string ToString() => Value;
}
