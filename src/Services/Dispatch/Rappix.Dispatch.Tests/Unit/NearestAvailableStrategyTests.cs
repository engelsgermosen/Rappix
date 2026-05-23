using FluentAssertions;
using Rappix.Dispatch.Application.Couriers.Assignment;
using Rappix.Dispatch.Domain.Couriers;

namespace Rappix.Dispatch.Tests.Unit;

/// <summary>Pruebas de la strategy NearestAvailable: ranking respeta el orden ASC del GEOSEARCH.</summary>
public sealed class NearestAvailableStrategyTests
{
    [Fact]
    public async Task ChooseAsync_EmptyCandidates_ReturnsNoCandidates()
    {
        var strategy = new NearestAvailableStrategy();
        var context = new AssignmentContext(Guid.CreateVersion7(), Guid.CreateVersion7(), 0d, 0d, 0d, 0d);

        CourierAssignmentDecision decision = await strategy.ChooseAsync(context, [], CancellationToken.None);

        decision.RankedCandidates.Should().BeEmpty();
        decision.UnavailableReason.Should().Be("no_candidates");
    }

    [Fact]
    public async Task ChooseAsync_PreservesGeoSearchOrder()
    {
        // GEOSEARCH retorna ASC por distancia; la strategy NO reordena.
        var strategy = new NearestAvailableStrategy();
        var context = new AssignmentContext(Guid.CreateVersion7(), Guid.CreateVersion7(), 0d, 0d, 0d, 0d);
        CourierId nearest = CourierId.FromUserId(Guid.CreateVersion7());
        CourierId middle = CourierId.FromUserId(Guid.CreateVersion7());
        CourierId farthest = CourierId.FromUserId(Guid.CreateVersion7());
        CourierCandidate[] candidates =
        [
            new(nearest, 18.4861, -69.9312, DistanceMeters: 100d, VehicleType.Moto),
            new(middle, 18.49, -69.93, DistanceMeters: 500d, VehicleType.Bici),
            new(farthest, 18.50, -69.92, DistanceMeters: 1500d, VehicleType.Carro),
        ];

        CourierAssignmentDecision decision = await strategy.ChooseAsync(context, candidates, CancellationToken.None);

        decision.RankedCandidates.Should().Equal(nearest, middle, farthest);
        decision.UnavailableReason.Should().BeNull();
    }
}
