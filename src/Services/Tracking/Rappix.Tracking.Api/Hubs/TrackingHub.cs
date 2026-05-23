using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Rappix.BuildingBlocks.WebApi.Authentication;
using Rappix.Tracking.Api.Hubs.Payloads;
using Rappix.Tracking.Application.Abstractions;

namespace Rappix.Tracking.Api.Hubs;

/// <summary>
/// Hub SignalR /hubs/tracking: el cliente se autentica con su JWT (bearer en header o
/// <c>?access_token=</c> en query para el handshake WebSocket) y se suscribe a su pedido. La
/// validacion de ownership compara <c>JWT.sub</c> con <c>OrderTracking.CustomerUserId</c>. Tras
/// suscribirse, recibe inmediatamente el snapshot (status actual + ultima ubicacion conocida) y
/// despues los pushes en vivo via los consumers.
/// </summary>
/// <remarks>
/// Diseno:
/// <list type="bullet">
/// <item><c>404</c> y <c>403</c> se devuelven con el mismo mensaje <c>"no autorizado"</c> — no se
///   revela si el pedido existe.</item>
/// <item>Reintento corto (3 × 500 ms) ante el race subscribe-vs-proyeccion de OrderSubmitted (la
///   inbox EF puede tardar 100-500 ms desde el publish).</item>
/// <item>El cliente debe invocar <see cref="Subscribe"/> explicitamente; no auto-join. Eso permite
///   suscripciones multiples por conexion y un Unsubscribe limpio.</item>
/// <item><c>OnDisconnectedAsync</c>: SignalR limpia los groups automaticamente al cerrar la
///   conexion, asi que no hace falta tracking manual.</item>
/// </list>
/// </remarks>
[Authorize]
public sealed partial class TrackingHub(IOrderTrackingReadRepository repository, ILogger<TrackingHub> logger)
    : Hub<ITrackingClient>
{
    private static readonly TimeSpan SnapshotRetryDelay = TimeSpan.FromMilliseconds(500);
    private const int SnapshotRetryCount = 3;

    /// <summary>
    /// Suscribe al cliente al grupo del pedido tras validar ownership. Envia inmediatamente el
    /// snapshot al caller.
    /// </summary>
    public async Task Subscribe(Guid orderId)
    {
        Guid? userId = Context.User?.GetUserId();
        if (userId is null)
        {
            LogMissingUserId(Context.ConnectionId);
            throw new HubException("no autorizado");
        }

        OrderTrackingSnapshot? snapshot = await TryGetSnapshotWithRetryAsync(orderId, Context.ConnectionAborted);
        if (snapshot is null || snapshot.CustomerUserId != userId.Value)
        {
            LogOwnershipDenied(Context.ConnectionId, userId.Value, orderId);
            throw new HubException("no autorizado");
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(orderId), Context.ConnectionAborted);

        // Snapshot inmediato: status + (si hay) ultima ubicacion conocida.
        await Clients.Caller.StatusChanged(new StatusChangedPayload(
            snapshot.CurrentStatus.ToString(),
            snapshot.UpdatedAtUtc,
            snapshot.StatusReason));

        if (snapshot.LastCourierLat is not null && snapshot.LastCourierLng is not null
            && snapshot.LastLocationAtUtc is not null && snapshot.LastCourierId is not null)
        {
            await Clients.Caller.LocationUpdated(new LocationUpdatedPayload(
                snapshot.LastCourierLat.Value,
                snapshot.LastCourierLng.Value,
                snapshot.LastLocationAtUtc.Value,
                snapshot.LastCourierId.Value));
        }

        LogSubscribed(Context.ConnectionId, userId.Value, orderId);
    }

    /// <summary>Saca al cliente del grupo del pedido (deja de recibir pushes).</summary>
    public Task Unsubscribe(Guid orderId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(orderId), Context.ConnectionAborted);

    /// <summary>Nombre del grupo SignalR para un pedido (formato estable; usado por el notifier).</summary>
    internal static string GroupName(Guid orderId) => $"order:{orderId:N}";

    private async Task<OrderTrackingSnapshot?> TryGetSnapshotWithRetryAsync(Guid orderId, CancellationToken cancellationToken)
    {
        // Reintento corto: cubre la carrera subscribe-vs-proyeccion de OrderSubmitted. Si tras 3
        // intentos (~1.5s total) sigue sin existir, devuelve null y el caller responde 401.
        for (int attempt = 0; attempt < SnapshotRetryCount; attempt++)
        {
            OrderTrackingSnapshot? snapshot = await repository.GetSnapshotAsync(orderId, cancellationToken);
            if (snapshot is not null)
            {
                return snapshot;
            }

            if (attempt < SnapshotRetryCount - 1)
            {
                try
                {
                    await Task.Delay(SnapshotRetryDelay, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    // Cliente se desconecto a mitad del reintento: salimos limpio sin lanzar.
                    return null;
                }
            }
        }
        return null;
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Warning,
        Message = "Subscribe sin claim sub valido (connectionId={ConnectionId}).")]
    private partial void LogMissingUserId(string connectionId);

    [LoggerMessage(EventId = 2, Level = LogLevel.Warning,
        Message = "Subscribe denegado por ownership (connectionId={ConnectionId}, userId={UserId}, orderId={OrderId}).")]
    private partial void LogOwnershipDenied(string connectionId, Guid userId, Guid orderId);

    [LoggerMessage(EventId = 3, Level = LogLevel.Information,
        Message = "Subscribe ok (connectionId={ConnectionId}, userId={UserId}, orderId={OrderId}).")]
    private partial void LogSubscribed(string connectionId, Guid userId, Guid orderId);
}
