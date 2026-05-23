using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Rappix.Merchants.Domain.Merchants;
using Rappix.Merchants.Infrastructure.Persistence;

namespace Rappix.Merchants.Tests.Integration;

/// <summary>Flujo del owner: del Draft (sembrado) hasta enviar a aprobacion, mas la subida de logo.</summary>
[Collection(MerchantsApiCollection.Name)]
public sealed class MerchantLifecycleTests(MerchantsApiFactory factory)
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    [Fact]
    public async Task OwnerFlow_FromDraft_ToPending()
    {
        Guid ownerId = Guid.CreateVersion7();
        await SeedDraftAsync(ownerId);

        HttpClient client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestTokens.Merchant(ownerId));

        HttpResponseMessage me = await client.GetAsync("/api/v1/merchants/me");
        me.StatusCode.Should().Be(HttpStatusCode.OK);
        MerchantDto draft = (await me.Content.ReadFromJsonAsync<MerchantDto>(Json))!;
        draft.Status.Should().Be("Draft");

        HttpResponseMessage profile = await client.PutAsJsonAsync("/api/v1/merchants/me", new
        {
            name = "Tienda Lulu",
            slug = $"tienda-{ownerId:N}",
            rnc = TestData.UniqueRnc(),
            description = "Mi tienda de prueba",
            verticalType = "Food",
        });
        profile.StatusCode.Should().Be(HttpStatusCode.OK);

        HttpResponseMessage area = await client.PostAsJsonAsync("/api/v1/merchants/me/service-areas", new
        {
            type = "Circle",
            centerLatitude = 18.4861,
            centerLongitude = -69.9312,
            radiusMeters = 3000,
        });
        area.StatusCode.Should().Be(HttpStatusCode.OK);

        HttpResponseMessage hours = await client.PutAsJsonAsync("/api/v1/merchants/me/operating-hours", new
        {
            hours = new[] { new { dayOfWeek = "Monday", opensAt = "08:00", closesAt = "20:00" } },
        });
        hours.StatusCode.Should().Be(HttpStatusCode.OK);

        // Pickup obligatorio (Fase 6: Dispatch usa esta coord para matching geo de couriers).
        HttpResponseMessage pickup = await client.PutAsJsonAsync("/api/v1/merchants/me/pickup-location", new
        {
            latitude = 18.4861,
            longitude = -69.9312,
        });
        pickup.StatusCode.Should().Be(HttpStatusCode.OK);

        HttpResponseMessage submit = await client.PostAsync("/api/v1/merchants/me/submit-for-approval", content: null);
        submit.StatusCode.Should().Be(HttpStatusCode.NoContent);

        MerchantDto pending = (await (await client.GetAsync("/api/v1/merchants/me")).Content.ReadFromJsonAsync<MerchantDto>(Json))!;
        pending.Status.Should().Be("Pending");
    }

    [Fact]
    public async Task UploadLogo_WithValidPng_Succeeds()
    {
        Guid ownerId = Guid.CreateVersion7();
        await SeedDraftAsync(ownerId);

        HttpClient client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestTokens.Merchant(ownerId));

        using var content = new MultipartFormDataContent();
        var file = new ByteArrayContent(TestData.PngBytes(400, 400));
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        content.Add(file, "file", "logo.png");

        HttpResponseMessage response = await client.PostAsync("/api/v1/merchants/me/logo", content);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Me_WithoutToken_ReturnsUnauthorized()
    {
        HttpClient client = factory.CreateClient();

        HttpResponseMessage me = await client.GetAsync("/api/v1/merchants/me");

        me.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private Task SeedDraftAsync(Guid ownerId) => factory.SeedAsync(db =>
    {
        Merchant merchant = Merchant.CreateDraft(
            ownerId,
            "Mi negocio",
            Slug.CreatePlaceholder(ownerId),
            VerticalType.Food,
            CommissionPercentage.Default,
            DateTime.UtcNow);
        db.Merchants.Add(merchant);
        return Task.CompletedTask;
    });

    private sealed record MerchantDto(Guid Id, string Name, string Slug, string Status, string VerticalType);
}
