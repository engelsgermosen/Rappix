using System.Globalization;
using FluentAssertions;
using Grpc.Net.Client;
using Microsoft.AspNetCore.TestHost;
using Rappix.Pricing.Api.Grpc;

namespace Rappix.Pricing.Tests.Integration;

/// <summary>
/// Pruebas del servidor gRPC PricingService sobre el TestServer en memoria: QuotePrice produce el mismo
/// desglose que el REST y ConsumeQuote consume la cotizacion. Los clientes de Catalog/Merchants estan stubeados.
/// </summary>
[Collection(PricingApiCollection.Name)]
public sealed class PricingGrpcTests(PricingApiFactory factory)
{
    [Fact]
    public async Task QuotePrice_ReturnsBreakdown()
    {
        QuoteRequest request = NewRequest();

        QuoteReply reply = await Client().QuotePriceAsync(request);

        reply.Success.Should().BeTrue();
        Parse(reply.Breakdown.Subtotal).Should().Be(200m);
        Parse(reply.Breakdown.DeliveryFee).Should().Be(135m);
        Parse(reply.Breakdown.Total).Should().Be(407.10m);
        reply.Status.Should().Be("Active");
    }

    [Fact]
    public async Task ConsumeQuote_AfterQuote_MarksConsumed()
    {
        QuoteReply quote = await Client().QuotePriceAsync(NewRequest());

        ConsumeQuoteReply consume = await Client().ConsumeQuoteAsync(new ConsumeQuoteRequest { QuoteId = quote.QuoteId });

        consume.Success.Should().BeTrue();
        consume.Status.Should().Be("Consumed");
    }

    [Fact]
    public async Task QuotePrice_InvalidRequest_ReturnsError()
    {
        QuoteReply reply = await Client().QuotePriceAsync(new QuoteRequest { CustomerUserId = "not-a-guid" });

        reply.Success.Should().BeFalse();
        reply.ErrorCode.Should().NotBeNullOrEmpty();
    }

    private static QuoteRequest NewRequest()
    {
        var request = new QuoteRequest
        {
            CustomerUserId = Guid.CreateVersion7().ToString(),
            MerchantId = Guid.CreateVersion7().ToString(),
            Vertical = "Food",
            DistanceKm = "5",
            Tip = "0",
            IsFirstOrder = false,
        };
        request.Lines.Add(new QuoteLineRequest { ItemId = Guid.CreateVersion7().ToString(), Quantity = 2, ModifierTotal = "0" });
        return request;
    }

    private static decimal Parse(string value) => decimal.Parse(value, CultureInfo.InvariantCulture);

    private PricingService.PricingServiceClient Client()
    {
        GrpcChannel channel = GrpcChannel.ForAddress(
            factory.Server.BaseAddress,
            new GrpcChannelOptions { HttpHandler = factory.Server.CreateHandler() });
        return new PricingService.PricingServiceClient(channel);
    }
}
