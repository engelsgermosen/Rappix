using System.Globalization;
using Grpc.Core;
using MediatR;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.Pricing.Application.Quotes.Consume;
using Rappix.Pricing.Application.Quotes.Create;
using Rappix.Pricing.Application.Responses;
using Rappix.Pricing.Domain.Common;

namespace Rappix.Pricing.Api.Grpc;

/// <summary>
/// Implementacion gRPC del servicio interno de pricing. Delega en MediatR (mismo calculo que el REST).
/// Los montos viajan como string para preservar la precision decimal del dinero.
/// </summary>
internal sealed class PricingGrpcServiceImpl(ISender sender) : PricingService.PricingServiceBase
{
    public override async Task<QuoteReply> QuotePrice(QuoteRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.CustomerUserId, out Guid customerUserId)
            || !Guid.TryParse(request.MerchantId, out Guid merchantId)
            || !Enum.TryParse(request.Vertical, ignoreCase: true, out VerticalType vertical))
        {
            return new QuoteReply { Success = false, ErrorCode = "Pricing.Grpc.InvalidRequest" };
        }

        List<QuoteLineInput> lines = [.. request.Lines.Select(line => new QuoteLineInput(
            Guid.TryParse(line.ItemId, out Guid itemId) ? itemId : Guid.Empty,
            line.Quantity,
            ParseDecimal(line.ModifierTotal)))];

        var command = new CreateQuoteCommand(
            customerUserId,
            merchantId,
            vertical,
            ParseDecimal(request.DistanceKm),
            string.IsNullOrWhiteSpace(request.ZoneId) ? null : request.ZoneId,
            ParseDecimal(request.Tip),
            string.IsNullOrWhiteSpace(request.CouponCode) ? null : request.CouponCode,
            request.IsFirstOrder,
            lines);

        Result<QuoteResponse> result = await sender.Send(command, context.CancellationToken);
        if (result.IsFailure)
        {
            return new QuoteReply { Success = false, ErrorCode = result.Error.Code };
        }

        QuoteResponse quote = result.Value;
        return new QuoteReply
        {
            Success = true,
            QuoteId = quote.QuoteId.ToString(),
            Currency = quote.Currency,
            Breakdown = ToBreakdownReply(quote.Breakdown),
            ExpiresAtUtc = quote.ExpiresAtUtc.ToString("O", CultureInfo.InvariantCulture),
            Status = quote.Status,
        };
    }

    public override async Task<ConsumeQuoteReply> ConsumeQuote(ConsumeQuoteRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.QuoteId, out Guid quoteId))
        {
            return new ConsumeQuoteReply { Success = false, ErrorCode = "Pricing.Grpc.InvalidRequest" };
        }

        Result<QuoteResponse> result = await sender.Send(new ConsumeQuoteCommand(quoteId), context.CancellationToken);
        return result.IsSuccess
            ? new ConsumeQuoteReply { Success = true, Status = result.Value.Status }
            : new ConsumeQuoteReply { Success = false, ErrorCode = result.Error.Code };
    }

    private static decimal ParseDecimal(string? value) =>
        decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal parsed) ? parsed : 0m;

    private static string Format(decimal value) => value.ToString(CultureInfo.InvariantCulture);

    private static PriceBreakdownReply ToBreakdownReply(PriceBreakdownResponse breakdown) => new()
    {
        Subtotal = Format(breakdown.Subtotal),
        SurgeMultiplier = Format(breakdown.SurgeMultiplier),
        SurgeAmount = Format(breakdown.SurgeAmount),
        DiscountAmount = Format(breakdown.DiscountAmount),
        DeliveryFee = Format(breakdown.DeliveryFee),
        ServiceFee = Format(breakdown.ServiceFee),
        Tax = Format(breakdown.Tax),
        Tip = Format(breakdown.Tip),
        Total = Format(breakdown.Total),
    };
}
