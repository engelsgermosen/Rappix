# ADR 0006 — Diseño del Orders Service (saga orquestada, reserva de stock, compensaciones)

- **Estado:** Aceptado
- **Fecha:** 2026-05-22
- **Contexto de fase:** Fase 5 (Orders Service)

## Contexto

Orders es el servicio más complejo del proyecto: arma el pedido (snapshot inmutable del quote) y orquesta un
flujo distribuido entre Pricing, Catalog y los futuros Payments (Fase 8) y Dispatch (Fase 6), con
compensaciones cuando algo falla. Reaplica la plantilla de los servicios previos (CQRS, Result, outbox,
doble puerto Kestrel, building blocks) y añade la mecánica de saga. Este ADR registra las decisiones no
triviales.

## Decisiones

### 1. Saga ORQUESTADA (MassTransit State Machine), no coreografía

El flujo del pedido tiene **orden estricto, timeouts y compensaciones en orden inverso**. Una coreografía
(cada servicio reacciona a eventos) dispersaría esa lógica y haría invisible el estado global. Se eligió una
**saga orquestada** con `OrderStateMachine : MassTransitStateMachine<OrderState>`: un único lugar describe
estados, transiciones, timeouts y compensaciones. El estado (`OrderState`) se persiste con el repositorio EF
Core de MassTransit en `rappix_orders`, en el **mismo DbContext** que el outbox para que el estado de la saga
y los mensajes publicados se confirmen en una sola transacción.

- **Estados:** `ValidatingQuote → ReservingStock → AwaitingMerchant → AwaitingPayment → AwaitingCourier →
  Committing → InProgress → Completed`, más `CompensatingStock`/`CompensatingQuote` y terminales
  `Cancelled`/`Failed`/`NeedsReview`.
- **La saga solo reacciona a mensajes.** Los efectos gRPC viven en consumers "activity" (ConsumeQuote,
  ReserveStock, CommitStock, ReleaseStock, RevertQuote) que ejecutan la llamada y publican el evento-resultado.
  Esto aísla la I/O de la saga (Clean Architecture) y la hace testeable con el Test Harness + mocks NSubstitute.
- **Separación Order vs OrderState:** `Order` es el agregado de dominio (lo que ven cliente/merchant: snapshot,
  desglose, estado espejo); `OrderState` es la mecánica de la saga. Una proyección
  (`OrderStatusProjectionConsumer`) sincroniza el estado de la saga al agregado.

### 2. Reserva de stock REAL en Catalog (Opción A), no decremento optimista

La saga necesita **apartar** stock (hold) y luego confirmarlo o liberarlo. Se extendió Catalog con un patrón
de reserva real: `StockLevel.ReservedQuantity` (con `Available = Quantity − Reserved`), un agregado
`StockReservation` (un hold por `(OrderId, ItemId)`, con TTL) y un gRPC `StockReservationService`
(`ReserveStock`/`CommitStock`/`ReleaseStock`, idempotentes, todo-o-nada). Frente al decremento directo +
compensación, esto evita la ventana en la que el stock está "tomado" sin confirmar y permite responder
"cuánto hay reservado". Cambios additivos: los 43 tests de Catalog siguen verdes (Reserved default 0 ⇒
comportamiento idéntico sin reservas). Un hold abandonado se libera por **TTL** (barrido perezoso al reservar);
el TTL (default 30 min) supera la duración máxima de la saga para que un pedido en curso nunca pierda su hold.

### 3. Consumir quote ANTES de reservar stock + reversión real

La saga **consume la cotización primero** (congela precio y redime cupón) y luego reserva stock. Es una
decisión consciente: valida el precio vigente y el cupón antes de tocar inventario. Su coste es que cualquier
fallo posterior debe **revertir el consumo**, así que se implementó `RevertQuoteConsumption` real en Pricing
(`Quote.Revert` Consumed→Active, `Coupon.UnRedeem`, `CouponRedemption.RevertedAtUtc` conservando auditoría).
La alternativa (consumir al final) evitaría la reversión pero introduce problemas peores: la quote podría
expirar a mitad de la saga (su vida de 10 min ≈ la suma de timeouts) forzando un reembolso tras cobrar, y dos
pedidos con un cupón de un solo uso podrían colisionar después del pago. Consume-early + revert es el menor mal.

Para idempotencia ante reentregas: `ConsumeQuote` lleva `OrderId` y es idempotente (mismo pedido → no-op
exitoso; otro pedido → conflicto). Orders arma el snapshot del pedido con un nuevo gRPC `GetQuote`.

### 4. Scheduler de timeouts: Quartz + Postgres, no RabbitMQ delayed exchange

Los timeouts de la saga (merchant 5 min, payment 2 min, courier 3 min; configurables en `Orders:Timeouts:*`)
se programan con MassTransit `Schedule`. Se eligió **Quartz con job store Postgres** sobre el RabbitMQ delayed
message exchange porque: (a) la imagen pinned `rabbitmq:3.13-management-alpine` **no** trae el plugin de
delayed exchange; (b) sus mensajes diferidos **no son durables** ante un reinicio del broker, lo que dejaría
pedidos con timeouts que nunca disparan. Quartz reutiliza el Postgres que ya usamos, persiste los triggers y
sobrevive reinicios. Wiring: `AddPublishMessageScheduler` + `AddQuartzConsumers` + `UsePublishMessageScheduler`,
con `UsePersistentStore(UsePostgres)`. Las **tablas `qrtz_*`** se crean en la migración inicial de Orders
(SQL crudo, esquema estándar de Quartz.NET 3.x) para que migrate-on-startup provisione todo. En **tests**, el
Test Harness usa su scheduler in-memory y los timeouts se bajan a sub-segundo.

### 5. Compensaciones en orden inverso, con un matiz importante en el commit

Las compensaciones deshacen en orden inverso, con confirmación: reembolso (si se cobró) → liberar stock →
revertir quote. Cada rama de fallo fija un `CompensationTerminal` (Cancelled o Failed) y atraviesa
`CompensatingStock → CompensatingQuote`. Matiz clave: **`StockCommitFailed` tras un pago exitoso NUNCA
auto-reembolsa.** La reserva (no el commit) ya garantiza el inventario; el commit solo lo convierte de
apartado a descontado y solo puede fallar por infra transitoria (cubierta por el retry del bus). Tras los
reintentos se marca `NeedsReview` conservando el dinero y se deja para reconciliación manual: auto-reembolsar
un pedido pagado y con courier por un blip de commit sería estrictamente peor. La cancelación del cliente se
maneja en **todos** los estados de espera (cancelando el timeout armado); pasado `Committing`, el endpoint
devuelve 409. Las carreras de eventos tardíos (reserva o pago que llegan después de empezar a compensar) se
manejan explícitamente; el resto de duplicados se ignoran (entrega at-least-once).

### 6. Responders de pago/courier/entrega SIMULADOS y enchufables

Payments (Fase 8) y Dispatch (Fase 6) aún no existen. Orders publica los **contratos reales**
(`PaymentRequested`, `CourierRequested`, `RefundRequested` en `Rappix.Contracts.Payments`/`.Dispatch`) y unos
**responders simulados** (`SimulatedPaymentResponder`, `SimulatedCourierResponder`, `SimulatedDeliveryResponder`)
los atienden, configurables vía `Orders:Simulation:*` (Success | Fail/Unavailable | Timeout, con delays). Viven
en `Infrastructure/Messaging/Simulation/` detrás del flag `EnableSimulatedResponders`: cuando lleguen los
servicios reales se apagan/borran sin tocar la saga, porque consumen y emiten los mismos contratos. Para que la
demo llegue a `Completed` sin Dispatch, el responder de entrega reacciona al estado `InProgress` y publica
`OrderDelivered`; además hay un endpoint **temporal** `POST /orders/{id}/mark-delivered` (seam de Dispatch,
se elimina en Fase 6).

### 7. Concurrencia de la saga: xmin optimista

`OrderState` usa el token `xmin` de PostgreSQL como concurrencia optimista (igual que el resto de agregados).
Ante eventos concurrentes del mismo pedido (p. ej. cancel + timeout), el perdedor recibe un conflicto y el bus
reintenta contra el estado actualizado. Se prefirió a la concurrencia pesimista (SELECT FOR UPDATE), que
alargaría la transacción sobre las tablas de inbox/outbox.

### 8. Autorización del merchant por OwnerUserId (no por MerchantId)

El `Order` guarda el `MerchantId` (Id de la entidad Merchant), pero el `sub` del JWT de un merchant es su
`OwnerUserId` (usuario de Identity que administra el comercio): son entidades distintas, así que comparar
`MerchantId == sub` siempre falla (un smoke lo confirmó con un 403 indebido). Decisión (Opción B): al **crear**
el pedido, Orders resuelve el merchant vía el gRPC de Merchants (`GetMerchantBasicInfo`, extendido para
devolver `owner_user_id`) y **persiste `Order.MerchantOwnerUserId`**. Los endpoints de merchant (accept/reject)
comparan el `sub` del JWT contra ese campo y `GET /orders/merchant/pending` filtra por él. Se eligió persistir
(vs resolver `userId→merchantId` en cada accept) para no añadir un round-trip gRPC en el hot path; el costo es
una columna + una resolución en la creación del pedido (que además valida que el merchant exista). Esto cierra
la simplificación "identidad del merchant = sub del JWT" que se había documentado como caveat.

## Consecuencias

- **Positivas:** un único punto describe el flujo distribuido; compensaciones reales (stock liberado, quote y
  cupón revertidos); timeouts durables; los contratos de Payments/Dispatch quedan definidos y probados con
  simuladores; Catalog gana un patrón de reserva reutilizable.
- **Negativas / costes:** la saga es la pieza más delicada (un error se propaga); se tocaron Catalog, Pricing y
  Merchants (additivo, sin romper sus suites); Quartz añade tablas `qrtz_*` y configuración.
- **TODO futuro:** sustituir los responders simulados por Payments (Fase 8) y Dispatch (Fase 6) reales; gRPC
  interno `OrderService.GetOrderStatus` para Tracking; reaper de reservas vencidas con Hangfire si se requiere
  más allá del barrido perezoso.
