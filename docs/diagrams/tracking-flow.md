# Diagrama de flujo — Tracking (Fase 7)

Flujo end-to-end del push en vivo: desde el courier reportando ubicación hasta el cliente recibiendo `LocationUpdated`. Y la línea de tiempo de los `StatusChanged` desde `OrderSubmitted` hasta `Delivered`.

## Secuencia: cliente se suscribe + recibe pushes en vivo

```
Cliente              Tracking                Orders/Dispatch       RabbitMQ          Courier
  |                     |                            |                |                |
  | POST /orders        |                            |                |                |
  |---------------- (a Orders, no Tracking)--------->|                |                |
  |                     |                            |                |                |
  |                     |                            |- Publish OrderSubmitted ------->|
  |                     |<-- (consumer) ------------ |                |                |
  |                     | INSERT order_trackings(status=Placed)       |                |
  |                     |                            |                |                |
  | wss://.../hubs/tracking?access_token=<jwt>       |                |                |
  |------------------------------>|                  |                |                |
  | (handshake JWT, 200 OK)       |                  |                |                |
  | Invoke("Subscribe", orderId)  |                  |                |                |
  |------------------------------>|                  |                |                |
  |                     | Ownership check (JWT.sub == CustomerUserId) |                |
  |                     | reintento 3x500ms si row no existe          |                |
  |                     | Groups.AddToGroupAsync("order:{guid}")      |                |
  |                     | Clients.Caller.StatusChanged("Placed") <-----------(snapshot)|
  |<------ StatusChanged("Placed") -----------------|                  |                |
  |                     |                            |                |                |
  |                     | (cliente queda en el grupo)|                |                |
  |                     |                            |                |                |
  | POST .../accept     |                            |                |                |
  |---------------------(a Orders)------------------>|                |                |
  |                     |                            |- Publish OrderAccepted -------->|
  |                     |<-- (consumer) ------------ |                |                |
  |                     | UPDATE order_trackings(status=MerchantAccepted)              |
  |                     | Clients.Group(...).StatusChanged("MerchantAccepted")         |
  |<--- StatusChanged("MerchantAccepted") -----------|                |                |
  |                     |                            |                |                |
  |                     |     (saga avanza: AwaitingCourier)          |                |
  |                     |                            |- Publish CourierAssigned ------>|
  |                     |<-- (consumer) ------------ |                |                |
  |                     | UPDATE order_trackings(status=CourierAssigned, LastCourierId)|
  |                     | UPSERT courier_active_orders                |                |
  |                     | Clients.Group(...).StatusChanged("CourierAssigned")          |
  |<--- StatusChanged("CourierAssigned") ------------|                |                |
  |                     |                            |                |                |
  |                     |                            |                | POST /me/location
  |                     |                            |                |<---------------|
  |                     |                            |- Publish CourierLocationUpdated >
  |                     |<-- (consumer) ------------ |                |                |
  |                     | Lookup courier_active_orders by CourierId   |                |
  |                     | UPDATE order_trackings(LastCourier{Lat,Lng,At})              |
  |                     | Clients.Group(...).LocationUpdated(lat, lng, at, courierId)  |
  |<------ LocationUpdated(lat, lng, ...) -----------|                |                |
  |                     |                            |                |                |
  |                     |  (repetir LocationUpdated por cada POST /me/location)        |
  |                     |                            |                |                |
  | POST .../mark-delivered                          |                |                |
  |---------------------(a Orders)------------------>|                |                |
  |                     |                            |- Publish OrderDelivered ------->|
  |                     |                            |- Publish OrderCompleted ------->|
  |                     |<-- (terminal consumer, multi-IConsumer) --- |                |
  |                     | UPDATE order_trackings(status=Delivered)    |                |
  |                     | DELETE courier_active_orders WHERE OrderId  |                |
  |                     | Clients.Group(...).StatusChanged("Delivered")                |
  |<--- StatusChanged("Delivered") ------------------|                |                |
  |                     |  (OrderCompleted llega despues; ApplyDelivered es no-op,     |
  |                     |   wasTerminalBefore=true -> NO se reempuja al cliente)       |
```

## Ownership — el camino feliz y el camino bloqueado

```
Cliente A (JWT.sub=A)               Cliente B (JWT.sub=B)              Tracking
       |                                   |                              |
       | Subscribe(orderId_A)              |                              |
       |---------------------------------->|----------------------------->|
       |                                   |                              | Ownership: OK
       |                                   |                              | Groups.AddToGroupAsync
       |<-- StatusChanged(snapshot) -------|----------------------------- |
       |                                   |                              |
       |                                   | Subscribe(orderId_A)         |
       |                                   |----------------------------->|
       |                                   |                              | Ownership: FAIL
       |                                   |                              | (CustomerUserId != B)
       |                                   |<-- HubException("no autorizado")
       |                                   |                              |
       |                                   |          ... evento publicado al grupo order:orderId_A
       |<-- StatusChanged(x) --------------|----------------------------- |
       |                                   |       (B NO esta en el grupo, NO recibe)
```

Mismo error `"no autorizado"` para "no existe" y "no es tuyo" — el caller no distingue. Cubierto por test automatizado: `Subscribe_WhenNotOwner_ThrowsHubException_AndDoesNotReceivePushes`.

## Estados terminales (idempotencia)

`OrderDelivered` (Dispatch) y `OrderCompleted` (Orders) llegan en secuencia tras `mark-delivered`. Ambos pasan por `ApplyTerminalAsync(Delivered, ...)`:

- **Primer evento** (`OrderDelivered`): `wasTerminalBefore=false` → `ApplyDelivered` transiciona + `DELETE courier_active_orders` + `Clients.Group.StatusChanged("Delivered")`.
- **Segundo evento** (`OrderCompleted`): `wasTerminalBefore=true` → `ApplyDelivered` es no-op + `RemoveByOrderIdAsync` no-op (ya borrado) + **NO push** (log "idempotente").

Resultado: el cliente recibe `Delivered` una sola vez. Mismo patrón con `OrderCancelled` / `OrderFailed` (cada uno llega una sola vez por terminal de saga, pero la defensa en doble capa cubre redelivery RabbitMQ).
