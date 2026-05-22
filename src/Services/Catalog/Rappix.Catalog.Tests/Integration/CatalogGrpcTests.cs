using System.Globalization;
using FluentAssertions;
using Grpc.Net.Client;
using Microsoft.AspNetCore.TestHost;
using NSubstitute;
using Rappix.Catalog.Api.Grpc;
using Rappix.Catalog.Application.Abstractions;
using Rappix.Catalog.Domain.Catalogs;
using Rappix.Catalog.Domain.Items;
using Rappix.Catalog.Infrastructure.Persistence;

namespace Rappix.Catalog.Tests.Integration;

/// <summary>
/// Pruebas del servicio gRPC CatalogValidationService sobre el TestServer en memoria. El cliente de
/// Merchants esta stubeado (NSubstitute): valida tanto el camino activo como el fallback al gating local.
/// </summary>
[Collection(CatalogApiCollection.Name)]
public sealed class CatalogGrpcTests(CatalogApiFactory factory)
{
    [Fact]
    public async Task GetItemPricing_ForActiveMerchant_ReturnsPurchasable()
    {
        Guid merchantId = Guid.CreateVersion7();
        Guid itemId = await SeedAsync(merchantId, catalogEnabled: true, itemAvailable: true, stock: 5);

        ItemPricingResponse response = await Client().GetItemPricingAsync(new ItemPricingRequest { ItemId = itemId.ToString() });

        response.Found.Should().BeTrue();
        response.IsPurchasable.Should().BeTrue();
        response.TracksInventory.Should().BeTrue();
        response.StockQuantity.Should().Be(5);
        decimal.Parse(response.PriceAmount, CultureInfo.InvariantCulture).Should().Be(199.99m);
    }

    [Fact]
    public async Task GetItemPricing_WhenMerchantsUnavailable_FallsBackToLocalGating()
    {
        Guid merchantId = Guid.CreateVersion7();
        Guid itemId = await SeedAsync(merchantId, catalogEnabled: false, itemAvailable: true, stock: 5);

        // Servicio Merchants no disponible para este merchant: el handler hace fallback al catalogo local (deshabilitado).
        factory.MerchantValidationClient
            .ValidateAsync(merchantId, Arg.Any<CancellationToken>())
            .Returns(MerchantValidation.Unavailable);

        ItemPricingResponse response = await Client().GetItemPricingAsync(new ItemPricingRequest { ItemId = itemId.ToString() });

        response.Found.Should().BeTrue();
        response.IsPurchasable.Should().BeFalse();
    }

    [Fact]
    public async Task GetItemPricing_ForUnknownItem_ReturnsNotFound()
    {
        ItemPricingResponse response = await Client().GetItemPricingAsync(
            new ItemPricingRequest { ItemId = Guid.CreateVersion7().ToString() });

        response.Found.Should().BeFalse();
    }

    private CatalogValidationService.CatalogValidationServiceClient Client()
    {
        GrpcChannel channel = GrpcChannel.ForAddress(
            factory.Server.BaseAddress,
            new GrpcChannelOptions { HttpHandler = factory.Server.CreateHandler() });
        return new CatalogValidationService.CatalogValidationServiceClient(channel);
    }

    private async Task<Guid> SeedAsync(Guid merchantId, bool catalogEnabled, bool itemAvailable, int stock)
    {
        Guid itemId = Guid.Empty;
        await factory.SeedAsync(db =>
        {
            MerchantCatalog catalog = MerchantCatalog.Create(merchantId, Guid.CreateVersion7(), VerticalType.Food, DateTime.UtcNow);
            if (!catalogEnabled)
            {
                catalog.Disable(DateTime.UtcNow);
            }

            db.Catalogs.Add(catalog);

            Item item = Item.Create(merchantId, null, "Producto gRPC", null, Money.Create(199.99m).Value, tracksInventory: true, attributes: null, DateTime.UtcNow).Value;
            if (!itemAvailable)
            {
                item.MakeUnavailable(DateTime.UtcNow);
            }

            db.Items.Add(item);
            db.StockLevels.Add(StockLevel.Create(item.Id, merchantId, stock, DateTime.UtcNow).Value);
            itemId = item.Id.Value;
            return Task.CompletedTask;
        });
        return itemId;
    }
}
