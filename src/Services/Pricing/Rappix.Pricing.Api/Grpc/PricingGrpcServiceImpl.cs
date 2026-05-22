using System.Globalization;
using Grpc.Core;
using MediatR;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.Pricing.Application.Quotes.Consume;
using Rappix.Pricing.Application.Quotes.Create;
using Rappix.Pricing.Application.Quotes.Get;
using Rappix.Pricing.Application.Quotes.Revert;
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
        if (!Guid.TryParse(request.QuoteId, out Guid quoteId) || !Guid.TryParse(request.OrderId, out Guid orderId))
        {
            return new ConsumeQuoteReply { Success = false, ErrorCode = "Pricing.Grpc.InvalidRequest" };
        }

        Result<QuoteResponse> result = await sender.Send(new ConsumeQuoteCommand(quoteId, orderId), context.CancellationToken);
        return result.IsSuccess
            ? new ConsumeQuoteReply { Success = true, Status = result.Value.Status }
            : new ConsumeQuoteReply { Success = false, ErrorCode = result.Error.Code };
    }

    public override async Task<GetQuoteReply> GetQuote(GetQuoteRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.QuoteId, out Guid quoteId))
        {
            return new GetQuoteReply { Success = false, ErrorCode = "Pricing.Grpc.InvalidRequest" };
        }

        Result<QuoteResponse> result = await sender.Send(new GetQuoteQuery(quoteId), context.CancellationToken);
        if (result.IsFailure)
        {
            return new GetQuoteReply { Success = false, ErrorCode = result.Error.Code };
        }

        QuoteResponse quote = result.Value;
        var reply = new GetQuoteReply
        {
            Success = true,
            QuoteId = quote.QuoteId.ToString(),
            CustomerUserId = quote.CustomerUserId.ToString(),
            MerchantId = quote.MerchantId.ToString(),
            Vertical = quote.Vertical,
            Currency = quote.Currency,
            Status = quote.Status,
            CouponCode = quote.CouponCode ?? string.Empty,
            CreatedAtUtc = quote.CreatedAtUtc.ToString("O", CultureInfo.InvariantCulture),
            ExpiresAtUtc = quote.ExpiresAtUtc.ToString("O", CultureInfo.InvariantCulture),
            Breakdown = ToBreakdownReply(quote.Breakdown),
        };

        reply.Lines.AddRange(quote.Lines.Select(line => new QuoteLineReply
        {
            ItemId = line.ItemId.ToString(),
            ItemName = line.ItemName,
            UnitPrice = Format(line.UnitPrice),
            ModifierTotal = Format(line.ModifierTotal),
            Quantity = line.Quantity,
            LineSubtotal = Format(line.LineSubtotal),
        }));

        return reply;
    }

    public override async Task<RevertQuoteReply> RevertQuoteConsumption(RevertQuoteRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.QuoteId, out Guid quoteId) || !Guid.TryParse(request.OrderId, out Guid orderId))
        {
            return new RevertQuoteReply { Success = false, ErrorCode = "Pricing.Grpc.InvalidRequest" };
        }

        string? reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason;
        Result<QuoteResponse> result = await sender.Send(new RevertQuoteCommand(quoteId, orderId, reason), context.CancellationToken);
        return result.IsSuccess
            ? new RevertQuoteReply { Success = true, Status = result.Value.Status }
            : new RevertQuoteReply { Success = false, ErrorCode = result.Error.Code };
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
