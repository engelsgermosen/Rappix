using FluentAssertions;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.Merchants.Application.Geo;
using Rappix.Merchants.Domain.Merchants;

namespace Rappix.Merchants.Tests.Unit;

/// <summary>Pruebas del discriminador de ServiceArea (poligono vs circulo) via el agregado.</summary>
public sealed class ServiceAreaTests
{
    private static readonly DateTime Now = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void AddCircleServiceArea_WithValidRadius_StoresCircleOnly()
    {
        Merchant merchant = Draft();

        Result result = merchant.AddCircleServiceArea(GeoFactory.CreatePoint(18.4861, -69.9312), 2000, Now);

        result.IsSuccess.Should().BeTrue();
        ServiceArea area = merchant.ServiceAreas.Should().ContainSingle().Subject;
        area.Type.Should().Be(ServiceAreaType.Circle);
        area.Center.Should().NotBeNull();
        area.RadiusMeters.Should().Be(2000);
        area.Polygon.Should().BeNull();
    }

    [Theory]
    [InlineData(50)]
    [InlineData(60000)]
    public void AddCircleServiceArea_WithRadiusOutOfRange_Fails(int radiusMeters)
    {
        Merchant merchant = Draft();

        Result result = merchant.AddCircleServiceArea(GeoFactory.CreatePoint(18.4861, -69.9312), radiusMeters, Now);

        result.IsFailure.Should().BeTrue();
        merchant.ServiceAreas.Should().BeEmpty();
    }

    [Fact]
    public void AddPolygonServiceArea_WithValidRing_StoresPolygonOnly()
    {
        Merchant merchant = Draft();
        IReadOnlyList<double[]> ring =
        [
            [-69.95, 18.45],
            [-69.90, 18.45],
            [-69.90, 18.50],
            [-69.95, 18.50],
            [-69.95, 18.45],
        ];

        Result result = merchant.AddPolygonServiceArea(GeoFactory.CreatePolygon(ring), Now);

        result.IsSuccess.Should().BeTrue();
        ServiceArea area = merchant.ServiceAreas.Should().ContainSingle().Subject;
        area.Type.Should().Be(ServiceAreaType.Polygon);
        area.Polygon.Should().NotBeNull();
        area.Center.Should().BeNull();
        area.RadiusMeters.Should().BeNull();
    }

    [Fact]
    public void RemoveServiceArea_RemovesTheArea()
    {
        Merchant merchant = Draft();
        merchant.AddCircleServiceArea(GeoFactory.CreatePoint(18.4861, -69.9312), 2000, Now);
        Guid areaId = merchant.ServiceAreas.Single().Id;

        Result result = merchant.RemoveServiceArea(areaId, Now);

        result.IsSuccess.Should().BeTrue();
        merchant.ServiceAreas.Should().BeEmpty();
    }

    private static Merchant Draft() => Merchant.CreateDraft(
        Guid.CreateVersion7(),
        "Mi negocio",
        Slug.FromTrusted("mi-negocio-area"),
        VerticalType.Food,
        CommissionPercentage.Default,
        Now);
}
