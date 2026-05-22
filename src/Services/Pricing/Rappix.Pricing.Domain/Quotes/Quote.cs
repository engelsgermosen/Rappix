using Rappix.BuildingBlocks.Core.Domain;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.Pricing.Domain.Abstractions;
using Rappix.Pricing.Domain.Common;
using Rappix.Pricing.Domain.Coupons;
using Rappix.Pricing.Domain.Errors;
using Rappix.Pricing.Domain.Quotes.Events;

namespace Rappix.Pricing.Domain.Quotes;

/// <summary>
/// Agregado raiz de una cotizacion: el precio total de un pedido potencial, desglosado y persistido con
/// expiracion. La crea el cliente al armar el carrito; un pedido (futuro servicio Orders) la consume por
/// su id. Inmutable salvo por la transicion de estado (Active -> Consumed | Expired). Las cotizaciones no
/// se borran (auditoria); expiran de forma perezosa al leerse vencidas.
/// </summary>
public sealed class Quote : AggregateRoot<QuoteId>, IHasDomainEvents
{
    private readonly List<QuoteLine> _lines = [];

    private Quote()
    {
    }

    private Quote(
        QuoteId id,
        Guid customerUserId,
        Guid merchantId,
        VerticalType vertical,
        string currency,
        PriceBreakdown breakdown,
        CouponId? appliedCouponId,
        string? appliedCouponCode,
        DateTime createdAtUtc,
        DateTime expiresAtUtc)
        : base(id)
    {
        CustomerUserId = customerUserId;
        MerchantId = merchantId;
        Vertical = vertical;
        Currency = currency;
        Breakdown = breakdown;
        AppliedCouponId = appliedCouponId;
        AppliedCouponCode = appliedCouponCode;
        CreatedAtUtc = createdAtUtc;
        ExpiresAtUtc = expiresAtUtc;
        Status = QuoteStatus.Active;
    }

    /// <summary>Cliente para quien se cotizo (sujeto del JWT).</summary>
    public Guid CustomerUserId { get; private set; }

    /// <summary>Merchant del pedido cotizado.</summary>
    public Guid MerchantId { get; private set; }

    /// <summary>Vertical del pedido (afecta tarifa de envio y surge).</summary>
    public VerticalType Vertical { get; private set; }

    /// <summary>Moneda de todos los montos de la cotizacion (ISO 4217).</summary>
    public string Currency { get; private set; } = null!;

    /// <summary>Desglose monetario completo (value object embebido).</summary>
    public PriceBreakdown Breakdown { get; private set; } = null!;

    /// <summary>Cupon aplicado, si lo hubo (su uso se redime al consumir la cotizacion).</summary>
    public CouponId? AppliedCouponId { get; private set; }

    /// <summary>Codigo del cupon aplicado (snapshot legible).</summary>
    public string? AppliedCouponCode { get; private set; }

    /// <summary>Momento de creacion (UTC).</summary>
    public DateTime CreatedAtUtc { get; private set; }

    /// <summary>Momento de expiracion (UTC); por defecto 10 minutos desde la creacion.</summary>
    public DateTime ExpiresAtUtc { get; private set; }

    /// <summary>Estado del ciclo de vida.</summary>
    public QuoteStatus Status { get; private set; }

    /// <summary>Momento de consumo (UTC), si fue consumida.</summary>
    public DateTime? ConsumedAtUtc { get; private set; }

    /// <summary>Pedido que consumio la cotizacion (para idempotencia de consumo y reversion de la saga).</summary>
    public Guid? ConsumedByOrderId { get; private set; }

    /// <summary>Momento de la ultima reversion del consumo (UTC), si la saga compenso. Audita el revert.</summary>
    public DateTime? RevertedAtUtc { get; private set; }

    /// <summary>Lineas del carrito cotizado.</summary>
    public IReadOnlyCollection<QuoteLine> Lines => _lines.AsReadOnly();

    /// <summary>Crea una cotizacion vigente con su desglose y ventana de expiracion. Eleva <see cref="QuoteCreatedDomainEvent"/>.</summary>
    public static Result<Quote> Create(
        Guid customerUserId,
        Guid merchantId,
        VerticalType vertical,
        string currency,
        IReadOnlyList<QuoteLine> lines,
        PriceBreakdown breakdown,
        CouponId? appliedCouponId,
        string? appliedCouponCode,
        DateTime createdAtUtc,
        int expiryMinutes)
    {
        if (lines.Count == 0)
        {
            return Result.Failure<Quote>(QuoteErrors.NoLines);
        }

        if (expiryMinutes <= 0)
        {
            return Result.Failure<Quote>(QuoteErrors.InvalidExpiration);
        }

        Result<Money> currencyCheck = Money.Create(0m, currency);
        if (currencyCheck.IsFailure)
        {
            return Result.Failure<Quote>(currencyCheck.Error);
        }

        DateTime expiresAtUtc = createdAtUtc.AddMinutes(expiryMinutes);

        var quote = new Quote(
            QuoteId.New(),
            customerUserId,
            merchantId,
            vertical,
            currencyCheck.Value.Currency,
            breakdown,
            appliedCouponId,
            appliedCouponCode,
            createdAtUtc,
            expiresAtUtc);
        quote._lines.AddRange(lines);

        quote.RaiseDomainEvent(new QuoteCreatedDomainEvent(
            quote.Id, customerUserId, merchantId, breakdown.Total, quote.Currency, expiresAtUtc));

        return quote;
    }

    /// <summary>Indica si la cotizacion esta vigente pero ya vencio por tiempo (necesita marcarse expirada).</summary>
    public bool IsExpiredAt(DateTime utcNow) => Status == QuoteStatus.Active && utcNow >= ExpiresAtUtc;

    /// <summary>Marca la cotizacion como expirada (expiracion perezosa al leerse vencida).</summary>
    public void MarkExpired()
    {
        if (Status == QuoteStatus.Active)
        {
            Status = QuoteStatus.Expired;
        }
    }

    /// <summary>
    /// Consume la cotizacion (la referencia un pedido). Idempotente por pedido: si ya fue consumida por el
    /// MISMO pedido devuelve exito sin cambios (reintento seguro de la saga); por OTRO pedido falla con
    /// AlreadyConsumed. Falla si expiro (y la marca expirada). Transicion Active -> Consumed.
    /// </summary>
    public Result Consume(DateTime utcNow, Guid orderId)
    {
        if (Status == QuoteStatus.Consumed)
        {
            return ConsumedByOrderId == orderId
                ? Result.Success()
                : Result.Failure(QuoteErrors.AlreadyConsumed);
        }

        if (Status == QuoteStatus.Expired || utcNow >= ExpiresAtUtc)
        {
            Status = QuoteStatus.Expired;
            return Result.Failure(QuoteErrors.Expired);
        }

        Status = QuoteStatus.Consumed;
        ConsumedAtUtc = utcNow;
        ConsumedByOrderId = orderId;
        RevertedAtUtc = null;
        return Result.Success();
    }

    /// <summary>
    /// Revierte el consumo (compensacion de la saga): devuelve la cotizacion a Active. Solo es valido sobre
    /// una cotizacion consumida por ESTE pedido; sobre cualquier otro estado (Active sin consumir, consumida
    /// por otro pedido, Expired) devuelve un error tipado. Idempotente: re-revertir por el mismo pedido es
    /// un no-op exitoso.
    /// </summary>
    public Result Revert(Guid orderId, DateTime utcNow)
    {
        // Idempotente: ya revertida por este pedido (Active tras un revert previo de este pedido).
        if (Status == QuoteStatus.Active && ConsumedByOrderId == orderId && RevertedAtUtc is not null)
        {
            return Result.Success();
        }

        if (Status != QuoteStatus.Consumed || ConsumedByOrderId != orderId)
        {
            return Result.Failure(QuoteErrors.RevertNotAllowed);
        }

        Status = QuoteStatus.Active;
        ConsumedAtUtc = null;
        RevertedAtUtc = utcNow;
        return Result.Success();
    }
}
