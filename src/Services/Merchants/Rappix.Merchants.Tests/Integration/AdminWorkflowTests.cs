using System.Net;
using System.Net.Http.Headers;
using Rappix.Merchants.Application.Geo;
using FluentAssertions;
using Rappix.Merchants.Domain.Merchants;
using Rappix.Merchants.Infrastructure.Persistence;

namespace Rappix.Merchants.Tests.Integration;

/// <summary>Workflow de administracion: listar pendientes, aprobar y autorizar por rol.</summary>
[Collection(MerchantsApiCollection.Name)]
public sealed class AdminWorkflowTests(MerchantsApiFactory factory)
{
    [Fact]
    public async Task Admin_Approves_AndMerchantBecomesPubliclyVisible()
    {
        Guid merchantId = await SeedPendingAsync();

        HttpClient admin = factory.CreateClient();
        admin.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestTokens.Admin());

        HttpResponseMessage list = await admin.GetAsync("/api/v1/admin/merchants?status=Pending");
        list.StatusCode.Should().Be(HttpStatusCode.OK);

        HttpResponseMessage approve = await admin.PostAsync($"/api/v1/admin/merchants/{merchantId}/approve", content: null);
        approve.StatusCode.Should().Be(HttpStatusCode.NoContent);

        HttpClient anonymous = factory.CreateClient();
        HttpResponseMessage publicView = await anonymous.GetAsync($"/api/v1/merchants/{merchantId}");
        publicView.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Approve_WithMerchantToken_IsForbidden()
    {
        Guid merchantId = await SeedPendingAsync();

        HttpClient client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestTokens.Merchant(Guid.CreateVersion7()));

        HttpResponseMessage approve = await client.PostAsync($"/api/v1/admin/merchants/{merchantId}/approve", content: null);

        approve.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task PublicView_OfNonActiveMerchant_ReturnsNotFound()
    {
        Guid merchantId = await SeedPendingAsync();

        HttpClient anonymous = factory.CreateClient();
        HttpResponseMessage publicView = await anonymous.GetAsync($"/api/v1/merchants/{merchantId}");

        publicView.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private async Task<Guid> SeedPendingAsync()
    {
        Guid ownerId = Guid.CreateVersion7();
        Guid merchantId = Guid.Empty;

        await factory.SeedAsync(db =>
        {
            DateTime now = DateTime.UtcNow;
            Merchant merchant = Merchant.CreateDraft(ownerId, "Pendiente", Slug.FromTrusted($"pendiente-{ownerId:N}"), VerticalType.Pharmacy, CommissionPercentage.Default, now);
            merchant.UpdateProfile("Pendiente", merchant.Slug, Rnc.Create(TestData.UniqueRnc()).Value, null, VerticalType.Pharmacy, now);
            merchant.AddCircleServiceArea(GeoFactory.CreatePoint(18.4861, -69.9312), 2000, now);
            merchant.ReplaceOperatingHours([new OperatingHoursRange(DayOfWeek.Monday, new TimeOnly(8, 0), new TimeOnly(18, 0))], now);
            merchant.SetPickupLocation(GeoFactory.CreatePoint(18.4861, -69.9312), now);
            merchant.SubmitForApproval(now);
            db.Merchants.Add(merchant);
            merchantId = merchant.Id.Value;
            return Task.CompletedTask;
        });

        return merchantId;
    }
}
