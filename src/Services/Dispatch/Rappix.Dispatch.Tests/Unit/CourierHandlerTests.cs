using FluentAssertions;
using NSubstitute;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.BuildingBlocks.Core.Time;
using Rappix.Dispatch.Application.Abstractions;
using Rappix.Dispatch.Application.Couriers.Get;
using Rappix.Dispatch.Application.Couriers.GoOffline;
using Rappix.Dispatch.Application.Couriers.GoOnline;
using Rappix.Dispatch.Application.Couriers.ReportLocation;
using Rappix.Dispatch.Application.Couriers.UpdateVehicle;
using Rappix.Dispatch.Application.Responses;
using Rappix.Dispatch.Domain;
using Rappix.Dispatch.Domain.Couriers;

namespace Rappix.Dispatch.Tests.Unit;

/// <summary>
/// Pruebas de los handlers de self-service del courier con NSubstitute (sin DB, sin Redis real).
/// El comportamiento del aggregate se prueba aparte en CourierProfileTests.
/// </summary>
public sealed class CourierHandlerTests
{
    private static readonly DateTime Now = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task GetMyCourier_NotFound_Fails()
    {
        ICourierRepository couriers = Substitute.For<ICourierRepository>();
        couriers.GetByIdAsync(Arg.Any<CourierId>(), Arg.Any<CancellationToken>()).Returns((CourierProfile?)null);
        var handler = new GetMyCourierQueryHandler(couriers);

        Result<CourierResponse> result = await handler.Handle(new GetMyCourierQuery(Guid.CreateVersion7()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(CourierErrors.NotFound);
    }

    [Fact]
    public async Task UpdateVehicle_PersistsAndReturnsResponse()
    {
        Guid userId = Guid.CreateVersion7();
        CourierProfile courier = CourierProfile.CreateDraft(CourierId.FromUserId(userId), "Coco", Now);
        (ICourierRepository couriers, IUnitOfWork uow, IDateTimeProvider clock) = Mocks(courier);
        var handler = new UpdateVehicleCommandHandler(couriers, uow, clock);

        Result<CourierResponse> result = await handler.Handle(
            new UpdateVehicleCommand(userId, "Moto", "a123456", 20m), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        courier.Vehicle.Should().NotBeNull();
        courier.Vehicle!.Type.Should().Be(VehicleType.Moto);
        courier.Vehicle.Plate.Should().Be("A123456"); // trim+upper aplicado en Vehicle.Create
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GoOnline_WithoutVehicle_Fails_NoSave()
    {
        Guid userId = Guid.CreateVersion7();
        CourierProfile courier = CourierProfile.CreateDraft(CourierId.FromUserId(userId), "Coco", Now);
        (ICourierRepository couriers, IUnitOfWork uow, IDateTimeProvider clock) = Mocks(courier);
        IRedisGeoIndex geo = Substitute.For<IRedisGeoIndex>();
        var handler = new GoOnlineCommandHandler(couriers, geo, uow, clock);

        Result<CourierResponse> result = await handler.Handle(new GoOnlineCommand(userId), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(CourierErrors.VehicleRequired);
        await uow.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        await geo.DidNotReceive().AddOrUpdateAsync(Arg.Any<CourierId>(), Arg.Any<double>(), Arg.Any<double>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GoOnline_WithVehicleAndLocation_SavesAndAddsToRedis()
    {
        Guid userId = Guid.CreateVersion7();
        CourierProfile courier = CourierProfile.CreateDraft(CourierId.FromUserId(userId), "Coco", Now);
        courier.SetVehicle(Vehicle.Create(VehicleType.Moto, "A1", capacityKg: 10m).Value, Now);
        courier.ReportLocation(18.4861, -69.9312, Now);
        (ICourierRepository couriers, IUnitOfWork uow, IDateTimeProvider clock) = Mocks(courier);
        IRedisGeoIndex geo = Substitute.For<IRedisGeoIndex>();
        var handler = new GoOnlineCommandHandler(couriers, geo, uow, clock);

        Result<CourierResponse> result = await handler.Handle(new GoOnlineCommand(userId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        courier.Status.Should().Be(CourierStatus.Online);
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await geo.Received(1).AddOrUpdateAsync(courier.Id, 18.4861, -69.9312, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GoOnline_WithoutLocation_Saves_NoRedis()
    {
        // Permitido ir online sin location: aparece en Redis cuando reporte por primera vez.
        Guid userId = Guid.CreateVersion7();
        CourierProfile courier = CourierProfile.CreateDraft(CourierId.FromUserId(userId), "Coco", Now);
        courier.SetVehicle(Vehicle.Create(VehicleType.Bici, plate: null, capacityKg: null).Value, Now);
        (ICourierRepository couriers, IUnitOfWork uow, IDateTimeProvider clock) = Mocks(courier);
        IRedisGeoIndex geo = Substitute.For<IRedisGeoIndex>();
        var handler = new GoOnlineCommandHandler(couriers, geo, uow, clock);

        Result<CourierResponse> result = await handler.Handle(new GoOnlineCommand(userId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await geo.DidNotReceive().AddOrUpdateAsync(Arg.Any<CourierId>(), Arg.Any<double>(), Arg.Any<double>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GoOffline_WhenBusy_Fails_NoSave_NoRedisRemove()
    {
        Guid userId = Guid.CreateVersion7();
        CourierProfile courier = CourierProfile.CreateDraft(CourierId.FromUserId(userId), "Coco", Now);
        courier.SetVehicle(Vehicle.Create(VehicleType.Moto, "A1", capacityKg: 10m).Value, Now);
        courier.GoOnline(Now);
        courier.MarkBusy(Now);
        (ICourierRepository couriers, IUnitOfWork uow, IDateTimeProvider clock) = Mocks(courier);
        IRedisGeoIndex geo = Substitute.For<IRedisGeoIndex>();
        var handler = new GoOfflineCommandHandler(couriers, geo, uow, clock);

        Result<CourierResponse> result = await handler.Handle(new GoOfflineCommand(userId), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(CourierErrors.BusyCannotGoOffline);
        await uow.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        await geo.DidNotReceive().RemoveAsync(Arg.Any<CourierId>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GoOffline_FromOnline_SavesAndRemovesFromRedis()
    {
        Guid userId = Guid.CreateVersion7();
        CourierProfile courier = CourierProfile.CreateDraft(CourierId.FromUserId(userId), "Coco", Now);
        courier.SetVehicle(Vehicle.Create(VehicleType.Moto, "A1", capacityKg: 10m).Value, Now);
        courier.GoOnline(Now);
        (ICourierRepository couriers, IUnitOfWork uow, IDateTimeProvider clock) = Mocks(courier);
        IRedisGeoIndex geo = Substitute.For<IRedisGeoIndex>();
        var handler = new GoOfflineCommandHandler(couriers, geo, uow, clock);

        Result<CourierResponse> result = await handler.Handle(new GoOfflineCommand(userId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        courier.Status.Should().Be(CourierStatus.Offline);
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await geo.Received(1).RemoveAsync(courier.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReportLocation_WhenOnline_SavesAndUpdatesRedis()
    {
        Guid userId = Guid.CreateVersion7();
        CourierProfile courier = CourierProfile.CreateDraft(CourierId.FromUserId(userId), "Coco", Now);
        courier.SetVehicle(Vehicle.Create(VehicleType.Moto, "A1", capacityKg: 10m).Value, Now);
        courier.GoOnline(Now);
        (ICourierRepository couriers, IUnitOfWork uow, IDateTimeProvider clock) = Mocks(courier);
        IRedisGeoIndex geo = Substitute.For<IRedisGeoIndex>();
        var handler = new ReportLocationCommandHandler(couriers, geo, uow, clock);

        Result<CourierResponse> result = await handler.Handle(
            new ReportLocationCommand(userId, 18.5, -69.95), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await geo.Received(1).AddOrUpdateAsync(courier.Id, 18.5, -69.95, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReportLocation_WhenOffline_SavesButNotInRedis()
    {
        // El courier puede reportar location estando Offline (p. ej. la app sigue mandando heartbeat),
        // pero no entra al matching hasta ir Online.
        Guid userId = Guid.CreateVersion7();
        CourierProfile courier = CourierProfile.CreateDraft(CourierId.FromUserId(userId), "Coco", Now);
        (ICourierRepository couriers, IUnitOfWork uow, IDateTimeProvider clock) = Mocks(courier);
        IRedisGeoIndex geo = Substitute.For<IRedisGeoIndex>();
        var handler = new ReportLocationCommandHandler(couriers, geo, uow, clock);

        Result<CourierResponse> result = await handler.Handle(
            new ReportLocationCommand(userId, 18.5, -69.95), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await geo.DidNotReceive().AddOrUpdateAsync(Arg.Any<CourierId>(), Arg.Any<double>(), Arg.Any<double>(), Arg.Any<CancellationToken>());
    }

    private static (ICourierRepository couriers, IUnitOfWork uow, IDateTimeProvider clock) Mocks(CourierProfile courier)
    {
        ICourierRepository couriers = Substitute.For<ICourierRepository>();
        couriers.GetByIdAsync(Arg.Any<CourierId>(), Arg.Any<CancellationToken>()).Returns(courier);
        IUnitOfWork uow = Substitute.For<IUnitOfWork>();
        IDateTimeProvider clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(Now);
        return (couriers, uow, clock);
    }
}
