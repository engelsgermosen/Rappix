using FluentAssertions;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.Dispatch.Domain;
using Rappix.Dispatch.Domain.Couriers;
using Rappix.Dispatch.Domain.Couriers.Events;

namespace Rappix.Dispatch.Tests.Unit;

/// <summary>Pruebas unitarias de la maquina de estados del agregado CourierProfile.</summary>
public sealed class CourierProfileTests
{
    private static readonly DateTime Now = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly CourierId AnyCourier = CourierId.FromUserId(Guid.CreateVersion7());

    [Fact]
    public void CreateDraft_StartsOffline_WithoutVehicleOrLocation()
    {
        CourierProfile courier = CourierProfile.CreateDraft(AnyCourier, "Coco", Now);

        courier.Status.Should().Be(CourierStatus.Offline);
        courier.Vehicle.Should().BeNull();
        courier.LastLocation.Should().BeNull();
        courier.FirstName.Should().Be("Coco");
    }

    [Fact]
    public void GoOnline_WithoutVehicle_Fails()
    {
        CourierProfile courier = CourierProfile.CreateDraft(AnyCourier, "Coco", Now);

        Result result = courier.GoOnline(Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(CourierErrors.VehicleRequired);
        courier.Status.Should().Be(CourierStatus.Offline);
    }

    [Fact]
    public void GoOnline_WithVehicle_TransitionsAndRaisesEvent()
    {
        CourierProfile courier = WithVehicle();

        Result result = courier.GoOnline(Now);

        result.IsSuccess.Should().BeTrue();
        courier.Status.Should().Be(CourierStatus.Online);
        courier.DomainEvents.Should().ContainSingle();
        CourierStatusChangedDomainEvent changed = (CourierStatusChangedDomainEvent)courier.DomainEvents.Single();
        changed.FromStatus.Should().Be(CourierStatus.Offline);
        changed.ToStatus.Should().Be(CourierStatus.Online);
    }

    [Fact]
    public void GoOffline_WhenBusy_Fails()
    {
        CourierProfile courier = WithVehicle();
        courier.GoOnline(Now);
        courier.MarkBusy(Now);

        Result result = courier.GoOffline(Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(CourierErrors.BusyCannotGoOffline);
        courier.Status.Should().Be(CourierStatus.Busy);
    }

    [Fact]
    public void MarkBusy_FromOnline_Transitions()
    {
        CourierProfile courier = WithVehicle();
        courier.GoOnline(Now);

        Result result = courier.MarkBusy(Now);

        result.IsSuccess.Should().BeTrue();
        courier.Status.Should().Be(CourierStatus.Busy);
    }

    [Fact]
    public void MarkBusy_FromOffline_Fails()
    {
        CourierProfile courier = WithVehicle();

        Result result = courier.MarkBusy(Now);

        result.IsFailure.Should().BeTrue();
        courier.Status.Should().Be(CourierStatus.Offline);
    }

    [Fact]
    public void Release_FromBusy_GoesOnline()
    {
        CourierProfile courier = WithVehicle();
        courier.GoOnline(Now);
        courier.MarkBusy(Now);

        Result result = courier.Release(Now);

        result.IsSuccess.Should().BeTrue();
        courier.Status.Should().Be(CourierStatus.Online);
    }

    [Fact]
    public void Release_WhenNotBusy_IsNoOp()
    {
        CourierProfile courier = WithVehicle();
        courier.GoOnline(Now);

        Result result = courier.Release(Now);

        result.IsSuccess.Should().BeTrue();
        courier.Status.Should().Be(CourierStatus.Online);
    }

    [Fact]
    public void ReportLocation_PersistsAndRaisesEvent()
    {
        CourierProfile courier = WithVehicle();

        Result result = courier.ReportLocation(18.4861, -69.9312, Now);

        result.IsSuccess.Should().BeTrue();
        courier.LastLocation.Should().NotBeNull();
        courier.LastLocation!.Latitude.Should().Be(18.4861);
        courier.LastLocation.Longitude.Should().Be(-69.9312);
        courier.DomainEvents.Should().ContainSingle(domainEvent => domainEvent is CourierLocationReportedDomainEvent);
    }

    [Fact]
    public void ReportLocation_OutOfRange_Fails()
    {
        CourierProfile courier = CourierProfile.CreateDraft(AnyCourier, "Coco", Now);

        Result result = courier.ReportLocation(100, -69.9312, Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(CourierErrors.InvalidLocation);
        courier.LastLocation.Should().BeNull();
    }

    private static CourierProfile WithVehicle()
    {
        CourierProfile courier = CourierProfile.CreateDraft(AnyCourier, "Coco", Now);
        courier.SetVehicle(Vehicle.Create(VehicleType.Moto, "A123456", capacityKg: 20m).Value, Now);
        return courier;
    }
}
