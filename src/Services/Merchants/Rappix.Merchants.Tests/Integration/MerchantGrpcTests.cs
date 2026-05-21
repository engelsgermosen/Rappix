using FluentAssertions;
using Grpc.Net.Client;
using Microsoft.AspNetCore.TestHost;
using Rappix.Merchants.Api.Grpc;
using Rappix.Merchants.Application.Geo;
using Rappix.Merchants.Domain.Merchants;

namespace Rappix.Merchants.Tests.Integration;

/// <summary>Pruebas del servicio gRPC interno MerchantValidationService sobre el TestServer en memoria.</summary>
[Collection(MerchantsApiCollection.Name)]
public sealed class MerchantGrpcTests(MerchantsApiFactory factory)
{
    [Fact]
    public async Task IsMerchantActive_ReturnsActive_ForApprovedMerchant()
    {
        Guid merchantId = await SeedActiveAsync();
        MerchantValidationService.MerchantValidationServiceClient client = CreateClient();

        IsMerchantActiveResponse response = await client.IsMerchantActiveAsync(
            new MerchantIdRequest { MerchantId = merchantId.ToString() });

        response.IsActive.Should().BeTrue();
        response.Status.Should().Be("Active");
    }

    [Fact]
    public async Task IsMerchantActive_ReturnsInactive_ForUnknownMerchant()
    {
        MerchantValidationService.MerchantValidationServiceClient client = CreateClient();

        IsMerchantActiveResponse response = await client.IsMerchantActiveAsync(
            new MerchantIdRequest { MerchantId = Guid.CreateVersion7().ToString() });

        response.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task GetMerchantBasicInfo_ReturnsData_ForApprovedMerchant()
    {
        Guid merchantId = await SeedActiveAsync();
        MerchantValidationService.MerchantValidationServiceClient client = CreateClient();

        MerchantBasicInfoResponse response = await client.GetMerchantBasicInfoAsync(
            new MerchantIdRequest { MerchantId = merchantId.ToString() });

        response.Found.Should().BeTrue();
        response.IsActive.Should().BeTrue();
        response.VerticalType.Should().Be("Food");
    }

    private MerchantValidationService.MerchantValidationServiceClient CreateClient()
    {
        GrpcChannel channel = GrpcChannel.ForAddress(
            factory.Server.BaseAddress,
            new GrpcChannelOptions { HttpHandler = factory.Server.CreateHandler() });
        return new MerchantValidationService.MerchantValidationServiceClient(channel);
    }

    private async Task<Guid> SeedActiveAsync()
    {
        Guid ownerId = Guid.CreateVersion7();
        Guid merchantId = Guid.Empty;

        await factory.SeedAsync(db =>
        {
            DateTime now = DateTime.UtcNow;
            Merchant merchant = Merchant.CreateDraft(ownerId, "Grpc Shop", Slug.FromTrusted($"grpc-{ownerId:N}"), VerticalType.Food, CommissionPercentage.Default, now);
            merchant.UpdateProfile("Grpc Shop", merchant.Slug, Rnc.Create(TestData.UniqueRnc()).Value, null, VerticalType.Food, now);
            merchant.AddCircleServiceArea(GeoFactory.CreatePoint(18.4861, -69.9312), 2000, now);
            merchant.ReplaceOperatingHours([new OperatingHoursRange(DayOfWeek.Monday, new TimeOnly(8, 0), new TimeOnly(18, 0))], now);
            merchant.SubmitForApproval(now);
            merchant.Approve(now);
            db.Merchants.Add(merchant);
            merchantId = merchant.Id.Value;
            return Task.CompletedTask;
        });

        return merchantId;
    }
}
