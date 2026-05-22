using Rappix.BuildingBlocks.Core.Domain;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.Pricing.Domain.Abstractions;
using Rappix.Pricing.Domain.Coupons.Events;
using Rappix.Pricing.Domain.Errors;
using Rappix.Pricing.Domain.Quotes;

namespace Rappix.Pricing.Domain.Coupons;

/// <summary>
/// Agregado raiz de un cupon de descuento. El uso (UsedCount) se incrementa cuando una cotizacion que lo
/// aplico se CONSUME (no al cotizar: evita quemar usos en cotizaciones abandonadas), con concurrencia
/// optimista (xmin) para no exceder MaxUses ante consumos concurrentes. Borrado logico (query filter).
/// </summary>
public sealed class Coupon : AggregateRoot<CouponId>, IHasDomainEvents
{
    private Coupon()
    {
    }

    private Coupon(
        CouponId id,
        CouponCode code,
        DiscountType discountType,
        decimal value,
        int? maxUses,
        decimal? minOrderAmount,
        DateTime validFromUtc,
        DateTime validUntilUtc,
        int? perUserLimit,
        Guid? merchantId,
        DateTime utcNow)
        : base(id)
    {
        Code = code;
        DiscountType = discountType;
        Value = value;
        MaxUses = maxUses;
        UsedCount = 0;
        MinOrderAmount = minOrderAmount;
        ValidFromUtc = validFromUtc;
        ValidUntilUtc = validUntilUtc;
        PerUserLimit = perUserLimit;
        MerchantId = merchantId;
        IsActive = true;
        IsDeleted = false;
        CreatedAtUtc = utcNow;
    }

    /// <summary>Codigo unico del cupon (normalizado en mayusculas).</summary>
    public CouponCode Code { get; private set; } = null!;

    /// <summary>Tipo de descuento (porcentaje o monto fijo).</summary>
    public DiscountType DiscountType { get; private set; }

    /// <summary>Valor del descuento (porcentaje 0-100 o monto fijo, segun el tipo).</summary>
    public decimal Value { get; private set; }

    /// <summary>Maximo de usos global (null = ilimitado).</summary>
    public int? MaxUses { get; private set; }

    /// <summary>Usos consumidos hasta ahora.</summary>
    public int UsedCount { get; private set; }

    /// <summary>Monto minimo de pedido para aplicar (null = sin minimo).</summary>
    public decimal? MinOrderAmount { get; private set; }

    /// <summary>Inicio de vigencia (UTC).</summary>
    public DateTime ValidFromUtc { get; private set; }

    /// <summary>Fin de vigencia (UTC, exclusivo).</summary>
    public DateTime ValidUntilUtc { get; private set; }

    /// <summary>Maximo de usos por usuario (null = sin limite por usuario).</summary>
    public int? PerUserLimit { get; private set; }

    /// <summary>Merchant al que aplica el cupon (null = global, todos los merchants).</summary>
    public Guid? MerchantId { get; private set; }

    /// <summary>Indica si el cupon esta activo.</summary>
    public bool IsActive { get; private set; }

    /// <summary>Borrado logico (desactivacion definitiva).</summary>
    public bool IsDeleted { get; private set; }

    /// <summary>Momento de creacion (UTC).</summary>
    public DateTime CreatedAtUtc { get; private set; }

    /// <summary>Momento de la ultima actualizacion (UTC).</summary>
    public DateTime? UpdatedAtUtc { get; private set; }

    /// <summary>Crea un cupon validando codigo, valor segun tipo, vigencia y limites.</summary>
    public static Result<Coupon> Create(
        string? code,
        DiscountType discountType,
        decimal value,
        int? maxUses,
        decimal? minOrderAmount,
        DateTime validFromUtc,
        DateTime validUntilUtc,
        int? perUserLimit,
        Guid? merchantId,
        DateTime utcNow)
    {
        Result<CouponCode> couponCode = CouponCode.Create(code);
        if (couponCode.IsFailure)
        {
            return Result.Failure<Coupon>(couponCode.Error);
        }

        Result invariants = ValidateInvariants(discountType, value, maxUses, minOrderAmount, validFromUtc, validUntilUtc, perUserLimit);
        if (invariants.IsFailure)
        {
            return Result.Failure<Coupon>(invariants.Error);
        }

        return new Coupon(
            CouponId.New(), couponCode.Value, discountType, value, maxUses,
            minOrderAmount, validFromUtc, validUntilUtc, perUserLimit, merchantId, utcNow);
    }

    /// <summary>Actualiza los campos editables del cupon (el codigo es inmutable).</summary>
    public Result Update(
        DiscountType discountType,
        decimal value,
        int? maxUses,
        decimal? minOrderAmount,
        DateTime validFromUtc,
        DateTime validUntilUtc,
        int? perUserLimit,
        Guid? merchantId,
        bool isActive,
        DateTime utcNow)
    {
        Result invariants = ValidateInvariants(discountType, value, maxUses, minOrderAmount, validFromUtc, validUntilUtc, perUserLimit);
        if (invariants.IsFailure)
        {
            return invariants;
        }

        DiscountType = discountType;
        Value = value;
        MaxUses = maxUses;
        MinOrderAmount = minOrderAmount;
        ValidFromUtc = validFromUtc;
        ValidUntilUtc = validUntilUtc;
        PerUserLimit = perUserLimit;
        MerchantId = merchantId;
        IsActive = isActive;
        UpdatedAtUtc = utcNow;
        return Result.Success();
    }

    /// <summary>Desactiva el cupon (borrado logico). Idempotente.</summary>
    public void Deactivate(DateTime utcNow)
    {
        IsActive = false;
        IsDeleted = true;
        UpdatedAtUtc = utcNow;
    }

    /// <summary>Indica si el cupon aplica a un merchant dado (global o coincidente).</summary>
    public bool AppliesToMerchant(Guid merchantId) => MerchantId is null || MerchantId == merchantId;

    /// <summary>
    /// Valida que el cupon sea usable para un pedido: activo, vigente, sin exceder MaxUses, cumpliendo el
    /// monto minimo y el limite por usuario. El match de merchant se valida aparte (lo conoce el llamador).
    /// </summary>
    public Result ValidateUsable(decimal orderAmount, DateTime utcNow, int userRedemptionCount)
    {
        if (!IsActive || IsDeleted)
        {
            return Result.Failure(CouponErrors.Inactive);
        }

        if (utcNow < ValidFromUtc)
        {
            return Result.Failure(CouponErrors.NotYetValid);
        }

        if (utcNow >= ValidUntilUtc)
        {
            return Result.Failure(CouponErrors.Expired);
        }

        if (MaxUses is { } max && UsedCount >= max)
        {
            return Result.Failure(CouponErrors.MaxUsesReached);
        }

        if (MinOrderAmount is { } min && orderAmount < min)
        {
            return Result.Failure(CouponErrors.MinOrderNotMet);
        }

        return PerUserLimit is { } limit && userRedemptionCount >= limit
            ? Result.Failure(CouponErrors.PerUserLimitReached)
            : Result.Success();
    }

    /// <summary>Calcula el monto de descuento sobre una base, sin exceder la propia base.</summary>
    public decimal CalculateDiscount(decimal baseAmount)
    {
        if (baseAmount <= 0m)
        {
            return 0m;
        }

        decimal discount = DiscountType == DiscountType.Percentage
            ? baseAmount * Value / 100m
            : Value;

        return Math.Min(discount, baseAmount);
    }

    /// <summary>
    /// Redime un uso del cupon al consumir una cotizacion. Falla si ya alcanzo MaxUses (el token xmin
    /// evita exceder el limite ante consumos concurrentes). Eleva <see cref="CouponRedeemedDomainEvent"/>.
    /// </summary>
    public Result Redeem(QuoteId quoteId, Guid customerUserId, decimal discountAmount, DateTime utcNow)
    {
        if (MaxUses is { } max && UsedCount >= max)
        {
            return Result.Failure(CouponErrors.MaxUsesReached);
        }

        UsedCount++;
        UpdatedAtUtc = utcNow;
        RaiseDomainEvent(new CouponRedeemedDomainEvent(Id, Code.Value, customerUserId, quoteId, discountAmount));
        return Result.Success();
    }

    private static Result ValidateInvariants(
        DiscountType discountType,
        decimal value,
        int? maxUses,
        decimal? minOrderAmount,
        DateTime validFromUtc,
        DateTime validUntilUtc,
        int? perUserLimit)
    {
        bool invalidValue = discountType == DiscountType.Percentage
            ? value is <= 0m or > 100m
            : value <= 0m;
        if (invalidValue)
        {
            return Result.Failure(CouponErrors.InvalidValue);
        }

        if (validFromUtc >= validUntilUtc)
        {
            return Result.Failure(CouponErrors.InvalidDateRange);
        }

        if (maxUses is <= 0)
        {
            return Result.Failure(CouponErrors.InvalidMaxUses);
        }

        if (perUserLimit is <= 0)
        {
            return Result.Failure(CouponErrors.InvalidPerUserLimit);
        }

        return minOrderAmount is < 0m
            ? Result.Failure(CouponErrors.InvalidMinOrderAmount)
            : Result.Success();
    }
}
