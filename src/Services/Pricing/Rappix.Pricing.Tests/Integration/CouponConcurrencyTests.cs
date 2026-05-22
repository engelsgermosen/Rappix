using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.Pricing.Application.Quotes.Consume;
using Rappix.Pricing.Application.Responses;
using Rappix.Pricing.Domain.Common;
using Rappix.Pricing.Domain.Coupons;
using Rappix.Pricing.Domain.Errors;
using Rappix.Pricing.Domain.Quotes;
using Rappix.Pricing.Infrastructure.Persistence;

namespace Rappix.Pricing.Tests.Integration;

/// <summary>
/// Prueba que el token de concurrencia xmin del cupon evita exceder MaxUses: 10 consumos concurrentes de
/// cotizaciones que comparten un cupon con MaxUses=5 terminan en exactamente 5 redenciones y 5 rechazos,
/// con UsedCount final = 5 (analogo a la no-sobreventa de stock en Catalog).
/// </summary>
[Collection(PricingApiCollection.Name)]
public sealed class CouponConcurrencyTests(PricingApiFactory factory)
{
    private enum Outcome
    {
        Consumed,
        MaxUsesReached,
        Conflict,
    }

    [Fact]
    public async Task ConcurrentConsumes_DoNotExceedCouponMaxUses()
    {
        const int maxUses = 5;
        const int quoteCount = 10;

        CouponId couponId = default;
        var quoteIds = new List<Guid>();

        await factory.SeedAsync(db =>
        {
            DateTime now = DateTime.UtcNow;
            Coupon coupon = Coupon.Create("MAX5", DiscountType.Percentage, 10m, maxUses, null,
                now.AddDays(-1), now.AddDays(30), null, null, now).Value;
            db.Coupons.Add(coupon);
            couponId = coupon.Id;

            for (int i = 0; i < quoteCount; i++)
            {
                QuoteLine line = QuoteLine.Create(Guid.CreateVersion7(), "Item", 100m, 0m, 1).Value;
                var breakdown = new PriceBreakdown { Subtotal = 100m, SurgeMultiplier = 1m, DiscountAmount = 10m, Total = 90m };
                Quote quote = Quote.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), VerticalType.Food, "DOP",
                    [line], breakdown, coupon.Id, "MAX5", now, 10).Value;
                db.Quotes.Add(quote);
                quoteIds.Add(quote.Id.Value);
            }

            return Task.CompletedTask;
        });

        int consumed = 0;
        int maxUsesReached = 0;

        await Task.WhenAll(quoteIds.Select(async quoteId =>
        {
            Outcome outcome = await ConsumeWithRetryAsync(quoteId);
            if (outcome == Outcome.Consumed)
            {
                Interlocked.Increment(ref consumed);
            }
            else if (outcome == Outcome.MaxUsesReached)
            {
                Interlocked.Increment(ref maxUsesReached);
            }
        }));

        consumed.Should().Be(maxUses);
        maxUsesReached.Should().Be(quoteCount - maxUses);
        (await GetUsedCountAsync(couponId)).Should().Be(maxUses);
    }

    private async Task<Outcome> ConsumeWithRetryAsync(Guid quoteId)
    {
        Guid orderId = Guid.CreateVersion7();
        for (int attempt = 0; attempt < 100; attempt++)
        {
            using IServiceScope scope = factory.Services.CreateScope();
            ISender sender = scope.ServiceProvider.GetRequiredService<ISender>();
            Result<QuoteResponse> result = await sender.Send(new ConsumeQuoteCommand(quoteId, orderId));

            if (result.IsSuccess)
            {
                return Outcome.Consumed;
            }

            if (result.Error.Code == CouponErrors.MaxUsesReached.Code)
            {
                return Outcome.MaxUsesReached;
            }

            // ConcurrencyConflict (xmin): otra transaccion gano; reintentar.
            await Task.Delay(Random.Shared.Next(5, 25));
        }

        return Outcome.Conflict;
    }

    private async Task<int> GetUsedCountAsync(CouponId couponId)
    {
        using IServiceScope scope = factory.Services.CreateScope();
        PricingDbContext context = scope.ServiceProvider.GetRequiredService<PricingDbContext>();
        return await context.Coupons
            .Where(coupon => coupon.Id == couponId)
            .Select(coupon => coupon.UsedCount)
            .FirstAsync();
    }
}
