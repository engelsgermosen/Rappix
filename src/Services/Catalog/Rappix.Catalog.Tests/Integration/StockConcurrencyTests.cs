using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.Catalog.Application.Responses;
using Rappix.Catalog.Application.Stock.Decrement;
using Rappix.Catalog.Domain;
using Rappix.Catalog.Domain.Catalogs;
using Rappix.Catalog.Domain.Items;
using Rappix.Catalog.Infrastructure.Persistence;

namespace Rappix.Catalog.Tests.Integration;

/// <summary>
/// Prueba que el token de concurrencia xmin evita la sobreventa: 20 decrementos concurrentes sobre un
/// stock de 10 terminan exactamente en 10 exitos, 10 con stock insuficiente y cantidad final 0.
/// </summary>
[Collection(CatalogApiCollection.Name)]
public sealed class StockConcurrencyTests(CatalogApiFactory factory)
{
    private enum DecrementOutcome
    {
        Success,
        Insufficient,
        Conflict,
    }

    [Fact]
    public async Task ConcurrentDecrements_DoNotOversell()
    {
        Guid merchantId = Guid.CreateVersion7();
        Guid itemId = await SeedTrackedItemAsync(merchantId, stock: 10);

        int successes = 0;
        int insufficient = 0;

        await Task.WhenAll(Enumerable.Range(0, 20).Select(async _ =>
        {
            DecrementOutcome outcome = await DecrementWithRetryAsync(merchantId, itemId);
            if (outcome == DecrementOutcome.Success)
            {
                Interlocked.Increment(ref successes);
            }
            else if (outcome == DecrementOutcome.Insufficient)
            {
                Interlocked.Increment(ref insufficient);
            }
        }));

        successes.Should().Be(10);
        insufficient.Should().Be(10);
        (await GetQuantityAsync(itemId)).Should().Be(0);
    }

    private async Task<DecrementOutcome> DecrementWithRetryAsync(Guid merchantId, Guid itemId)
    {
        for (int attempt = 0; attempt < 100; attempt++)
        {
            using IServiceScope scope = factory.Services.CreateScope();
            ISender sender = scope.ServiceProvider.GetRequiredService<ISender>();
            Result<StockResponse> result = await sender.Send(new DecrementStockCommand(merchantId, itemId, 1));

            if (result.IsSuccess)
            {
                return DecrementOutcome.Success;
            }

            if (result.Error.Code == StockErrors.InsufficientStock.Code)
            {
                return DecrementOutcome.Insufficient;
            }

            // ConcurrencyConflict (xmin): otra transaccion gano; reintentar.
            await Task.Delay(Random.Shared.Next(5, 25));
        }

        return DecrementOutcome.Conflict;
    }

    private async Task<int> GetQuantityAsync(Guid itemId)
    {
        using IServiceScope scope = factory.Services.CreateScope();
        CatalogDbContext context = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        return await context.StockLevels
            .Where(stock => stock.Id == new ItemId(itemId))
            .Select(stock => stock.Quantity)
            .FirstAsync();
    }

    private async Task<Guid> SeedTrackedItemAsync(Guid merchantId, int stock)
    {
        Guid itemId = Guid.Empty;
        await factory.SeedAsync(db =>
        {
            db.Catalogs.Add(MerchantCatalog.Create(merchantId, Guid.CreateVersion7(), VerticalType.Food, DateTime.UtcNow));
            Item item = Item.Create(merchantId, null, "Producto con stock", null, Money.Create(100m).Value, tracksInventory: true, attributes: null, DateTime.UtcNow).Value;
            db.Items.Add(item);
            db.StockLevels.Add(StockLevel.Create(item.Id, merchantId, stock, DateTime.UtcNow).Value);
            itemId = item.Id.Value;
            return Task.CompletedTask;
        });
        return itemId;
    }
}
