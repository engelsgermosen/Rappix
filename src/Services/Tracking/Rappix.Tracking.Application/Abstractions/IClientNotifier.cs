namespace Rappix.Tracking.Application.Abstractions;

/// <summary>
/// Empuja actualizaciones en vivo al cliente final. La implementacion SignalR vive en la capa Api
/// (depende de Microsoft.AspNetCore.SignalR), por lo que los consumers de Infrastructure se
/// acoplan solo a esta abstraccion. Las llamadas son "fire-and-forget desde el grupo": si nadie
/// esta suscrito al grupo del orderId, el push es no-op (no error).
/// </summary>
public interface IClientNotifier
{
    /// <summary>Empuja una actualizacion de ubicacion del courier al grupo del pedido.</summary>
    Task PushLocation(
        Guid orderId,
        double latitude,
        double longitude,
        DateTime reportedAtUtc,
        Guid courierId,
        CancellationToken cancellationToken);

    /// <summary>Empuja un cambio de estado al grupo del pedido.</summary>
    Task PushStatus(
        Guid orderId,
        string status,
        DateTime changedAtUtc,
        string? reason,
        CancellationToken cancellationToken);
}
