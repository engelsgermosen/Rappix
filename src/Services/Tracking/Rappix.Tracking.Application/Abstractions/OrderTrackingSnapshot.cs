using Rappix.Tracking.Domain.OrderTrackings;

namespace Rappix.Tracking.Application.Abstractions;

/// <summary>
/// Snapshot inmutable del read model para hidratar al cliente al suscribirse al hub o consultar
/// el endpoint REST. Se proyecta con <c>AsNoTracking()</c> desde EF — separar del aggregate evita
/// fugas del change-tracker en el hot path de lectura.
/// </summary>
public sealed record OrderTrackingSnapshot(
    Guid OrderId,
    Guid CustomerUserId,
    Guid MerchantId,
    TrackingStatus CurrentStatus,
    string? StatusReason,
    Guid? LastCourierId,
    double? LastCourierLat,
    double? LastCourierLng,
    DateTime? LastLocationAtUtc,
    double PickupLat,
    double PickupLng,
    double DeliveryLat,
    double DeliveryLng,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);
