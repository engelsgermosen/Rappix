using FluentAssertions;
using Rappix.Orders.Domain.Common;

namespace Rappix.Orders.Tests.Unit;

/// <summary>Pruebas del value object DeliveryAddress (validacion de calle y coordenadas).</summary>
public sealed class DeliveryAddressTests
{
    [Fact]
    public void Create_Valid_Succeeds()
    {
        var result = DeliveryAddress.Create("Calle 1", "Apto 2", 18.48, -69.93);

        result.IsSuccess.Should().BeTrue();
        result.Value.Street.Should().Be("Calle 1");
        result.Value.Latitude.Should().Be(18.48);
    }

    [Fact]
    public void Create_BlankStreet_Fails()
    {
        DeliveryAddress.Create("  ", null, 18.0, -69.0).IsFailure.Should().BeTrue();
    }

    [Theory]
    [InlineData(91.0, -69.0)]
    [InlineData(-91.0, -69.0)]
    [InlineData(18.0, 181.0)]
    [InlineData(18.0, -181.0)]
    public void Create_CoordinatesOutOfRange_Fails(double latitude, double longitude)
    {
        DeliveryAddress.Create("Calle 1", null, latitude, longitude).IsFailure.Should().BeTrue();
    }
}
