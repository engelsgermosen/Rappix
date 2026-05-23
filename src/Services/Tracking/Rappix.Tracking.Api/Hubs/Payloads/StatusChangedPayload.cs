namespace Rappix.Tracking.Api.Hubs.Payloads;

/// <summary>
/// Payload del push <c>StatusChanged</c> que el hub envia al grupo del pedido. El status se serializa
/// como string (legibilidad cliente; mapeo desde <see cref="Domain.OrderTrackings.TrackingStatus"/>).
/// </summary>
/// <param name="Status">Nombre del estado actual (Placed, MerchantAccepted, CourierAssigned, Delivered, Cancelled, Failed).</param>
/// <param name="ChangedAtUtc">Timestamp del cambio (UTC).</param>
/// <param name="Reason">Razon textual cuando aplica (solo Cancelled/Failed).</param>
public sealed record StatusChangedPayload(
    string Status,
    DateTime ChangedAtUtc,
    string? Reason);
