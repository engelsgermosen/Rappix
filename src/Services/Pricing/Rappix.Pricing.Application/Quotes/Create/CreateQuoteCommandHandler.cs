using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.BuildingBlocks.Core.Time;
using Rappix.Pricing.Application.Abstractions;
using Rappix.Pricing.Application.Configuration;
using Rappix.Pricing.Application.Pricing.Delivery;
using Rappix.Pricing.Application.Pricing.Discounts;
using Rappix.Pricing.Application.Pricing.Engine;
using Rappix.Pricing.Application.Pricing.Surge;
using Rappix.Pricing.Application.Responses;
using Rappix.Pricing.Domain.Common;
using Rappix.Pricing.Domain.Coupons;
using Rappix.Pricing.Domain.Errors;
using Rappix.Pricing.Domain.ItemPrices;
using Rappix.Pricing.Domain.Quotes;

namespace Rappix.Pricing.Application.Quotes.Create;

/// <summary>
/// Orquesta la cotizacion: valida el merchant (gRPC a Merchants, con fallback), resuelve el precio de cada
/// linea (gRPC a Catalog, con fallback al cache local), resuelve surge y descuentos por estrategias,
/// resuelve el envio por vertical, calcula el desglose con el <see cref="QuoteCalculator"/> y persiste la
/// cotizacion. El orden de calculo y la politica de redondeo viven en el calculador (ADR-0005).
/// </summary>
internal sealed partial class CreateQuoteCommandHandler(
    IMerchantPricingClient merchantClient,
    ICatalogPricingClient catalogClient,
    IItemPriceCacheRepository priceCache,
    ISurgeStrategy surgeStrategy,
    DeliveryFeeResolver deliveryResolver,
    IEnumerable<IDiscountRule> discountRules,
    IQuoteRepository quotes,
    IUnitOfWork unitOfWork,
    IDateTimeProvider clock,
    IOptions<PricingOptions> options,
    ILogger<CreateQuoteCommandHandler> logger)
    : IRequestHandler<CreateQuoteCommand, Result<QuoteResponse>>
{
    public async Task<Result<QuoteResponse>> Handle(CreateQuoteCommand command, CancellationToken cancellationToken)
    {
        PricingOptions config = options.Value;
        DateTime now = clock.UtcNow;
        string currency = config.Currency;

        Result<VerticalType> gating = await ResolveMerchantAsync(command, cancellationToken);
        if (gating.IsFailure)
        {
            return Result.Failure<QuoteResponse>(gating.Error);
        }

        VerticalType vertical = gating.Value;

        Result<List<QuoteLine>> lines = await ResolveLinesAsync(command, currency, cancellationToken);
        if (lines.IsFailure)
        {
            return Result.Failure<QuoteResponse>(lines.Error);
        }

        decimal subtotal = lines.Value.Sum(line => line.LineSubtotal);

        Result<DiscountSelection> discounts = await ResolveDiscountsAsync(command, subtotal, config, now, cancellationToken);
        if (discounts.IsFailure)
        {
            return Result.Failure<QuoteResponse>(discounts.Error);
        }

        decimal surgeMultiplier = await surgeStrategy.ResolveMultiplierAsync(
            new SurgeContext(command.ZoneId, vertical, now), cancellationToken);

        decimal deliveryFee = deliveryResolver.Resolve(vertical, command.DistanceKm);

        var input = new QuoteCalculationInput
        {
            Lines = lines.Value,
            SurgeMultiplier = surgeMultiplier,
            Discounts = discounts.Value.Directives,
            DeliveryFee = deliveryFee,
            ServiceFeePercentage = config.ServiceFeePercentage,
            TaxPercentage = config.TaxPercentage,
            TaxAppliesToDelivery = config.TaxAppliesToDelivery,
            TaxAppliesToServiceFee = config.TaxAppliesToServiceFee,
            Tip = command.Tip,
        };

        PriceBreakdown breakdown = QuoteCalculator.Calculate(input);

        Result<Quote> quote = Quote.Create(
            command.CustomerUserId,
            command.MerchantId,
            vertical,
            currency,
            lines.Value,
            breakdown,
            discounts.Value.CouponId,
            discounts.Value.CouponCode,
            now,
            config.QuoteExpiryMinutes);
        if (quote.IsFailure)
        {
            return Result.Failure<QuoteResponse>(quote.Error);
        }

        quotes.Add(quote.Value);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return QuoteResponse.From(quote.Value);
    }

    private async Task<Result<VerticalType>> ResolveMerchantAsync(CreateQuoteCommand command, CancellationToken cancellationToken)
    {
        MerchantPricingInfo merchant = await merchantClient.GetAsync(command.MerchantId, cancellationToken);

        if (!merchant.ServiceAvailable)
        {
            // Fallback documentado: no bloquear la cotizacion ante un fallo transitorio de Merchants.
            LogMerchantUnavailable(logger, command.MerchantId);
            return command.Vertical;
        }

        if (!merchant.Found)
        {
            return Result.Failure<VerticalType>(QuoteErrors.MerchantNotFound);
        }

        if (!merchant.IsActive)
        {
            return Result.Failure<VerticalType>(QuoteErrors.MerchantInactive);
        }

        return Enum.TryParse(merchant.VerticalType, ignoreCase: true, out VerticalType vertical)
            ? vertical
            : command.Vertical;
    }

    private async Task<Result<List<QuoteLine>>> ResolveLinesAsync(CreateQuoteCommand command, string currency, CancellationToken cancellationToken)
    {
        var lines = new List<QuoteLine>(command.Lines.Count);

        foreach (QuoteLineInput requested in command.Lines)
        {
            Result<LineResolution> resolved = await ResolveLinePriceAsync(requested.ItemId, cancellationToken);
            if (resolved.IsFailure)
            {
                return Result.Failure<List<QuoteLine>>(resolved.Error);
            }

            Result<QuoteLine> line = QuoteLine.Create(
                requested.ItemId, resolved.Value.Name, resolved.Value.UnitPrice, requested.ModifierTotal, requested.Quantity);
            if (line.IsFailure)
            {
                return Result.Failure<List<QuoteLine>>(line.Error);
            }

            lines.Add(line.Value);
        }

        return lines;
    }

    private async Task<Result<LineResolution>> ResolveLinePriceAsync(Guid itemId, CancellationToken cancellationToken)
    {
        CatalogItemPricing pricing = await catalogClient.GetItemPricingAsync(itemId, cancellationToken);

        if (pricing.ServiceAvailable && pricing.Found)
        {
            return pricing.IsPurchasable
                ? new LineResolution(pricing.Name, pricing.PriceAmount)
                : Result.Failure<LineResolution>(QuoteLineErrors.ItemNotPurchasable);
        }

        // Fallback: Catalog no respondio o el item no estaba en su respuesta; usar el cache local de precios.
        ItemPriceCache? cached = await priceCache.GetByItemIdAsync(itemId, cancellationToken);
        if (cached is not null)
        {
            LogPriceFallback(logger, itemId);
            return new LineResolution(cached.Name, cached.BasePrice);
        }

        return Result.Failure<LineResolution>(QuoteLineErrors.PriceUnavailable);
    }

    private async Task<Result<DiscountSelection>> ResolveDiscountsAsync(
        CreateQuoteCommand command,
        decimal subtotal,
        PricingOptions config,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var context = new DiscountRuleContext(
            command.CustomerUserId, command.MerchantId, subtotal, command.IsFirstOrder, command.CouponCode, now);

        var evaluations = new List<DiscountEvaluation>();
        foreach (IDiscountRule rule in discountRules)
        {
            Result<DiscountEvaluation?> evaluation = await rule.EvaluateAsync(context, cancellationToken);
            if (evaluation.IsFailure)
            {
                return Result.Failure<DiscountSelection>(evaluation.Error);
            }

            if (evaluation.Value is not null)
            {
                evaluations.Add(evaluation.Value);
            }
        }

        DiscountEvaluation? coupon = evaluations.Find(evaluation => evaluation.CouponId is not null);

        // Stacking: solo un cupon por cotizacion; el cupon se acumula con primera compra solo si se permite.
        List<DiscountEvaluation> applied = coupon is not null && !config.AllowStackingCouponWithFirstOrder
            ? [coupon]
            : evaluations;

        var directives = applied
            .Select(evaluation => new DiscountDirective(evaluation.Source, evaluation.Type, evaluation.Value))
            .ToList();

        return new DiscountSelection(directives, coupon?.CouponId, coupon?.CouponCode);
    }

    private sealed record LineResolution(string Name, decimal UnitPrice);

    private sealed record DiscountSelection(IReadOnlyList<DiscountDirective> Directives, CouponId? CouponId, string? CouponCode);

    [LoggerMessage(Level = LogLevel.Warning, Message = "El servicio Merchants no respondio para {MerchantId}; se procede asumiendo el vertical solicitado.")]
    private static partial void LogMerchantUnavailable(ILogger logger, Guid merchantId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Catalog no respondio para el item {ItemId}; usando el precio del cache local.")]
    private static partial void LogPriceFallback(ILogger logger, Guid itemId);
}
