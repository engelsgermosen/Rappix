using FluentAssertions;
using Microsoft.Extensions.Options;
using Rappix.Pricing.Application.Configuration;
using Rappix.Pricing.Application.Pricing.Delivery;
using Rappix.Pricing.Domain.Common;

namespace Rappix.Pricing.Tests.Unit;

/// <summary>Pruebas de las estrategias de envio por vertical y su resolutor.</summary>
public sealed class DeliveryFeeResolverTests
{
    [Theory]
    [InlineData(VerticalType.Food, 2, 90)]      // 60 + 15*2
    [InlineData(VerticalType.Grocery, 0, 60)]   // 60 + 15*0
    [InlineData(VerticalType.Pharmacy, 1, 75)]  // 60 + 15*1
    public void Resolve_StandardVerticals_UseStandardRate(VerticalType vertical, decimal distanceKm, decimal expected)
    {
        Resolver().Resolve(vertical, distanceKm).Should().Be(expected);
    }

    [Fact]
    public void Resolve_Parcel_UsesParcelRate()
    {
        Resolver().Resolve(VerticalType.Parcel, 2m).Should().Be(130m); // 80 + 25*2
    }

    [Fact]
    public void Resolve_NegativeDistance_ChargesBaseOnly()
    {
        Resolver().Resolve(VerticalType.Food, -5m).Should().Be(60m);
    }

    private static DeliveryFeeResolver Resolver()
    {
        var options = Options.Create(new PricingOptions
        {
            Delivery = new DeliveryOptions
            {
                Standard = new DeliveryRate { BaseFee = 60m, PerKm = 15m },
                Parcel = new DeliveryRate { BaseFee = 80m, PerKm = 25m },
            },
        });

        IDeliveryFeeStrategy[] strategies = [new StandardDeliveryFeeStrategy(options), new ParcelDeliveryFeeStrategy(options)];
        return new DeliveryFeeResolver(strategies, options);
    }
}
