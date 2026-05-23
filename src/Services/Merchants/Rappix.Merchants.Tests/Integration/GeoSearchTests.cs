using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Rappix.Merchants.Application.Geo;
using Rappix.Merchants.Domain.Merchants;
using Rappix.Merchants.Infrastructure.Persistence;

namespace Rappix.Merchants.Tests.Integration;

/// <summary>Busqueda geoespacial PostGIS: circulos (ST_DWithin) y poligonos (ST_Contains).</summary>
[Collection(MerchantsApiCollection.Name)]
public sealed class GeoSearchTests(MerchantsApiFactory factory)
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    [Fact]
    public async Task Nearby_IncludesMerchant_WhenPointInsideCircle()
    {
        string slug = $"geo-circle-{Guid.CreateVersion7():N}";
        await SeedActiveAsync(slug, merchant => merchant.AddCircleServiceArea(GeoFactory.CreatePoint(18.4861, -69.9312), 5000, DateTime.UtcNow));

        IReadOnlyList<NearbyDto> results = await SearchAsync(18.4861, -69.9312);

        results.Should().Contain(merchant => merchant.Slug == slug);
    }

    [Fact]
    public async Task Nearby_ExcludesMerchant_WhenPointOutsideCircle()
    {
        string slug = $"geo-far-{Guid.CreateVersion7():N}";
        await SeedActiveAsync(slug, merchant => merchant.AddCircleServiceArea(GeoFactory.CreatePoint(18.4861, -69.9312), 1000, DateTime.UtcNow));

        // Santiago, a ~150 km del punto sembrado.
        IReadOnlyList<NearbyDto> results = await SearchAsync(19.4517, -70.6970);

        results.Should().NotContain(merchant => merchant.Slug == slug);
    }

    [Fact]
    public async Task Nearby_IncludesMerchant_WhenPointInsidePolygon()
    {
        string slug = $"geo-polygon-{Guid.CreateVersion7():N}";
        IReadOnlyList<double[]> ring =
        [
            [-69.95, 18.45],
            [-69.90, 18.45],
            [-69.90, 18.50],
            [-69.95, 18.50],
            [-69.95, 18.45],
        ];
        await SeedActiveAsync(slug, merchant => merchant.AddPolygonServiceArea(GeoFactory.CreatePolygon(ring), DateTime.UtcNow));

        IReadOnlyList<NearbyDto> results = await SearchAsync(18.475, -69.925);

        results.Should().Contain(merchant => merchant.Slug == slug);
    }

    private async Task<IReadOnlyList<NearbyDto>> SearchAsync(double lat, double lng)
    {
        HttpClient client = factory.CreateClient();
        HttpResponseMessage response = await client.GetAsync(
            $"/api/v1/merchants/nearby?lat={lat.ToString(System.Globalization.CultureInfo.InvariantCulture)}&lng={lng.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<List<NearbyDto>>(Json))!;
    }

    private Task SeedActiveAsync(string slug, Action<Merchant> addServiceArea) => factory.SeedAsync(db =>
    {
        DateTime now = DateTime.UtcNow;
        Guid ownerId = Guid.CreateVersion7();
        Merchant merchant = Merchant.CreateDraft(ownerId, "Geo Shop", Slug.FromTrusted(slug), VerticalType.Food, CommissionPercentage.Default, now);
        merchant.UpdateProfile("Geo Shop", merchant.Slug, Rnc.Create(TestData.UniqueRnc()).Value, null, VerticalType.Food, now);
        addServiceArea(merchant);
        merchant.ReplaceOperatingHours([new OperatingHoursRange(DayOfWeek.Monday, new TimeOnly(0, 0), new TimeOnly(23, 59))], now);
        merchant.SetPickupLocation(GeoFactory.CreatePoint(18.4861, -69.9312), now);
        merchant.SubmitForApproval(now);
        merchant.Approve(now);
        db.Merchants.Add(merchant);
        return Task.CompletedTask;
    });

    private sealed record NearbyDto(Guid Id, string Name, string Slug, string VerticalType);
}
