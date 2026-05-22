using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Rappix.Pricing.Application.ItemPrices.Cache;
using Rappix.Pricing.Infrastructure.Persistence;

namespace Rappix.Pricing.Tests.Integration;

/// <summary>Prueba que el cache de precios (alimentado por el consumer de ItemCreated) hace upsert idempotente.</summary>
[Collection(PricingApiCollection.Name)]
public sealed class ItemPriceCacheTests(PricingApiFactory factory)
{
    [Fact]
    public async Task CacheItemPrice_InsertsThenUpdates_SingleRow()
    {
        Guid itemId = Guid.CreateVersion7();
        Guid merchantId = Guid.CreateVersion7();

        await SendAsync(new CacheItemPriceCommand(itemId, merchantId, "Pizza", 250m, "DOP"));
        await SendAsync(new CacheItemPriceCommand(itemId, merchantId, "Pizza Grande", 300m, "DOP"));

        using IServiceScope scope = factory.Services.CreateScope();
        PricingDbContext context = scope.ServiceProvider.GetRequiredService<PricingDbContext>();
        var rows = await context.ItemPrices.Where(cache => cache.ItemId == itemId).ToListAsync();

        rows.Should().HaveCount(1);
        rows[0].BasePrice.Should().Be(300m);
        rows[0].Name.Should().Be("Pizza Grande");
    }

    private async Task SendAsync(CacheItemPriceCommand command)
    {
        using IServiceScope scope = factory.Services.CreateScope();
        ISender sender = scope.ServiceProvider.GetRequiredService<ISender>();
        await sender.Send(command);
    }
}
