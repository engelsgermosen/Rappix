using Microsoft.AspNetCore.SignalR;
using Rappix.Tracking.Api.Hubs.Payloads;
using Rappix.Tracking.Application.Abstractions;

namespace Rappix.Tracking.Api.Hubs;

/// <summary>
/// Implementacion SignalR de <see cref="IClientNotifier"/>: enruta los pushes al grupo del pedido
/// via <see cref="IHubContext{THub, T}"/>. Si nadie esta suscrito al grupo, el push es no-op
/// silencioso (no error).
/// </summary>
/// <remarks>
/// Vive en la capa Api (la unica que depende de Microsoft.AspNetCore.SignalR), y se registra como
/// singleton en <c>Program.cs</c> — <see cref="IHubContext{THub, T}"/> es thread-safe.
/// </remarks>
internal sealed class SignalRClientNotifier(IHubContext<TrackingHub, ITrackingClient> hub) : IClientNotifier
{
    public Task PushLocation(
        Guid orderId,
        double latitude,
        double longitude,
        DateTime reportedAtUtc,
        Guid courierId,
        CancellationToken cancellationToken) =>
            hub.Clients.Group(TrackingHub.GroupName(orderId))
                .LocationUpdated(new LocationUpdatedPayload(latitude, longitude, reportedAtUtc, courierId));

    public Task PushStatus(
        Guid orderId,
        string status,
        DateTime changedAtUtc,
        string? reason,
        CancellationToken cancellationToken) =>
            hub.Clients.Group(TrackingHub.GroupName(orderId))
                .StatusChanged(new StatusChangedPayload(status, changedAtUtc, reason));
}
