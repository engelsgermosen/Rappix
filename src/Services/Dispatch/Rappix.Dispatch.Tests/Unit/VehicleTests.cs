using FluentAssertions;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.Dispatch.Domain;
using Rappix.Dispatch.Domain.Couriers;

namespace Rappix.Dispatch.Tests.Unit;

/// <summary>Pruebas del value object Vehicle (placa, capacidad, normalizacion).</summary>
public sealed class VehicleTests
{
    [Fact]
    public void Create_TrimsAndUppercasesPlate()
    {
        Result<Vehicle> result = Vehicle.Create(VehicleType.Moto, "  a123456 ", capacityKg: 20m);

        result.IsSuccess.Should().BeTrue();
        result.Value.Plate.Should().Be("A123456");
    }

    [Fact]
    public void Create_WithoutPlate_Accepts()
    {
        Result<Vehicle> result = Vehicle.Create(VehicleType.Bici, plate: null, capacityKg: null);

        result.IsSuccess.Should().BeTrue();
        result.Value.Plate.Should().BeNull();
        result.Value.CapacityKg.Should().BeNull();
    }

    [Fact]
    public void Create_PlateTooLong_Fails()
    {
        Result<Vehicle> result = Vehicle.Create(VehicleType.Carro, new string('A', Vehicle.MaxPlateLength + 1), capacityKg: 100m);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(CourierErrors.InvalidVehiclePlate);
    }

    [Fact]
    public void Create_NonPositiveCapacity_Fails()
    {
        Result<Vehicle> result = Vehicle.Create(VehicleType.Moto, "X1", capacityKg: 0m);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(CourierErrors.InvalidVehicleCapacity);
    }
}
