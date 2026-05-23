using Rappix.BuildingBlocks.Core.Domain;
using Rappix.BuildingBlocks.Core.Results;
using Rappix.Dispatch.Domain.Abstractions;
using Rappix.Dispatch.Domain.Couriers.Events;

namespace Rappix.Dispatch.Domain.Couriers;

/// <summary>
/// Aggregate raiz del courier. La propiedad <c>Id</c> es igual al UserId de Identity (1-1). El
/// CourierProfile arranca en <see cref="CourierStatus.Offline"/> al consumir UserRegistered, y el
/// courier completa su perfil via REST: PUT /me/vehicle, POST /me/location, POST /me/online.
/// </summary>
/// <remarks>
/// Reglas de transicion:
/// <list type="bullet">
/// <item>Offline -> Online exige <see cref="Vehicle"/> != null.</item>
/// <item>Online -> Offline siempre permitido.</item>
/// <item>Busy -> Offline PROHIBIDO (debe liberarse la asignacion primero).</item>
/// <item>Online -> Busy y Busy -> Online solo desde el flujo de asignacion (claim/release).</item>
/// </list>
/// La concurrencia optimista en Status la garantiza el predicado <c>WHERE Status = 'Online'</c>
/// del claim atomico (ExecuteUpdateAsync). xmin queda como token para los otros paths
/// (perfil/vehiculo/location) y para detectar carreras de profile-update.
/// </remarks>
public sealed class CourierProfile : AggregateRoot<CourierId>, IHasDomainEvents
{
    private CourierProfile()
    {
    }

    private CourierProfile(CourierId id, string firstName, DateTime createdAtUtc)
        : base(id)
    {
        FirstName = firstName;
        Status = CourierStatus.Offline;
        CreatedAtUtc = createdAtUtc;
    }

    /// <summary>Nombre del courier (cache del evento UserRegistered).</summary>
    public string FirstName { get; private set; } = null!;

    /// <summary>Estado de disponibilidad.</summary>
    public CourierStatus Status { get; private set; }

    /// <summary>Vehiculo declarado (null hasta PUT /me/vehicle).</summary>
    public Vehicle? Vehicle { get; private set; }

    /// <summary>Ultima ubicacion reportada (null hasta POST /me/location).</summary>
    public LastLocation? LastLocation { get; private set; }

    /// <summary>Momento de creacion (UTC).</summary>
    public DateTime CreatedAtUtc { get; private set; }

    /// <summary>Ultima actualizacion (UTC).</summary>
    public DateTime? UpdatedAtUtc { get; private set; }

    /// <summary>Crea un CourierProfile en estado Offline (lo invoca el consumer de UserRegistered).</summary>
    public static CourierProfile CreateDraft(CourierId id, string firstName, DateTime utcNow) =>
        new(id, firstName.Trim(), utcNow);

    /// <summary>Actualiza el vehiculo. Permitido en cualquier estado (incluso Busy: cambiar placa).</summary>
    public Result SetVehicle(Vehicle vehicle, DateTime utcNow)
    {
        Vehicle = vehicle;
        UpdatedAtUtc = utcNow;
        return Result.Success();
    }

    /// <summary>
    /// Reporta la ubicacion del courier. Eleva CourierLocationReported para que la capa de aplicacion
    /// publique CourierLocationUpdatedIntegrationEvent (Fase 7 Tracking lo consume).
    /// </summary>
    public Result ReportLocation(double latitude, double longitude, DateTime utcNow)
    {
        Result<LastLocation> result = LastLocation.Create(latitude, longitude, utcNow);
        if (result.IsFailure)
        {
            return Result.Failure(result.Error);
        }

        LastLocation = result.Value;
        UpdatedAtUtc = utcNow;
        RaiseDomainEvent(new CourierLocationReportedDomainEvent(Id, latitude, longitude, utcNow));
        return Result.Success();
    }

    /// <summary>
    /// Marca el courier como Online. Requiere Vehicle != null (sin vehiculo no puede operar).
    /// Permitido desde Offline o ya Online (no-op idempotente).
    /// </summary>
    public Result GoOnline(DateTime utcNow)
    {
        if (Vehicle is null)
        {
            return Result.Failure(CourierErrors.VehicleRequired);
        }

        if (Status == CourierStatus.Busy)
        {
            return Result.Failure(CourierErrors.InvalidTransition(Status, "ir online"));
        }

        return TransitionTo(CourierStatus.Online, utcNow);
    }

    /// <summary>
    /// Marca el courier como Offline. Prohibido si esta Busy (debe terminar/liberar la entrega).
    /// </summary>
    public Result GoOffline(DateTime utcNow)
    {
        if (Status == CourierStatus.Busy)
        {
            return Result.Failure(CourierErrors.BusyCannotGoOffline);
        }

        return TransitionTo(CourierStatus.Offline, utcNow);
    }

    /// <summary>
    /// Marca el courier como Busy (lo invoca el flujo de asignacion). NO usa este metodo el
    /// claim atomico de produccion (ExecuteUpdateAsync con WHERE Status='Online' lo hace en
    /// una sola instruccion para evitar TOCTOU); este existe para el dominio puro y los tests.
    /// </summary>
    public Result MarkBusy(DateTime utcNow)
    {
        if (Status != CourierStatus.Online)
        {
            return Result.Failure(CourierErrors.InvalidTransition(Status, "asignar pedido"));
        }

        return TransitionTo(CourierStatus.Busy, utcNow);
    }

    /// <summary>
    /// Libera al courier de su asignacion (Busy -> Online). Idempotente: si ya estaba Online u Offline, no-op.
    /// </summary>
    public Result Release(DateTime utcNow)
    {
        if (Status != CourierStatus.Busy)
        {
            return Result.Success();
        }

        return TransitionTo(CourierStatus.Online, utcNow);
    }

    private Result TransitionTo(CourierStatus to, DateTime utcNow)
    {
        if (Status == to)
        {
            UpdatedAtUtc = utcNow;
            return Result.Success();
        }

        CourierStatus from = Status;
        Status = to;
        UpdatedAtUtc = utcNow;
        RaiseDomainEvent(new CourierStatusChangedDomainEvent(Id, from, to, utcNow));
        return Result.Success();
    }
}
