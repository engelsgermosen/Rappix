# ADR 0007 — Diseño del Dispatch Service (matching geo de couriers, claim atómico)

- **Estado:** Aceptado
- **Fecha:** 2026-05-22
- **Contexto de fase:** Fase 6 (Dispatch Service)

## Contexto

Dispatch asigna couriers a pedidos. Su rol en la saga de Orders ya estaba reservado: la saga publica
`CourierRequestedIntegrationEvent` y espera `CourierAssigned` o `CourierUnavailable`. Fase 5 dejó un
`SimulatedCourierResponder` detrás del flag `Orders:EnableSimulatedResponders` para no bloquear el E2E.
Fase 6 construye el servicio real: choreography para el registro de couriers (igual que Merchants),
matching geoespacial con Redis Geo, claim atómico ante concurrencia, y liberación idempotente en los
eventos terminales del pedido. Reusa la plantilla madura (CQRS, Result, EF outbox, doble puerto Kestrel,
building blocks) y añade Redis como pieza nueva. Este ADR registra las decisiones no triviales.

## Decisiones

### 1. `PickupLocation` es campo de primera clase en `Merchant`, propagado por el evento

`CourierRequestedIntegrationEvent` necesita las coords desde dónde recoge el courier. Dos opciones se
descartaron:

- **Derivar de `ServiceArea`** (zona de entrega): semánticamente incorrecto. El centro del polígono o
  el `Center` del círculo NO es la dirección física del comercio; puede estar a cientos de metros del
  pickup real. Además: un merchant puede tener múltiples ServiceAreas, ¿cuál centroid?
- **Llamar gRPC a Merchants desde Dispatch**: añade un cliente gRPC más al servicio, una dependencia
  de red en el hot path de asignación, y un fallback que complica el flujo (¿qué hace Dispatch si
  Merchants está abajo? Publicar `CourierUnavailable` con razón "merchant_unreachable" es peor que
  no publicar nada).

**Decisión:** `Merchant.PickupLocation` (Point geography 4326, nullable hasta el primer set). El owner
lo fija en `PUT /api/v1/merchants/me/pickup-location`. `SubmitForApproval` lo exige (correctness:
sin pickup el merchant no puede recibir pedidos). Orders lo resuelve en `PlaceOrder` vía la gRPC ya
existente (`MerchantBasicInfoResponse` extendido) y lo congela en `Order.PickupLatitude/Longitude`. La
saga lo propaga: `OrderSubmittedIntegrationEvent → OrderState → CourierRequestedIntegrationEvent`.
Dispatch lee del evento, **cero clientes gRPC**.

Backfill en la migration `AddMerchantPickupLocation`: para merchants existentes copia `ST_Centroid` de
la primera `ServiceArea`. Es un default razonable; el owner puede sobrescribir vía REST.

### 2. Redis Geo para matching + rehidratación al arranque

Redis tiene primitivas geoespaciales nativas (`GEOADD`, `GEORADIUS`, `GEOSEARCH`) sobre sorted sets con
geohash interno. Para `N` couriers la búsqueda por radio es O(log N + M) con M = resultados. Comparado
con PostGIS + GiST sobre la propia BD de Dispatch:

- **Redis Geo** es 10-100× más rápido en el hot path (matching corre por cada `CourierRequested`).
- **Redis Geo** desacopla el matching del DbContext (no compite con la transacción del claim).
- **PostGIS** sería mejor para queries complejas (intersecciones de polígonos, etc.); aquí solo
  necesitamos "couriers en un radio". Sobrante.

**Decisión:** Redis Geo key `dispatch:couriers:geo`. Solo couriers `Online` con `LastLocation` están
ahí. Ciclo de vida:

- `GoOnline` (con location conocida): `GEOADD`.
- `ReportLocation` (Online): `GEOADD` (sobrescribe).
- Claim → `Busy`: `ZREM` (no aparece en futuras búsquedas).
- Release (terminal) → `Online`: `GEOADD` de vuelta si tiene location.
- `GoOffline`: `ZREM`.

**Recovery:** un `RedisGeoRehydrationService` (`IHostedService`) recorre la BD al arranque y rellena
el set. Cubre Redis flush, restart sin persistencia, o desincronización. Corre **después** de las
migraciones EF (las aplica `Program.cs` antes de `app.Run()`) y antes de que el bus reciba mensajes.

### 3. Claim atómico vía `ExecuteUpdateAsync` con `WHERE Status='Online'`

El matching termina con un `UPDATE courier_profiles SET Status='Busy' WHERE Id=@id AND Status='Online'
RETURNING ...`. Dos consumers compitiendo por el mismo courier: solo uno actualiza la fila (`rows == 1`);
el otro recibe `0` y prueba con el siguiente candidato del ranking. El predicado `Status='Online'` **es
el concurrency token**, colapsando el TOCTOU entre "leer Online" y "marcar Busy" en una sola sentencia
SQL sin necesidad de `xmin` para este hot path. xmin queda como token para los otros paths (perfil,
vehículo, location).

**Red de seguridad en BD:** dos unique partial indexes en `courier_assignments`:
- `UNIQUE (courier_id) WHERE released_at_utc IS NULL` — un courier solo puede tener una asignación
  activa.
- `UNIQUE (order_id) WHERE released_at_utc IS NULL` — un pedido solo puede tener una asignación
  activa.

Si la lógica de aplicación llegara a romperse (refactor futuro, bug, race no contemplado), la BD lo
catch ea con `UniqueViolation` antes de que se cree estado inconsistente. El test de concurrencia
(`TwoOrders_OneCourier_ExactlyOneAssigned_OtherUnavailable`) ejercita este camino en Postgres real.

### 4. `ICourierAssignmentStrategy` devuelve un RANKING, no una elección única

El orchestrator (`CourierRequestedConsumer`) itera el ranking intentando claim atómico hasta que uno
gana. Esto deja el seam listo para una futura `OfferBasedStrategy` (oferta tipo Uber) sin tocar el
orchestrator:

- **Fase 6 — `NearestAvailableStrategy`:** devuelve los candidatos en el orden ASC por distancia
  que ya trae `GEOSEARCH`. Claim al primero que esté disponible. Latencia mínima.
- **Futuro — `OfferBasedStrategy`:** ventana Quartz de N segundos; ofrece a top-K couriers en
  paralelo (publica `CourierOffered` → Tracking notifica al courier); el primer accept gana via el
  mismo claim atómico. Si nadie acepta dentro de la ventana, re-oferta a top-K siguiente. Encaja en
  el mismo contrato `ChooseAsync(ctx, candidates) -> RankedCandidates`.

Swap a `OfferBasedStrategy` = una línea en `AddDispatchApplication` cuando llegue.

### 5. Liberación en los 3 eventos terminales (no solo `OrderDelivered`)

`OrderTerminalEventsConsumer` libera al courier al consumir cualquiera de `OrderDelivered`,
`OrderCancelled`, `OrderFailed`. Razones:

- **`OrderDelivered`** es el anclaje de NEGOCIO ("el courier dejó el pedido"). Es lo que SEMÁNTICAMENTE
  libera al courier.
- **`OrderCompleted`** (publicado al final de la saga) sería frágil: si Fase 7 añade un estado
  intermedio (p. ej. ventana de rating antes de Completed), el courier quedaría Busy minutos extra.
- **`OrderCancelled`/`OrderFailed`** cubren el caso de cancelación o fallo TRAS asignar (saga en
  `CompensatingStock` o `Failed`). Sin estos consumers el courier quedaría Busy sin asignación viva.

El consumer es idempotente: `GetActiveByOrderAsync` returns null → no-op (cubre el caso "cancelado
antes de AwaitingCourier"). El inbox EF de MassTransit dedupea por messageId; doble entrega es seguro.

### 6. `CourierProfile.Id = UserId` (1-1 con Identity)

A diferencia de Merchants (donde un user PODRÍA tener varios merchants en el futuro), un usuario
solo es un courier. No hay razón para un `CourierId` separado del `UserId` del JWT. Simplifica el
modelo: los endpoints `/api/v1/couriers/me` resuelven directamente a `CourierProfile.Id =
principal.GetUserId()`, sin tabla de mapping. La choreography (`UserRegisteredConsumer` filtra
`UserType="Courier"`) crea el `CourierProfile` con `Id = UserId` directamente.

`CourierId` sigue siendo un strongly-typed record struct (consistente con `OrderId`/`MerchantId`/etc.)
con su `ValueConverter` para EF.

### 7. Sin Quartz / reaper en Fase 6 — follow-up explícito para Fase 7

Existe UN hueco conocido: si Dispatch crashea entre el `ExecuteUpdate` (claim atómico, courier marca
Busy) y el `SaveChanges` (insert assignment + outbox publish), el courier quedaría Busy en BD sin
una asignación viva. En la práctica:

- MassTransit reintentaría el mensaje en otro consumer instance → el `idempotency guard` no encuentra
  asignación activa → re-ejecuta el flujo → otro courier gana el claim → el primer courier sigue Busy.

**Mitigación en Fase 6:** la transacción EF del SaveChanges es corta (~ms), el riesgo es bajo, y
no hay corrupción de datos (solo un courier "perdido" que requiere intervención manual o un release
forzado).

**Solución propuesta en Fase 7:** un `StaleAssignmentReaper` job Quartz cada N minutos que detecte
couriers con `Status=Busy` cuya última asignación esté en estado inválido (sin row activa
o con `AssignedAtUtc + maxAge < now AND ReleasedAtUtc IS NULL`) y libere idempotentemente. Encaja
sobre la misma infra que Orders usa hoy para timeouts de saga (Quartz + Postgres persistente).

Esto NO entra en Fase 6 para mantener el scope. Documentado aquí como follow-up explícito.

### 8. Eliminación del `SimulatedCourierResponder` de Orders

Borrado limpio en commit 3 de esta fase (combinado con la propagación de pickup): archivo,
registro DI, opciones `CourierOutcome`/`CourierDelayMs`, y env vars de compose. **NO se dejó un flag
para volver atrás**: el patrón de flag-soup (`EnableSimulatedResponders` controlando 3 simuladores
distintos) era pragmático en Fase 5 pero ahora oculta más de lo que ayuda.

`SimulatedPaymentResponder` y `SimulatedDeliveryResponder` se quedan hasta que lleguen Payments (Fase 8)
y Tracking (Fase 7 confirmará entrega vía Dispatch real). El flag `Orders:EnableSimulatedResponders`
sigue activo controlando esos dos.

Tests in-memory de la saga (`OrderSagaTests`) que dependían del simulador se refactorizaron a publicar
`CourierAssignedIntegrationEvent` / `CourierUnavailableIntegrationEvent` manualmente vía
`harness.Bus.Publish(...)` para ejercer las transiciones — no se vaciaron, mantienen cobertura.

### 9. Puerto gRPC 8081 reservado, sin servicio en Fase 6

Dispatch configura los 2 puertos Kestrel cleartext (8080 REST + 8081 HTTP/2) igual que el resto de
servicios (ADR-0003), pero NO expone ningún servicio gRPC. Esto reserva el puerto para Fase 7
(Tracking necesitará consultar la última ubicación del courier vía gRPC, posiblemente con
streaming server-side). Sin reservarlo ahora habría que reabrir el contenedor en Fase 7 con
downtime; reservarlo es gratis.

## Consecuencias

### Positivas

- **Saga de Orders intacta:** el contrato de `CourierRequested/Assigned/Unavailable` no cambia, solo
  el publisher de las respuestas.
- **PickupLocation reutilizable:** Pricing podrá usarla en una futura fase (delivery fee distance-based)
  y Tracking en Fase 7 (origen del trayecto en el mapa).
- **Claim atómico simple y verificable:** una sola sentencia SQL, test de concurrencia contra
  Postgres real que falla loud si alguien quita el predicado.
- **Strategy con seam claro:** `OfferBasedStrategy` se mete sin refactor del orchestrator.

### Negativas

- **Dos sources of truth** para el estado de couriers Online: Redis Geo (matching) + Postgres
  (canonical). La rehidratación al arranque y los handlers que actualizan ambos lados en orden
  (DB primero, Redis después) mantienen consistencia eventual; un Redis caído no rompe el servicio
  (logs + reconciliación con el próximo report).
- **El hueco crash-entre-claim-y-publish** existe (sección 7). Probabilidad muy baja, impacto
  limitado, mitigación cubierta para Fase 7.

### Neutras (follow-up explícito)

- `StaleAssignmentReaper` Quartz job en Fase 7 (sección 7).
- gRPC server en Dispatch para Tracking en Fase 7 (sección 9): `GetCourierLocation`, posiblemente
  un streaming `WatchCourier` para el cliente.
- Métricas de matching (radio promedio, tiempo de claim, ratio assigned/unavailable) en Fase 9
  cuando llegue Observability.
