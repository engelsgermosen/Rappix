using System.Globalization;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using Rappix.Orders.Application.Abstractions;
using Rappix.Orders.Infrastructure.Grpc.Pricing;

namespace Rappix.Orders.Infrastructure.Grpc;

/// <summary>
/// Cliente gRPC del servicio Pricing. Traduce los DTO de proto (decimales como string) a los tipos de la
/// aplicacion. Ante RpcException devuelve un resultado con ServiceAvailable=false para que la saga reintente.
/// </summary>
internal sealed partial class PricingGrpcClient(
    PricingService.PricingServiceClient client,
    ILogger<PricingGrpcClient> logger)
    : IPricingClient
{
    public async Task<QuoteSnapshot> GetQuoteAsync(Guid quoteId, CancellationToken cancellationToken)
    {
        try
        {
            GetQuoteReply reply = await client.GetQuoteAsync(new GetQuoteRequest { QuoteId = quoteId.ToString() }, cancellationToken: cancellationToken);
            if (!reply.Success)
            {
                return new QuoteSnapshot(ServiceAvailable: true, Found: false, reply.ErrorCode, Guid.Empty, Guid.Empty, string.Empty, string.Empty, 0m, 0m, 0m, 0m, 0m, 0m, 0m, []);
            }

            var lines = reply.Lines.Select(line => new QuoteSnapshotLine(
                Guid.TryParse(line.ItemId, out Guid itemId) ? itemId : Guid.Empty,
                line.ItemName,
                Parse(line.UnitPrice),
                Parse(line.ModifierTotal),
                line.Quantity)).ToList();

            return new QuoteSnapshot(
                ServiceAvailable: true,
                Found: true,
                reply.Status,
                Guid.TryParse(reply.CustomerUserId, out Guid customerId) ? customerId : Guid.Empty,
                Guid.TryParse(reply.MerchantId, out Guid merchantId) ? merchantId : Guid.Empty,
                reply.Vertical,
                reply.Currency,
                Parse(reply.Breakdown.Subtotal),
                Parse(reply.Breakdown.DeliveryFee),
                Parse(reply.Breakdown.ServiceFee),
                Parse(reply.Breakdown.Tax),
                Parse(reply.Breakdown.Tip),
                Parse(reply.Breakdown.DiscountAmount),
                Parse(reply.Breakdown.Total),
                lines);
        }
        catch (RpcException ex)
        {
            LogUnavailable(logger, quoteId, ex.StatusCode, ex);
            return QuoteSnapshot.Unavailable;
        }
    }

    public async Task<PricingOperationResult> ConsumeQuoteAsync(Guid quoteId, Guid orderId, CancellationToken cancellationToken)
    {
        try
        {
            ConsumeQuoteReply reply = await client.ConsumeQuoteAsync(
                new ConsumeQuoteRequest { QuoteId = quoteId.ToString(), OrderId = orderId.ToString() },
                cancellationToken: cancellationToken);
            return new PricingOperationResult(ServiceAvailable: true, reply.Success, reply.ErrorCode, reply.Status);
        }
        catch (RpcException ex)
        {
            LogUnavailable(logger, quoteId, ex.StatusCode, ex);
            return PricingOperationResult.Unavailable;
        }
    }

    public async Task<PricingOperationResult> RevertQuoteAsync(Guid quoteId, Guid orderId, string reason, CancellationToken cancellationToken)
    {
        try
        {
            RevertQuoteReply reply = await client.RevertQuoteConsumptionAsync(
                new RevertQuoteRequest { QuoteId = quoteId.ToString(), OrderId = orderId.ToString(), Reason = reason },
                cancellationToken: cancellationToken);
            return new PricingOperationResult(ServiceAvailable: true, reply.Success, reply.ErrorCode, reply.Status);
        }
        catch (RpcException ex)
        {
            LogUnavailable(logger, quoteId, ex.StatusCode, ex);
            return PricingOperationResult.Unavailable;
        }
    }

    private static decimal Parse(string? value) =>
        decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal parsed) ? parsed : 0m;

    [LoggerMessage(Level = LogLevel.Warning, Message = "Pricing no respondio para la cotizacion {QuoteId} ({StatusCode}).")]
    private static partial void LogUnavailable(ILogger logger, Guid quoteId, StatusCode statusCode, Exception exception);
}
