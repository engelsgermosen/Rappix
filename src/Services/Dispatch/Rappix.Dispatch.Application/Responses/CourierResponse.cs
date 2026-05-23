using Rappix.Dispatch.Domain.Couriers;

namespace Rappix.Dispatch.Application.Responses;

/// <summary>Proyeccion publica del courier (GET /me).</summary>
public sealed record CourierResponse(
    Guid CourierId,
    string FirstName,
    string Status,
    VehicleResponse? Vehicle,
    LocationResponse? LastLocation,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc)
{
    /// <summary>Proyecta el agregado.</summary>
    public static CourierResponse From(CourierProfile courier) => new(
        CourierId: courier.Id.Value,
        FirstName: courier.FirstName,
        Status: courier.Status.ToString(),
        Vehicle: courier.Vehicle is null
            ? null
            : new VehicleResponse(courier.Vehicle.Type.ToString(), courier.Vehicle.Plate, courier.Vehicle.CapacityKg),
        LastLocation: courier.LastLocation is null
            ? null
            : new LocationResponse(courier.LastLocation.Latitude, courier.LastLocation.Longitude, courier.LastLocation.ReportedAtUtc),
        CreatedAtUtc: courier.CreatedAtUtc,
        UpdatedAtUtc: courier.UpdatedAtUtc);
}

/// <summary>Proyeccion del vehiculo.</summary>
public sealed record VehicleResponse(string Type, string? Plate, decimal? CapacityKg);

/// <summary>Proyeccion de la ultima ubicacion reportada.</summary>
public sealed record LocationResponse(double Latitude, double Longitude, DateTime ReportedAtUtc);

/// <summary>Proyeccion de la asignacion activa del courier (GET /me/current-assignment).</summary>
public sealed record CurrentAssignmentResponse(
    Guid AssignmentId,
    Guid OrderId,
    DateTime AssignedAtUtc);
