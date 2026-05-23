# Tracking (Fase 7) — Setup, smoke E2E y clientes de ejemplo

Rappix.Tracking transmite al cliente final, en tiempo real, la ubicación del courier asignado a su pedido + el estado del pedido en vivo. La proyección de los eventos de Orders y Dispatch a un read model autorizado por ownership, expuesta por dos superficies:

- **WebSocket / SignalR** — push en vivo en `/hubs/tracking` (puerto host **5007**).
- **REST snapshot** — `GET /api/v1/tracking/orders/{orderId}` (puerto host **5007**) como fallback cuando WS no conecta.

Reservado: puerto host **5017** (container 8081, HTTP/2 h2c) para un futuro `TrackingGrpc` (streaming server-side). Sin servicio gRPC expuesto en Fase 7.

---

## 1. Arrancar el servicio

```powershell
# Stack completo (Identity, Merchants, Catalog, Pricing, Orders, Dispatch, Tracking + infra):
docker compose up -d --build

# Solo Tracking (asume el resto ya levantado):
docker compose up -d --build tracking-api
```

Verifica que arrancó:

```powershell
docker ps --filter "name=rappix-tracking"
docker logs rappix-tracking --tail 30
# Esperado:
#   Now listening on: http://[::]:8080
#   Now listening on: http://[::]:8081
#   Bus started: rabbitmq://rabbitmq:5672/
#   Application started
curl http://localhost:5007/health
# {"service":"tracking","status":"ok"}
```

---

## 2. Snapshot REST (fallback)

```powershell
# Reemplaza $ORDER_ID y $CUSTOMER (JWT) con valores reales.
Invoke-RestMethod -Method Get `
  -Uri "http://localhost:5007/api/v1/tracking/orders/$ORDER_ID" `
  -Headers @{ Authorization = "Bearer $CUSTOMER" }
```

Respuesta (200) cuando el cliente es el dueño:

```json
{
  "orderId": "0193...-...-...-...",
  "status": "CourierAssigned",
  "statusReason": null,
  "lastLocation": { "lat": 18.487, "lng": -69.932, "reportedAtUtc": "2026-05-23T01:21:00Z" },
  "courierId": "0193...-...-...-...",
  "pickup":   { "lat": 18.490, "lng": -69.940 },
  "delivery": { "lat": 18.486, "lng": -69.931 },
  "createdAtUtc": "...",
  "updatedAtUtc": "..."
}
```

Respuesta cuando el pedido NO existe o NO es del cliente: **404** con `Tracking.OrderTracking.NotFound` (mismo error para no filtrar existencia).

---

## 3. Hub SignalR — cliente .NET

El proyecto `tools/Rappix.Tracking.SmokeClient` es un cliente listo para usar:

```powershell
dotnet run --project tools/Rappix.Tracking.SmokeClient -- "<ORDER_ID>" "<JWT>"
# opcional: tercer argumento = baseUrl (default http://localhost:5007)
```

Salida típica:

```
[smoke] Conectando a http://localhost:5007/hubs/tracking para orderId=...
[smoke] Conectado (ConnectionId=...). Invocando Subscribe(...)...
[smoke] Subscribe OK. Escuchando pushes (Ctrl+C para salir).
[smoke] StatusChanged   status=Placed              changedAtUtc=2026-05-23T01:20:55Z reason=(no reason)
[smoke] StatusChanged   status=MerchantAccepted   changedAtUtc=2026-05-23T01:21:02Z reason=(no reason)
[smoke] StatusChanged   status=CourierAssigned   changedAtUtc=2026-05-23T01:21:10Z reason=(no reason)
[smoke] LocationUpdated lat=18.487 lng=-69.932 reportedAtUtc=2026-05-23T01:21:15Z courierId=...
...
[smoke] StatusChanged   status=Delivered          changedAtUtc=2026-05-23T01:25:00Z reason=(no reason)
```

Acceso por JWT en query string (necesario porque el handshake WebSocket en navegador NO permite header Authorization). El cliente SignalR concatena automáticamente `?access_token=...`; el servidor lo pesca SOLO en el path `/hubs/tracking` (mitigación de leaks).

### Snippet propio (.NET)

```csharp
using Microsoft.AspNetCore.SignalR.Client;

var connection = new HubConnectionBuilder()
    .WithUrl("http://localhost:5007/hubs/tracking", options =>
    {
        options.AccessTokenProvider = () => Task.FromResult<string?>(jwt);
    })
    .WithAutomaticReconnect()
    .Build();

connection.On<dynamic>("LocationUpdated", payload =>
    Console.WriteLine($"loc: {payload.latitude}, {payload.longitude}"));

connection.On<dynamic>("StatusChanged", payload =>
    Console.WriteLine($"status: {payload.status}"));

await connection.StartAsync();
await connection.InvokeAsync("Subscribe", orderId);
```

### Snippet TypeScript (browser / Node)

```typescript
import { HubConnectionBuilder, HttpTransportType } from "@microsoft/signalr";

const connection = new HubConnectionBuilder()
  .withUrl(`http://localhost:5007/hubs/tracking`, {
    accessTokenFactory: () => jwt,
  })
  .withAutomaticReconnect()
  .build();

connection.on("LocationUpdated", payload => console.log("loc", payload));
connection.on("StatusChanged", payload => console.log("status", payload));

await connection.start();
await connection.invoke("Subscribe", orderId);
```

---

## 4. Smoke E2E completo (el cierre de Fase 7)

El push en vivo (segundo tramo `IClientNotifier → SignalR → cliente`) **no** está cubierto por tests automatizados (limitación del TestServer + LongPolling con SignalR Groups). El smoke E2E ES la verificación.

### Smoke automatizado (recomendado)

`tools/smoke-tracking-e2e.ps1` automatiza el flujo entero (setup + crear pedido + lanzar el smoke-client en background + accept + 2 location updates + mark-delivered + lectura de pushes recibidos). Requiere `Orders__Simulation__DeliveryDelayMs` ≥ 20000 en `docker-compose.yml` (ya en main; con delay <1s el simulated delivery cierra la saga antes de que las ubicaciones lleguen).

```powershell
docker compose down -v
docker compose up -d --build
./tools/smoke-tracking-e2e.ps1
```

Salida esperada (6 pushes en orden, con timestamps reales del smoke):

```
[smoke] StatusChanged   status=Placed             changedAtUtc=...11.872Z  reason=(no reason)
[smoke] StatusChanged   status=MerchantAccepted   changedAtUtc=...21.918Z
[smoke] StatusChanged   status=CourierAssigned    changedAtUtc=...22.209Z
[smoke] LocationUpdated lat=18.487  lng=-69.932   reportedAtUtc=...29.917Z  courierId=...
[smoke] LocationUpdated lat=18.4875 lng=-69.9325  reportedAtUtc=...32.994Z  courierId=...
[smoke] StatusChanged   status=Delivered          changedAtUtc=...36.012Z
```

Si el smoke automatizado pasa con los 6 pushes en orden, los 4 tests SKIP en `TrackingHubConnectionTests` (limitación de TestServer + SignalR Groups por LongPolling) quedan **cubiertos por evidencia del flujo real**.

### Smoke manual (paso a paso)

### Pasos

1. **Levantar la stack limpia**:
   ```powershell
   docker compose down -v
   docker compose up -d --build
   ```
2. **Sembrar datos** (registra cliente + merchant + courier + crea cotización):
   ```powershell
   . ./tools/seed-smoke.ps1
   ```
   Anota los valores impresos: `$CUSTOMER`, `$MERCHANT`, `$COURIER`, `$ORDER_ID` (lo obtienes en el siguiente paso).
3. **Crear el pedido** (arranca la saga):
   ```powershell
   $order = Invoke-RestMethod -Method Post -Uri http://localhost:5005/api/v1/orders `
     -Headers @{ Authorization = "Bearer $CUSTOMER"; "Content-Type"="application/json"; "Idempotency-Key"="smoke-001" } `
     -Body (@{ quoteId = "$QUOTE_ID"; street="Calle Test 123"; reference="Apto 1"; latitude=18.4861; longitude=-69.9312 } | ConvertTo-Json)
   $ORDER_ID = $order.orderId
   ```
4. **En otra terminal**, arranca el cliente smoke del hub:
   ```powershell
   dotnet run --project tools/Rappix.Tracking.SmokeClient -- "$ORDER_ID" "$CUSTOMER"
   # Imprime: StatusChanged status=Placed (el snapshot)
   ```
5. **Merchant acepta** (en la terminal principal):
   ```powershell
   Invoke-RestMethod -Method Post -Uri "http://localhost:5005/api/v1/orders/$ORDER_ID/accept" `
     -Headers @{ Authorization = "Bearer $MERCHANT" }
   # En el smoke-client llega: StatusChanged status=MerchantAccepted
   # Despues, automaticamente, Dispatch asigna el courier:
   # En el smoke-client llega: StatusChanged status=CourierAssigned
   ```
6. **Courier reporta ubicación** (varias veces):
   ```powershell
   Invoke-RestMethod -Method Post -Uri "http://localhost:5006/api/v1/couriers/me/location" `
     -Headers @{ Authorization = "Bearer $COURIER"; "Content-Type"="application/json" } `
     -Body (@{ latitude = 18.4870; longitude = -69.9320 } | ConvertTo-Json)
   # En el smoke-client llega: LocationUpdated lat=18.487 lng=-69.932 ...
   ```
7. **Marcar entregado**:
   ```powershell
   Invoke-RestMethod -Method Post -Uri "http://localhost:5005/api/v1/orders/$ORDER_ID/mark-delivered" `
     -Headers @{ Authorization = "Bearer $MERCHANT" }
   # En el smoke-client llega: StatusChanged status=Delivered
   ```

### Test de privacidad (en paralelo)

Con un JWT de OTRO cliente (no `$CUSTOMER`), invoca el smoke-client al mismo `$ORDER_ID`:

```powershell
dotnet run --project tools/Rappix.Tracking.SmokeClient -- "$ORDER_ID" "<JWT-DE-OTRO-USER>"
# Debe imprimir: [smoke] Fallo conectar o suscribir: HubException: no autorizado
# (Misma respuesta para "no existe el pedido" y "no es tuyo" — no se revela existencia.)
```

---

## 5. Eventos consumidos (referencia)

| De Orders | De Dispatch |
|---|---|
| `OrderSubmittedIntegrationEvent` → status `Placed`, captura pickup/delivery | `CourierAssignedIntegrationEvent` → status `CourierAssigned` + crea mapping courier→order |
| `OrderAcceptedIntegrationEvent` → status `MerchantAccepted` | `CourierLocationUpdatedIntegrationEvent` → push `LocationUpdated` al grupo |
| `OrderCompletedIntegrationEvent` → status `Delivered` (idempotente con OrderDelivered) | `OrderDeliveredIntegrationEvent` → status `Delivered` (ancla de negocio) |
| `OrderCancelledIntegrationEvent` → status `Cancelled` + reason | |
| `OrderFailedIntegrationEvent` → status `Failed` + reason | |

Tracking **NO** publica al bus en Fase 7 — solo consume y empuja al cliente.

---

## 6. Troubleshooting

- **`401 Unauthorized` en el handshake**: el JWT no es válido. Verifica `IDENTITY_JWT_SIGNINGKEY` (compartido entre Identity y Tracking en `.env`).
- **`HubException: no autorizado`**: el `JWT.sub` no coincide con `OrderTracking.CustomerUserId`. Reasignación de roles o token de otro usuario.
- **`HubException: no autorizado` justo después de crear el pedido**: race con la proyección de `OrderSubmitted` (~100-500ms). El hub reintenta 3×500ms automáticamente; si tras 1.5s sigue null, el cliente debe reintentar `Subscribe`.
- **Sin `LocationUpdated` aunque el courier reporta**: verifica que el courier esté **asignado** al pedido. `CourierLocationUpdatedConsumer` ignora silenciosamente los reportes de couriers en idle.
- **Logs Seq**: filtra por `Service = "tracking"` para ver consumers + hub subscribe/unsubscribe.
