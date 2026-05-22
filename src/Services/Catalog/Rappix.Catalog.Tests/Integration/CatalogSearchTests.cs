using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Rappix.Catalog.Domain.Catalogs;
using Rappix.Catalog.Domain.Items;

namespace Rappix.Catalog.Tests.Integration;

/// <summary>Pruebas de la busqueda full-text (español) sobre la columna generada tsvector + indice GIN.</summary>
[Collection(CatalogApiCollection.Name)]
public sealed class CatalogSearchTests(CatalogApiFactory factory)
{
    [Fact]
    public async Task Search_MatchesByWord_AndExcludesDisabledCatalogs()
    {
        Guid enabledMerchant = Guid.CreateVersion7();
        Guid disabledMerchant = Guid.CreateVersion7();

        await factory.SeedAsync(db =>
        {
            db.Catalogs.Add(MerchantCatalog.Create(enabledMerchant, Guid.CreateVersion7(), VerticalType.Food, DateTime.UtcNow));

            MerchantCatalog disabled = MerchantCatalog.Create(disabledMerchant, Guid.CreateVersion7(), VerticalType.Food, DateTime.UtcNow);
            disabled.Disable(DateTime.UtcNow);
            db.Catalogs.Add(disabled);

            db.Items.Add(NewItem(enabledMerchant, "Hamburguesa Clasica", "Con queso y tocino"));
            db.Items.Add(NewItem(enabledMerchant, "Pizza Pepperoni", "Masa delgada"));
            db.Items.Add(NewItem(enabledMerchant, "Jugo de Naranja", "Natural"));
            db.Items.Add(NewItem(disabledMerchant, "Hamburguesa Oculta", "De un merchant suspendido"));
            return Task.CompletedTask;
        });

        (await SearchCountAsync($"q=hamburguesa&merchantId={enabledMerchant}")).Should().Be(1);
        (await SearchCountAsync($"q=pizza&merchantId={enabledMerchant}")).Should().Be(1);
        (await SearchCountAsync($"q=inexistente&merchantId={enabledMerchant}")).Should().Be(0);

        // El item del catalogo deshabilitado no aparece (filtro de comprabilidad).
        (await SearchCountAsync("q=hamburguesa")).Should().Be(1);
    }

    [Fact]
    public async Task Search_AppliesSpanishStemming()
    {
        Guid merchant = Guid.CreateVersion7();
        await factory.SeedAsync(db =>
        {
            db.Catalogs.Add(MerchantCatalog.Create(merchant, Guid.CreateVersion7(), VerticalType.Food, DateTime.UtcNow));
            db.Items.Add(NewItem(merchant, "Empanada de pollo", "Frita"));
            return Task.CompletedTask;
        });

        // "empanadas" (plural) debe encontrar "empanada" gracias al stemmer español.
        (await SearchCountAsync($"q=empanadas&merchantId={merchant}")).Should().Be(1);
    }

    private async Task<int> SearchCountAsync(string queryString)
    {
        HttpResponseMessage response = await factory.CreateClient().GetAsync($"/api/v1/catalog/items/search?{queryString}");
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("totalCount").GetInt32();
    }

    private static Item NewItem(Guid merchantId, string name, string description) =>
        Item.Create(merchantId, null, name, description, Money.Create(100m).Value, tracksInventory: false, attributes: null, DateTime.UtcNow).Value;
}
