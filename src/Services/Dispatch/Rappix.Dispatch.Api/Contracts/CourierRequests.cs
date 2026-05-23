namespace Rappix.Dispatch.Api.Contracts;

/// <summary>Cuerpo del PUT /me/vehicle.</summary>
public sealed record UpdateVehicleRequest(string VehicleType, string? Plate, decimal? CapacityKg);

/// <summary>Cuerpo del POST /me/location.</summary>
public sealed record ReportLocationRequest(double Latitude, double Longitude);
