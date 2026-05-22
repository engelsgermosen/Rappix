using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Rappix.Pricing.Application.Responses;
using Rappix.Pricing.Domain.Common;
using Rappix.Pricing.Domain.Coupons;
using Rappix.Pricing.Domain.Quotes;
using Rappix.Pricing.Infrastructure.Persistence;

namespace Rappix.Pricing.Tests.Integration;

/// <summary>
/// Pruebas del ciclo de vida de una cotizacion por HTTP: cotizar (desglose correcto), recuperar, aplicar
/// cupon y expiracion perezosa. El precio de los items lo provee el stub gRPC de Catalog (100 DOP).
/// </summary>
[Collection(PricingApiCollection.Name)]
public sealed class QuoteLifecycleTests(PricingApiFactory factory)
{
    [Fact]
    public async Task CreateQuote_ReturnsDesglosedBreakdown()
    {
        HttpClient client = CustomerClient();

        // 1 linea x cantidad 2 a 100 DOP -> subtotal 200; Food + 5km -> envio 60 + 15*5 = 135;
        // service 5% -> 10; tax 18% sobre (200 + 135 + 10) = 345 -> 62.10; total 407.10.
        var body = new
        {
            merchantId = Guid.CreateVersion7(),
            vertical = "Food",
            distanceKm = 5m,
            tip = 0m,
            isFirstOrder = false,
            lines = new[] { new { itemId = Guid.CreateVersion7(), quantity = 2, modifierTotal = 0m } },
        };

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/pricing/quotes", body);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        QuoteResponse quote = (await response.Content.ReadFromJsonAsync<QuoteResponse>())!;
        quote.Breakdown.Subtotal.Should().Be(200m);
        quote.Breakdown.DeliveryFee.Should().Be(135m);
        quote.Breakdown.ServiceFee.Should().Be(10m);
        quote.Breakdown.Tax.Should().Be(62.10m);
        quote.Breakdown.Total.Should().Be(407.10m);
        quote.Status.Should().Be("Active");
    }

    [Fact]
    public async Task GetQuote_AfterCreate_ReturnsTheQuote()
    {
        HttpClient client = CustomerClient();
        var body = new
        {
            merchantId = Guid.CreateVersion7(),
            vertical = "Food",
            distanceKm = 0m,
            tip = 0m,
            isFirstOrder = false,
            lines = new[] { new { itemId = Guid.CreateVersion7(), quantity = 1, modifierTotal = 0m } },
        };
        QuoteResponse created = (await (await client.PostAsJsonAsync("/api/v1/pricing/quotes", body)).Content.ReadFromJsonAsync<QuoteResponse>())!;

        HttpResponseMessage response = await client.GetAsync($"/api/v1/pricing/quotes/{created.QuoteId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        QuoteResponse fetched = (await response.Content.ReadFromJsonAsync<QuoteResponse>())!;
        fetched.QuoteId.Should().Be(created.QuoteId);
    }

    [Fact]
    public async Task CreateQuote_WithValidCoupon_AppliesDiscount()
    {
        Guid merchantId = Guid.CreateVersion7();
        await factory.SeedAsync(db =>
        {
            db.Coupons.Add(Coupon.Create("WELCOME10", DiscountType.Percentage, 10m, null, null,
                DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(30), null, null, DateTime.UtcNow).Value);
            return Task.CompletedTask;
        });

        HttpClient client = CustomerClient();
        var body = new
        {
            merchantId,
            vertical = "Food",
            distanceKm = 0m, // envio 60
            tip = 0m,
            couponCode = "WELCOME10",
            isFirstOrder = false,
            lines = new[] { new { itemId = Guid.CreateVersion7(), quantity = 2, modifierTotal = 0m } },
        };

        QuoteResponse quote = (await (await client.PostAsJsonAsync("/api/v1/pricing/quotes", body)).Content.ReadFromJsonAsync<QuoteResponse>())!;

        // subtotal 200; descuento 10% = 20; envio 60; service 10; tax 18% sobre (180+60+10)=250 -> 45; total 295.
        quote.CouponCode.Should().Be("WELCOME10");
        quote.Breakdown.DiscountAmount.Should().Be(20m);
        quote.Breakdown.Total.Should().Be(295m);
    }

    [Fact]
    public async Task GetQuote_Expired_Returns409AndMarksExpired()
    {
        Guid quoteId = await SeedExpiredQuoteAsync();

        HttpResponseMessage response = await CustomerClient().GetAsync($"/api/v1/pricing/quotes/{quoteId}");

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);

        using var scope = factory.Services.CreateScope();
        PricingDbContext context = scope.ServiceProvider.GetRequiredService<PricingDbContext>();
        Quote stored = await context.Quotes.SingleAsync(quote => quote.Id == new QuoteId(quoteId));
        stored.Status.Should().Be(QuoteStatus.Expired);
    }

    private async Task<Guid> SeedExpiredQuoteAsync()
    {
        Guid quoteId = Guid.Empty;
        await factory.SeedAsync(db =>
        {
            DateTime past = DateTime.UtcNow.AddMinutes(-30);
            QuoteLine line = QuoteLine.Create(Guid.CreateVersion7(), "Item", 100m, 0m, 1).Value;
            var breakdown = new PriceBreakdown { Subtotal = 100m, SurgeMultiplier = 1m, Total = 100m };
            Quote quote = Quote.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), VerticalType.Food, "DOP", [line], breakdown, null, null, past, 10).Value;
            db.Quotes.Add(quote);
            quoteId = quote.Id.Value;
            return Task.CompletedTask;
        });
        return quoteId;
    }

    private HttpClient CustomerClient()
    {
        HttpClient client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestTokens.Customer(Guid.CreateVersion7()));
        return client;
    }
}
