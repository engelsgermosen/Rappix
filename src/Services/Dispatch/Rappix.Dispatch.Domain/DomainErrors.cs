using Rappix.BuildingBlocks.Core.Results;
using Rappix.Dispatch.Domain.Couriers;

namespace Rappix.Dispatch.Domain;

/// <summary>Errores del agregado CourierProfile y sus value objects.</summary>
public static class CourierErrors
{
    /// <summary>Courier no encontrado.</summary>
    public static readonly Error NotFound =
        Error.NotFound("Dispatch.Courier.NotFound", "Courier no encontrado.");

    /// <summary>El courier ya existe (el consumer de UserRegistered es idempotente).</summary>
    public static readonly Error AlreadyExists =
        Error.Conflict("Dispatch.Courier.AlreadyExists", "El courier ya existe.");

    /// <summary>Intento de ir Online sin haber configurado vehiculo.</summary>
    public static readonly Error VehicleRequired =
        Error.Validation("Dispatch.Courier.VehicleRequired", "Configura tu vehiculo (PUT /me/vehicle) antes de ir online.");

    /// <summary>Intento de ir Offline estando Busy (tiene un pedido asignado).</summary>
    public static readonly Error BusyCannotGoOffline =
        Error.Conflict("Dispatch.Courier.BusyCannotGoOffline", "No puedes desconectarte mientras tengas un pedido asignado.");

    /// <summary>Coordenadas fuera de rango.</summary>
    public static readonly Error InvalidLocation =
        Error.Validation("Dispatch.Courier.InvalidLocation", "La ubicacion reportada esta fuera de rango.");

    /// <summary>Placa con formato invalido o demasiado larga.</summary>
    public static readonly Error InvalidVehiclePlate =
        Error.Validation("Dispatch.Courier.InvalidVehiclePlate", $"La placa puede tener hasta {Vehicle.MaxPlateLength} caracteres.");

    /// <summary>Capacidad no positiva.</summary>
    public static readonly Error InvalidVehicleCapacity =
        Error.Validation("Dispatch.Courier.InvalidVehicleCapacity", "La capacidad debe ser positiva.");

    /// <summary>Transicion de estado no permitida en el estado actual.</summary>
    public static Error InvalidTransition(CourierStatus from, string action) =>
        Error.Conflict("Dispatch.Courier.InvalidTransition", $"No puedes {action} con el estado {from}.");
}

/// <summary>Errores relacionados con las asignaciones.</summary>
public static class CourierAssignmentErrors
{
    /// <summary>No hay asignacion activa para el pedido (el caller debe tratar como no-op).</summary>
    public static readonly Error NoActiveAssignment =
        Error.NotFound("Dispatch.Assignment.NoActiveAssignment", "No hay asignacion activa para este pedido.");
}
