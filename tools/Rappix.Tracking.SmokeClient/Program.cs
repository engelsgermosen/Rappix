using Microsoft.AspNetCore.SignalR.Client;

// Cliente smoke del Hub /hubs/tracking. Lo usa el smoke E2E de Fase 7 para confirmar que un cliente
// conectado recibe StatusChanged y LocationUpdated en vivo cuando los consumers empujan al grupo.
//
// Uso:
//   dotnet run --project tools/Rappix.Tracking.SmokeClient -- <orderId> <jwt> [trackingUrl]
//
// Default trackingUrl = http://localhost:5007 (puerto host del docker-compose).
// El cliente queda escuchando indefinidamente — corta con Ctrl+C.

if (args.Length < 2)
{
    Console.WriteLine("Uso: dotnet run --project tools/Rappix.Tracking.SmokeClient -- <orderId> <jwt> [trackingUrl=http://localhost:5007]");
    return 1;
}

string orderIdRaw = args[0];
string jwt = args[1];
string baseUrl = args.Length >= 3 ? args[2].TrimEnd('/') : "http://localhost:5007";

if (!Guid.TryParse(orderIdRaw, out Guid orderId))
{
    Console.Error.WriteLine($"orderId no es un GUID valido: {orderIdRaw}");
    return 2;
}

string hubUrl = $"{baseUrl}/hubs/tracking";
Console.WriteLine($"[smoke] Conectando a {hubUrl} para orderId={orderId}...");

HubConnection connection = new HubConnectionBuilder()
    .WithUrl(hubUrl, options =>
    {
        // El cliente pasa el JWT en la query string del handshake. SignalR.Client lo concatena cuando
        // no se puede usar header Authorization (handshake WebSocket en navegador). El servidor lo
        // pesca via JwtBearerEvents.OnMessageReceived filtrado al path /hubs/tracking.
        options.AccessTokenProvider = () => Task.FromResult<string?>(jwt);
    })
    .WithAutomaticReconnect()
    .Build();

// Records que matchean ITrackingClient del server. Las propiedades son PascalCase (System.Text.Json
// con camelCase por default mapea Latitude<->latitude automaticamente).
connection.On<LocationPayload>("LocationUpdated", payload =>
{
    Console.WriteLine($"[smoke] LocationUpdated  lat={payload.Latitude}  lng={payload.Longitude}  " +
        $"reportedAtUtc={payload.ReportedAtUtc:o}  courierId={payload.CourierId}");
});

connection.On<StatusPayload>("StatusChanged", payload =>
{
    string reason = payload.Reason is null ? "(no reason)" : payload.Reason;
    Console.WriteLine($"[smoke] StatusChanged   status={payload.Status}  changedAtUtc={payload.ChangedAtUtc:o}  reason={reason}");
});

connection.Closed += async exception =>
{
    Console.Error.WriteLine($"[smoke] Conexion cerrada: {exception?.Message}");
    await Task.CompletedTask;
};

try
{
    await connection.StartAsync();
    Console.WriteLine($"[smoke] Conectado (ConnectionId={connection.ConnectionId}). Invocando Subscribe({orderId})...");
    await connection.InvokeAsync("Subscribe", orderId);
    Console.WriteLine($"[smoke] Subscribe OK. Escuchando pushes (Ctrl+C para salir).");
}
catch (Exception ex)
{
    Console.Error.WriteLine($"[smoke] Fallo conectar o suscribir: {ex.GetType().Name}: {ex.Message}");
    return 3;
}

// Espera indefinida; cualquier StatusChanged / LocationUpdated se imprime arriba.
await Task.Delay(Timeout.Infinite);
return 0;

internal sealed record LocationPayload(double Latitude, double Longitude, DateTime ReportedAtUtc, Guid CourierId);
internal sealed record StatusPayload(string Status, DateTime ChangedAtUtc, string? Reason);
