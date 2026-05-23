using Rappix.Tracking.Application.Abstractions;
using Rappix.Tracking.Domain.OrderTrackings;

namespace Rappix.Tracking.Application.OrderTrackings.Responses;

/// <summary>
/// Snapshot del tracking devuelto por el REST. <c>Status</c> serializado como string (legibilidad
/// cliente). <c>lastLocation</c> y <c>courierId</c> nullable hasta que haya un courier asignado.
/// </summary>
public sealed record OrderTrackingResponse(
    Guid OrderId,
    string Status,
    string? StatusReason,
    LastLocationResponse? LastLocation,
    Guid? CourierId,
    CoordinateResponse Pickup,
    CoordinateResponse Delivery,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc)
{
    /// <summary>Construye la respuesta desde el snapshot del repo de lectura.</summary>
    public static OrderTrackingResponse From(OrderTrackingSnapshot snapshot)
    {
        LastLocationResponse? lastLocation =
            snapshot.LastCourierLat is not null && snapshot.LastCourierLng is not null && snapshot.LastLocationAtUtc is not null
                ? new LastLocationResponse(
                    snapshot.LastCourierLat.Value,
                    snapshot.LastCourierLng.Value,
                    snapshot.LastLocationAtUtc.Value)
                : null;

        return new OrderTrackingResponse(
            snapshot.OrderId,
            snapshot.CurrentStatus.ToString(),
            snapshot.StatusReason,
            lastLocation,
            snapshot.LastCourierId,
            new CoordinateResponse(snapshot.PickupLat, snapshot.PickupLng),
            new CoordinateResponse(snapshot.DeliveryLat, snapshot.DeliveryLng),
            snapshot.CreatedAtUtc,
            snapshot.UpdatedAtUtc);
    }
}

/// <summary>Ultima ubicacion conocida del courier asignado.</summary>
public sealed record LastLocationResponse(double Lat, double Lng, DateTime ReportedAtUtc);

/// <summary>Coordenada lat/lng para pickup y delivery.</summary>
public sealed record CoordinateResponse(double Lat, double Lng);
