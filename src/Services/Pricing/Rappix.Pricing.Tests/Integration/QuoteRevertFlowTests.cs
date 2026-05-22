using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.Pricing.Application.Quotes.Consume;
using Rappix.Pricing.Application.Quotes.Revert;
using Rappix.Pricing.Application.Responses;
using Rappix.Pricing.Domain.Common;
using Rappix.Pricing.Domain.Coupons;
using Rappix.Pricing.Domain.Quotes;
using Rappix.Pricing.Infrastructure.Persistence;

namespace Rappix.Pricing.Tests.Integration;

/// <summary>
/// Prueba la compensacion de la saga contra PostgreSQL real: consumir una cotizacion con cupon redime el
/// cupon (UsedCount++ y registro de redencion); revertir el consumo lo deshace (UsedCount-- y redencion
/// marcada revertida, sin borrarse) y devuelve la cotizacion a Active.
/// </summary>
[Collection(PricingApiCollection.Name)]
public sealed class QuoteRevertFlowTests(PricingApiFactory factory)
{
    [Fact]
    public async Task ConsumeThenRevert_UndoesCouponRedemption_AndRestoresQuote()
    {
        Guid quoteId = Guid.Empty;
        CouponId couponId = default;

        await factory.SeedAsync(db =>
        {
            DateTime now = DateTime.UtcNow;
            Coupon coupon = Coupon.Create("REVERT10", DiscountType.Percentage, 10m, maxUses: 5, null,
                now.AddDays(-1), now.AddDays(30), null, null, now).Value;
            db.Coupons.Add(coupon);
            couponId = coupon.Id;

            QuoteLine line = QuoteLine.Create(Guid.CreateVersion7(), "Item", 100m, 0m, 1).Value;
            var breakdown = new PriceBreakdown { Subtotal = 100m, SurgeMultiplier = 1m, DiscountAmount = 10m, Total = 90m };
            Quote quote = Quote.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), VerticalType.Food, "DOP",
                [line], breakdown, coupon.Id, "REVERT10", now, 10).Value;
            db.Quotes.Add(quote);
            quoteId = quote.Id.Value;
            return Task.CompletedTask;
        });

        Guid orderId = Guid.CreateVersion7();

        // Consumir: redime el cupon.
        (await SendAsync(new ConsumeQuoteCommand(quoteId, orderId))).IsSuccess.Should().BeTrue();
        (await GetUsedCountAsync(couponId)).Should().Be(1);

        // Revertir: deshace la redencion y devuelve la cotizacion a Active.
        Result<QuoteResponse> revert = await SendAsync(new RevertQuoteCommand(quoteId, orderId, "compensacion"));

        revert.IsSuccess.Should().BeTrue();
        revert.Value.Status.Should().Be("Active");
        (await GetUsedCountAsync(couponId)).Should().Be(0);

        // La auditoria se conserva: la redencion sigue existiendo pero marcada revertida.
        await AssertRedemptionRevertedAsync(couponId);
    }

    [Fact]
    public async Task Revert_IsIdempotent_BySameOrder()
    {
        Guid quoteId = Guid.Empty;
        CouponId couponId = default;

        await factory.SeedAsync(db =>
        {
            DateTime now = DateTime.UtcNow;
            Coupon coupon = Coupon.Create("REVERTID", DiscountType.Percentage, 10m, maxUses: 5, null,
                now.AddDays(-1), now.AddDays(30), null, null, now).Value;
            db.Coupons.Add(coupon);
            couponId = coupon.Id;

            QuoteLine line = QuoteLine.Create(Guid.CreateVersion7(), "Item", 100m, 0m, 1).Value;
            var breakdown = new PriceBreakdown { Subtotal = 100m, SurgeMultiplier = 1m, DiscountAmount = 10m, Total = 90m };
            Quote quote = Quote.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), VerticalType.Food, "DOP",
                [line], breakdown, coupon.Id, "REVERTID", now, 10).Value;
            db.Quotes.Add(quote);
            quoteId = quote.Id.Value;
            return Task.CompletedTask;
        });

        Guid orderId = Guid.CreateVersion7();
        await SendAsync(new ConsumeQuoteCommand(quoteId, orderId));
        await SendAsync(new RevertQuoteCommand(quoteId, orderId, null));

        // Segunda reversion (re-entrega): no-op exitoso, UsedCount no baja de 0.
        (await SendAsync(new RevertQuoteCommand(quoteId, orderId, null))).IsSuccess.Should().BeTrue();
        (await GetUsedCountAsync(couponId)).Should().Be(0);
    }

    private async Task<TResult> SendAsync<TResult>(IRequest<TResult> request)
    {
        using IServiceScope scope = factory.Services.CreateScope();
        ISender sender = scope.ServiceProvider.GetRequiredService<ISender>();
        return await sender.Send(request);
    }

    private async Task<int> GetUsedCountAsync(CouponId couponId)
    {
        using IServiceScope scope = factory.Services.CreateScope();
        PricingDbContext context = scope.ServiceProvider.GetRequiredService<PricingDbContext>();
        return await context.Coupons.Where(coupon => coupon.Id == couponId).Select(coupon => coupon.UsedCount).FirstAsync();
    }

    private async Task AssertRedemptionRevertedAsync(CouponId couponId)
    {
        using IServiceScope scope = factory.Services.CreateScope();
        PricingDbContext context = scope.ServiceProvider.GetRequiredService<PricingDbContext>();
        CouponRedemption redemption = await context.CouponRedemptions.AsNoTracking().FirstAsync(r => r.CouponId == couponId);
        redemption.RevertedAtUtc.Should().NotBeNull();
        redemption.RevertReason.Should().Be("compensacion");
    }
}
