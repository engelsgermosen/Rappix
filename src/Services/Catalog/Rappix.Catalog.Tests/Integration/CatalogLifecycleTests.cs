using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Rappix.Catalog.Domain.Catalogs;
using Rappix.Catalog.Infrastructure.Persistence;

namespace Rappix.Catalog.Tests.Integration;

/// <summary>Pruebas de extremo a extremo del flujo del owner sobre HTTP (incluye gating y validacion por vertical).</summary>
[Collection(CatalogApiCollection.Name)]
public sealed class CatalogLifecycleTests(CatalogApiFactory factory)
{
    [Fact]
    public async Task CreateGetUpdate_Item_FullOwnerFlow()
    {
        Guid ownerUserId = await SeedCatalogAsync(VerticalType.Food);
        HttpClient client = CreateAuthenticatedClient(ownerUserId);

        HttpResponseMessage created = await client.PostAsJsonAsync("/api/v1/catalog/me/items", new
        {
            name = "Pizza Margarita",
            description = "Clasica italiana",
            priceAmount = 250m,
            currency = "DOP",
            tracksInventory = false,
            initialStock = 0,
        });
        created.StatusCode.Should().Be(HttpStatusCode.OK);
        Guid itemId = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        HttpResponseMessage fetched = await client.GetAsync($"/api/v1/catalog/me/items/{itemId}");
        fetched.StatusCode.Should().Be(HttpStatusCode.OK);

        HttpResponseMessage list = await client.GetAsync("/api/v1/catalog/me/items");
        list.StatusCode.Should().Be(HttpStatusCode.OK);
        (await list.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("totalCount").GetInt32().Should().BeGreaterThanOrEqualTo(1);

        HttpResponseMessage unavailable = await client.PutAsJsonAsync($"/api/v1/catalog/me/items/{itemId}/availability", new { available = false });
        unavailable.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task CreateItem_WithInvalidVerticalAttribute_ReturnsBadRequest()
    {
        Guid ownerUserId = await SeedCatalogAsync(VerticalType.Food);
        HttpClient client = CreateAuthenticatedClient(ownerUserId);

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/catalog/me/items", new
        {
            name = "Item raro",
            priceAmount = 100m,
            tracksInventory = false,
            initialStock = 0,
            attributes = new Dictionary<string, string> { ["color"] = "rojo" },
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task OwnerEndpoints_WithoutToken_AreUnauthorized()
    {
        HttpResponseMessage response = await factory.CreateClient().GetAsync("/api/v1/catalog/me/items");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private HttpClient CreateAuthenticatedClient(Guid ownerUserId)
    {
        HttpClient client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestTokens.Merchant(ownerUserId));
        return client;
    }

    private async Task<Guid> SeedCatalogAsync(VerticalType vertical)
    {
        Guid ownerUserId = Guid.CreateVersion7();
        await factory.SeedAsync(db =>
        {
            db.Catalogs.Add(MerchantCatalog.Create(Guid.CreateVersion7(), ownerUserId, vertical, DateTime.UtcNow));
            return Task.CompletedTask;
        });
        return ownerUserId;
    }
}
