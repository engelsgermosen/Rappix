using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.Catalog.Application.Pricing.GetItemPricing;
using Rappix.Catalog.Application.Responses;
using Rappix.Catalog.Application.Stock.Commit;
using Rappix.Catalog.Application.Stock.Release;
using Rappix.Catalog.Application.Stock.Reserve;
using Rappix.Catalog.Domain;
using Rappix.Catalog.Domain.Catalogs;
using Rappix.Catalog.Domain.Items;
using Rappix.Catalog.Infrastructure.Persistence;

namespace Rappix.Catalog.Tests.Integration;

/// <summary>
/// Pruebas del patron de reserva contra PostgreSQL real (via MediatR): hold -> commit/release,
/// idempotencia por OrderId, todo-o-nada, barrido de holds vencidos (TTL) y reflejo del disponible
/// en la comprabilidad.
/// </summary>
[Collection(CatalogApiCollection.Name)]
public sealed class StockReservationFlowTests(CatalogApiFactory factory)
{
    [Fact]
    public async Task Reserve_ThenCommit_DecrementsPhysicalStock()
    {
        Guid merchantId = Guid.CreateVersion7();
        Guid itemId = await SeedTrackedItemAsync(merchantId, stock: 10);
        Guid orderId = Guid.CreateVersion7();

        (await ReserveAsync(orderId, (itemId, 3))).IsSuccess.Should().BeTrue();

        StockLevel afterReserve = await GetStockAsync(itemId);
        afterReserve.Quantity.Should().Be(10);
        afterReserve.ReservedQuantity.Should().Be(3);
        afterReserve.Available.Should().Be(7);

        (await SendAsync(new CommitStockCommand(orderId))).IsSuccess.Should().BeTrue();

        StockLevel afterCommit = await GetStockAsync(itemId);
        afterCommit.Quantity.Should().Be(7);
        afterCommit.ReservedQuantity.Should().Be(0);
        afterCommit.Available.Should().Be(7);
    }

    [Fact]
    public async Task Reserve_ThenRelease_RestoresAvailable()
    {
        Guid merchantId = Guid.CreateVersion7();
        Guid itemId = await SeedTrackedItemAsync(merchantId, stock: 8);
        Guid orderId = Guid.CreateVersion7();

        await ReserveAsync(orderId, (itemId, 5));
        (await SendAsync(new ReleaseStockCommand(orderId))).IsSuccess.Should().BeTrue();

        StockLevel stock = await GetStockAsync(itemId);
        stock.Quantity.Should().Be(8);
        stock.ReservedQuantity.Should().Be(0);
        stock.Available.Should().Be(8);
    }

    [Fact]
    public async Task Reserve_InsufficientAvailable_Fails_AndHoldsNothing()
    {
        Guid merchantId = Guid.CreateVersion7();
        Guid itemId = await SeedTrackedItemAsync(merchantId, stock: 2);
        Guid orderId = Guid.CreateVersion7();

        Result result = await ReserveAsync(orderId, (itemId, 5));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(StockErrors.InsufficientStock.Code);
        (await GetStockAsync(itemId)).ReservedQuantity.Should().Be(0);
    }

    [Fact]
    public async Task Reserve_AllOrNothing_WhenOneLineInsufficient()
    {
        Guid merchantId = Guid.CreateVersion7();
        Guid plenty = await SeedTrackedItemAsync(merchantId, stock: 10);
        Guid scarce = await SeedTrackedItemAsync(merchantId, stock: 1);
        Guid orderId = Guid.CreateVersion7();

        Result result = await ReserveAsync(orderId, (plenty, 2), (scarce, 5));

        result.IsFailure.Should().BeTrue();
        (await GetStockAsync(plenty)).ReservedQuantity.Should().Be(0);
        (await GetStockAsync(scarce)).ReservedQuantity.Should().Be(0);
    }

    [Fact]
    public async Task Reserve_IsIdempotent_BySameOrderId()
    {
        Guid merchantId = Guid.CreateVersion7();
        Guid itemId = await SeedTrackedItemAsync(merchantId, stock: 10);
        Guid orderId = Guid.CreateVersion7();

        (await ReserveAsync(orderId, (itemId, 3))).IsSuccess.Should().BeTrue();
        (await ReserveAsync(orderId, (itemId, 3))).IsSuccess.Should().BeTrue(); // re-entrega

        StockLevel stock = await GetStockAsync(itemId);
        stock.ReservedQuantity.Should().Be(3); // no se duplica
    }

    [Fact]
    public async Task Reserve_SweepsExpiredHolds_OfOtherOrders()
    {
        Guid merchantId = Guid.CreateVersion7();
        Guid abandonedOrderId = Guid.CreateVersion7();
        DateTime past = DateTime.UtcNow.AddHours(-1);

        // Siembra: stock totalmente apartado por un hold YA VENCIDO de otro pedido (saga muerta).
        Guid itemId = Guid.Empty;
        await factory.SeedAsync(db =>
        {
            db.Catalogs.Add(MerchantCatalog.Create(merchantId, Guid.CreateVersion7(), VerticalType.Food, DateTime.UtcNow));
            Item item = Item.Create(merchantId, null, "Producto reservado", null, Money.Create(50m).Value, tracksInventory: true, attributes: null, DateTime.UtcNow).Value;
            db.Items.Add(item);
            StockLevel stock = StockLevel.Create(item.Id, merchantId, 5, past).Value;
            stock.Reserve(5, past); // ReservedQuantity = 5, Available = 0
            db.StockLevels.Add(stock);
            db.StockReservations.Add(StockReservation.Create(abandonedOrderId, item.Id, merchantId, 5, expiresAtUtc: past, utcNow: past));
            itemId = item.Id.Value;
            return Task.CompletedTask;
        });

        // Un nuevo pedido reserva: el barrido perezoso libera el hold vencido y la reserva procede.
        Guid newOrderId = Guid.CreateVersion7();
        Result result = await ReserveAsync(newOrderId, (itemId, 4));

        result.IsSuccess.Should().BeTrue();
        StockLevel after = await GetStockAsync(itemId);
        after.ReservedQuantity.Should().Be(4); // 5 vencidas liberadas, 4 nuevas apartadas
        after.Available.Should().Be(1);
    }

    [Fact]
    public async Task Reserve_MakesItemNotPurchasable_WhenAvailableIsZero()
    {
        Guid merchantId = Guid.CreateVersion7();
        Guid itemId = await SeedTrackedItemAsync(merchantId, stock: 3);
        Guid orderId = Guid.CreateVersion7();

        await ReserveAsync(orderId, (itemId, 3)); // aparta todo el disponible

        Result<ItemPricingResponse> pricing = await SendAsync(new GetItemPricingQuery(itemId));
        pricing.IsSuccess.Should().BeTrue();
        pricing.Value.StockQuantity.Should().Be(3); // fisico intacto
        pricing.Value.IsPurchasable.Should().BeFalse(); // pero no comprable: disponible 0
    }

    private async Task<Result> ReserveAsync(Guid orderId, params (Guid ItemId, int Quantity)[] lines)
    {
        var reserveLines = lines.Select(line => new ReserveStockLine(line.ItemId, line.Quantity)).ToList();
        return await SendAsync(new ReserveStockCommand(orderId, reserveLines, TtlSeconds: 1800));
    }

    private async Task<TResult> SendAsync<TResult>(IRequest<TResult> request)
    {
        using IServiceScope scope = factory.Services.CreateScope();
        ISender sender = scope.ServiceProvider.GetRequiredService<ISender>();
        return await sender.Send(request);
    }

    private async Task<StockLevel> GetStockAsync(Guid itemId)
    {
        using IServiceScope scope = factory.Services.CreateScope();
        CatalogDbContext context = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        return await context.StockLevels.AsNoTracking().FirstAsync(stock => stock.Id == new ItemId(itemId));
    }

    private async Task<Guid> SeedTrackedItemAsync(Guid merchantId, int stock)
    {
        Guid itemId = Guid.Empty;
        await factory.SeedAsync(async db =>
        {
            bool hasCatalog = await db.Catalogs.AnyAsync(catalog => catalog.MerchantId == merchantId);
            if (!hasCatalog)
            {
                db.Catalogs.Add(MerchantCatalog.Create(merchantId, Guid.CreateVersion7(), VerticalType.Food, DateTime.UtcNow));
            }

            Item item = Item.Create(merchantId, null, "Producto con stock", null, Money.Create(100m).Value, tracksInventory: true, attributes: null, DateTime.UtcNow).Value;
            db.Items.Add(item);
            db.StockLevels.Add(StockLevel.Create(item.Id, merchantId, stock, DateTime.UtcNow).Value);
            itemId = item.Id.Value;
        });
        return itemId;
    }
}
