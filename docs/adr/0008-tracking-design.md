# ADR 0008 — Diseño del Tracking Service (push en vivo al cliente vía SignalR)

- **Estado:** Aceptado
- **Fecha:** 2026-05-23
- **Contexto de fase:** Fase 7 (Tracking Service)

## Contexto

Tracking transmite al **cliente final**, en tiempo real, la **ubicación del courier asignado** + el **estado del pedido en vivo**. Es la contracara de Dispatch (Fase 6): Dispatch consume la ubicación del courier para **asignar**; Tracking la **proyecta y empuja al cliente**. Todos los eventos necesarios ya existen como `IntegrationEvent` post-Fase 6: `OrderSubmitted`, `OrderAccepted`, `OrderCompleted`, `OrderCancelled`, `OrderFailed` (Orders); `CourierAssigned`, `CourierLocationUpdated`, `OrderDelivered` (Dispatch). Tracking **solo consume y empuja** — no publica al bus, no expone gRPC en Fase 7.

Este ADR registra las decisiones no obvias.

## Decisiones

### 1. Read model dedicado, no consultar Orders/Dispatch al vuelo

Cada `Subscribe` y cada push necesita resolver `OrderId → estado actual + última ubicación + ownership`. Hacerlo por gRPC contra Orders/Dispatch en cada evento (cientos por entrega) cargaría a esos servicios sin valor; además expone el flujo al fallo de un cliente gRPC.

Tracking mantiene un **read model local** (`OrderTracking` por pedido + `CourierActiveOrder` por courier asignado) en `rappix_tracking`, alimentado por los consumers MassTransit. Lecturas son PK lookups locales. **Origen de verdad** sigue siendo Orders/Dispatch — Tracking solo proyecta.

### 2. `CourierId` como PK del mapping (tabla separada), no columna en `OrderTracking`

El hot path es `CourierLocationUpdatedConsumer`: el evento trae `CourierId`, no `OrderId`. El lookup crítico es `CourierId → OrderId activo`.

- **Opción descartada:** columna `LastCourierId` en `OrderTracking` + índice secundario. Lookup inverso fuerza otra tabla efectiva (el index) y obliga a invalidar en updates de status.
- **Adoptada:** tabla `CourierActiveOrder` con `CourierId` PK + unique index sobre `OrderId`. Lookup PK del consumer + refuerzo del invariante 1↔1 que Dispatch ya garantiza con su unique partial index en `courier_assignments`. Los consumers terminales hacen DELETE por `OrderId` (no `ReleasedAtUtc IS NULL`); Tracking proyecta solo el estado vigente, Dispatch ya audita.

### 3. Ownership por `JWT.sub == OrderTracking.CustomerUserId` con 404/403 indistinguibles

El cliente NO debe poder enumerar pedidos ajenos. Tanto el hub (`Subscribe`) como el endpoint REST (`GET`) validan que `JWT.sub == OrderTracking.CustomerUserId` y devuelven el mismo error `Tracking.OrderTracking.NotFound` para "no existe" y "no es tuyo". El test `Subscribe_WhenNotOwner_ThrowsHubException_AndDoesNotReceivePushes` cubre la garantía automáticamente: cliente A intenta suscribirse al pedido de B → `HubException("no autorizado")`. Posteriores publishes al grupo NO llegan al attacker (no se unió). Es el checkpoint crítico de la fase.

### 4. `TrackingStatus` enum de baja granularidad — no se modifica Orders

La saga de Orders publica HOY: `OrderSubmitted`, `OrderAccepted`, `OrderCompleted`, `OrderCancelled`, `OrderFailed`. Internamente transita `ValidatingQuote → ReservingStock → AwaitingMerchant → AwaitingPayment → AwaitingCourier → Committing → InProgress → Completed`, pero esos estados intermedios **no salen al bus**. Tracking queda con 6 estados visibles:

```
Placed → MerchantAccepted → CourierAssigned → Delivered / Cancelled / Failed
```

- **Opción evaluada:** añadir `OrderStatusChangedIntegrationEvent` que la saga publique en cada transición. Cambio mínimo a `OrderStateMachine`, pero modifica un servicio ya estable (Orders) + tests de Orders + ADR-0006 update; aumenta el scope de Fase 7. Aplazada hasta que el negocio pida granularidad mayor (Preparing/PickedUp/InTransit).
- **Adoptada:** Tracking solo consume lo que ya sale al bus. Documentado como follow-up.

### 5. Sin Redis en Fase 7 (independencia operacional)

Dispatch usa Redis para Geo + IdempotencyMiddleware (POST mutantes). Tracking expone solo:

- `GET /api/v1/tracking/orders/{orderId}` (no muta).
- Hub SignalR (no es REST con header `Idempotency-Key`).

El único consumidor de `IDistributedCache` en `BuildingBlocks.WebApi` es el `IdempotencyMiddleware`; sin POSTs mutantes, no se invoca `app.UseIdempotency()`, no se necesita `AddDistributedCache`. Tampoco se usa Geo. Tampoco backplane Redis para SignalR (single instance en Fase 7). **Resultado:** Tracking no referencia paquetes Redis ni declara `depends_on: redis`. Si en futuro se añaden POSTs mutantes idempotentes o se escala out a múltiples réplicas, se registra Redis en ese commit aislado.

### 6. JWT en query string para el handshake WebSocket

WebSockets en navegador no permite header `Authorization` en el upgrade. El cliente SignalR pasa el JWT como `?access_token=...` automáticamente. El server lo pesca en `JwtBearerEvents.OnMessageReceived` **solo si el path empieza con `/hubs/tracking`** — mitiga leaks por logs, referer y proxies en endpoints REST.

### 7. SignalR strongly-typed clients (`Hub<ITrackingClient>`)

`ITrackingClient` declara `LocationUpdated(LocationUpdatedPayload)` y `StatusChanged(StatusChangedPayload)`. Elimina strings frágiles, mantiene la API tipada en los notifiers, y simplifica los tests cliente (no `On<dynamic>`; usar records concretos evita problemas de deserialización camelCase/PascalCase con `On<dynamic>` en `SignalR.Client`).

### 8. Reintento corto subscribe-vs-proyección (3×500ms)

Hay un race: el cliente conecta inmediatamente después de `POST /orders`, antes de que el consumer `OrderSubmitted` haya proyectado `OrderTracking` (~100-500ms). El hub reintenta 3 veces × 500ms en `Subscribe`; si tras 1.5s sigue null → `HubException("no autorizado")` (mismo mensaje que ownership, no revela existencia). Doble red: el cliente puede hacer `GET /api/v1/tracking/orders/{id}` como warm-up antes del `Subscribe`. El loop usa `Context.ConnectionAborted` + `catch OperationCanceledException` → cliente desconectado a mitad sale limpio sin excepción colgada.

### 9. Idempotencia en doble capa: inbox EF + aggregate `Apply*` no-op

`AddConfigureEndpointsCallback(... UseEntityFrameworkOutbox<TrackingDbContext>)` aplica el filtro de outbox EF a cada endpoint. El **inbox** dedupica por `MessageId` (aunque Tracking no publica al bus, el inbox SÍ aplica — defensa contra redelivery RabbitMQ). Como segunda red, cada `Apply*` del aggregate es no-op si el estado destino ya está alcanzado: `ApplyDelivered` siendo llamado por `OrderDelivered` y luego por `OrderCompleted` solo aplica una vez; `ApplyLocation` descarta updates con timestamp anterior (evita pintar saltos al cliente).

### 10. `CourierAvailabilityChangedIntegrationEvent` NO se consume en Fase 7

Avisar al cliente "courier offline" suena útil — pero el courier puede ir Offline 30s al entrar en un túnel y volver. Sin threshold, el cliente recibe ruido. Cuando el negocio lo pida, se añade un consumer con threshold (>60s sin pings → "courier offline"). En Fase 7 el cliente nota la desconexión por ausencia de pings (timeout client-side).

## Consecuencias

### Positivas

- Read model dedicado: lectura rápida, sin cargar a Orders/Dispatch.
- Tracking independiente de Redis (~1 dependencia operacional menos).
- Ownership cubierto por test automatizado; el resto del flujo cubierto por smoke E2E real.
- Patrón directo de Dispatch reutilizado en proyectos, DI, persistence, outbox, Dockerfile, compose, tests.

### Negativas (y mitigaciones)

- **Timeline baja granularidad.** Sin Preparing/PickedUp/InTransit visibles. Mitigación: documentado, pendiente la decisión del negocio.
- **Sin revocación inmediata de JWT mid-WS.** La conexión activa sigue aunque el token se revoque; en `reconnect` falla. Aceptable Fase 7; un futuro endpoint admin podría llamar `IHubContext.Clients.User(userId).Aborted()` si se necesita inmediato.
- **Deuda de test de los 4 SKIP en `TrackingHubConnectionTests`.** Los tests de live location push, status change push, race subscribe-vs-proyección y unsubscribe quedan marcados `[Fact(Skip = …)]` con razón documentada: el flujo `Publish bus → consumer real → IClientNotifier → SignalR LongPolling → cliente` es frágil en `TestServer` por el dispatch a `Clients.Group` vía LongPolling (verificado: el consumer ejecuta y proyecta a la BD, pero el push al cliente del test no llega). **Mitigación**:
  1. **Primer tramo (evento → consumer → Push) está cubierto** por los integration tests de los consumers con `IClientNotifier` substituto (commits 6-8): proyección correcta, Push invocado con args correctos, idempotencia.
  2. **Segundo tramo (Push → SignalR → cliente) está cubierto por el smoke E2E automatizado** (`tools/smoke-tracking-e2e.ps1`): cliente real (`Rappix.Tracking.SmokeClient`) recibe los 6 pushes en orden vía RabbitMQ real + SignalR sobre `tracking-api` en Docker. Evidencia con timestamps de UNA corrida real está en el commit que registra la automatización.
  3. **Ownership** queda como único test automatizado del hub, pero es el checkpoint crítico de la fase (privacidad).

  Cuando el harness de SignalR + LongPolling sea más estable (Microsoft.AspNetCore.SignalR.Client futuro), reintentar automatizar los 4 SKIP es seguro.

## Follow-ups (no en Fase 7)

- `OrderStatusChangedIntegrationEvent` publicado por la saga en cada transición → enriquece la timeline (Preparing/PickedUp/InTransit).
- `CourierAvailabilityChangedIntegrationEvent` consumer con threshold (>60s sin pings).
- gRPC `TrackingGrpc.StreamOrderTracking` en puerto 5017 (clientes B2B / panel admin).
- Endpoint `GET /api/v1/tracking/me/active` (listar pedidos activos del cliente).
- SignalR Redis backplane cuando se escale a múltiples réplicas (1 línea + 1 paquete; `Microsoft.AspNetCore.SignalR.StackExchangeRedis` ya está en CPM listo).
- Housekeeping: purgar `order_trackings` con `current_status IN (Delivered, Cancelled, Failed)` y `updated_at_utc < now() - 30 days` (Hangfire futuro).
- Revocación inmediata de JWT mid-WS (close connection on revoke event).
- Automatizar los 4 SKIP de `TrackingHubConnectionTests` cuando el harness sea estable.
