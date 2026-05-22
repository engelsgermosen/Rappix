using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Rappix.Pricing.Application.Responses;

namespace Rappix.Pricing.Tests.Integration;

/// <summary>Pruebas de los endpoints de administracion (cupones y surge) y de la autorizacion por rol.</summary>
[Collection(PricingApiCollection.Name)]
public sealed class AdminPricingTests(PricingApiFactory factory)
{
    [Fact]
    public async Task CreateCoupon_ThenListed_AndFetchable()
    {
        HttpClient client = AdminClient();
        var body = new
        {
            code = "ADMIN20",
            discountType = "Percentage",
            value = 20m,
            validFromUtc = DateTime.UtcNow.AddDays(-1),
            validUntilUtc = DateTime.UtcNow.AddDays(30),
        };

        HttpResponseMessage created = await client.PostAsJsonAsync("/api/v1/admin/pricing/coupons", body);

        created.StatusCode.Should().Be(HttpStatusCode.OK);
        CouponResponse coupon = (await created.Content.ReadFromJsonAsync<CouponResponse>())!;
        coupon.Code.Should().Be("ADMIN20");

        CouponResponse fetched = (await client.GetFromJsonAsync<CouponResponse>($"/api/v1/admin/pricing/coupons/{coupon.CouponId}"))!;
        fetched.Code.Should().Be("ADMIN20");
    }

    [Fact]
    public async Task CreateCoupon_DuplicateCode_Returns409()
    {
        HttpClient client = AdminClient();
        var body = new
        {
            code = "DUP10",
            discountType = "Percentage",
            value = 10m,
            validFromUtc = DateTime.UtcNow.AddDays(-1),
            validUntilUtc = DateTime.UtcNow.AddDays(30),
        };
        await client.PostAsJsonAsync("/api/v1/admin/pricing/coupons", body);

        HttpResponseMessage duplicate = await client.PostAsJsonAsync("/api/v1/admin/pricing/coupons", body);

        duplicate.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task CreateSurgeRule_Succeeds()
    {
        var body = new
        {
            zoneId = "Z1",
            vertical = "Food",
            startHour = 12,
            endHour = 14,
            multiplier = 1.5m,
            priority = 1,
        };

        HttpResponseMessage response = await AdminClient().PostAsJsonAsync("/api/v1/admin/pricing/surge-rules", body);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        SurgeRuleResponse rule = (await response.Content.ReadFromJsonAsync<SurgeRuleResponse>())!;
        rule.Multiplier.Should().Be(1.5m);
        rule.ZoneId.Should().Be("Z1");
    }

    [Fact]
    public async Task CustomerToken_CannotAccessAdminEndpoints()
    {
        HttpClient client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestTokens.Customer(Guid.CreateVersion7()));

        HttpResponseMessage response = await client.GetAsync("/api/v1/admin/pricing/coupons");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private HttpClient AdminClient()
    {
        HttpClient client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestTokens.Admin(Guid.CreateVersion7()));
        return client;
    }
}
