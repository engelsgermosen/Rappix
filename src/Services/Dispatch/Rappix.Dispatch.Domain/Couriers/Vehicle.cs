using Rappix.BuildingBlocks.Core.Results;

namespace Rappix.Dispatch.Domain.Couriers;

/// <summary>
/// Vehiculo del courier (value object). Mapeado como OwnsOne en el aggregate.
/// Plate y CapacityKg son opcionales (algunos couriers en Bici no llevan placa).
/// </summary>
public sealed record Vehicle
{
    /// <summary>Maximo de caracteres para la placa.</summary>
    public const int MaxPlateLength = 20;

    private Vehicle(VehicleType type, string? plate, decimal? capacityKg)
    {
        Type = type;
        Plate = plate;
        CapacityKg = capacityKg;
    }

    /// <summary>Tipo de vehiculo (Moto/Bici/Carro).</summary>
    public VehicleType Type { get; }

    /// <summary>Placa (opcional, hasta 20 chars).</summary>
    public string? Plate { get; }

    /// <summary>Capacidad de carga en Kg (opcional, > 0).</summary>
    public decimal? CapacityKg { get; }

    /// <summary>Construye un Vehicle validando placa y capacidad.</summary>
    public static Result<Vehicle> Create(VehicleType type, string? plate, decimal? capacityKg)
    {
        string? normalizedPlate = string.IsNullOrWhiteSpace(plate) ? null : plate.Trim().ToUpperInvariant();
        if (normalizedPlate is { Length: > MaxPlateLength })
        {
            return CourierErrors.InvalidVehiclePlate;
        }

        if (capacityKg is <= 0)
        {
            return CourierErrors.InvalidVehicleCapacity;
        }

        return new Vehicle(type, normalizedPlate, capacityKg);
    }
}
