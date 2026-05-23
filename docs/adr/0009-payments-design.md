# ADR 0009 — Diseño del Payments Service (hold+capture, IPaymentGateway, idempotencia 3 niveles)

- **Estado:** Aceptado
- **Fecha:** 2026-05-23
- **Contexto de fase:** Fase 8 (Payments Service)

## Contexto

Fase 5 (Orders) dejó un `SimulatedPaymentResponder` (`src/Services/Orders/Rappix.Orders.Infrastructure/Messaging/Simulation/SimulatedPaymentResponder.cs`) que atiende `PaymentRequestedIntegrationEvent` y responde `PaymentSucceeded/Failed/RefundCompleted` según un flag de config. Es el último simulador del núcleo del negocio (el `SimulatedDeliveryResponder` se queda hasta una fase futura).

Fase 8 lo reemplaza por un servicio real que cobra vía **Stripe** (o un **Fake** conmutable in-process) en modelo **hold + capture**, manteniendo intactos los contratos `Rappix.Contracts.Payments.*` que la saga ya consume. Construido sobre la plantilla madura Tracking/Dispatch (5 csproj Domain/Application/Infrastructure/Api/Tests, BD `rappix_payments` ya prevista en `infra/local/postgres-init.sql`).

Este ADR registra las decisiones no obvias.

## Decisiones

### 1. Abstracción `IPaymentGateway` con dos implementaciones conmutables por config (Fake | Stripe)

`IPaymentGateway` vive en `Rappix.Payments.Application/Abstractions/` y expone 4 operaciones (`AuthorizeAsync`, `CaptureAsync`, `VoidAsync`, `RefundAsync`). Dos adaptadores en `Infrastructure/Gateways/`:

- **`FakePaymentGateway`** (default; `Payments:Gateway=Fake`): in-process con `ConcurrentDictionary<idempotencyKey, providerId>` instance-level (no static — vigilance #2). Permite que el smoke E2E y los tests corran sin cuenta Stripe ni Stripe CLI. Modos `Always` (todo OK) y `AlwaysFail` (para tests del camino fallido).
- **`StripePaymentGateway`** (`Payments:Gateway=Stripe`): wrap delgado sobre `Stripe.PaymentIntentService` + `RefundService` con `RequestOptions.IdempotencyKey` estable por `(operación, OrderId)`. Hardcoded `pm_card_visa` + `Confirm=true` + `CaptureMethod=manual` para Authorize no-interactivo en test mode.

**Domain y Application jamás referencian `Stripe.net`**. Solo Infrastructure (el adaptador) y Api (el endpoint webhook que valida firmas) lo conocen. Cambiar a Adyen/Braintree en el futuro es swap del adaptador en Infrastructure, cero cambio en Domain/Application.

### 2. Hold (Authorize) + Capture al delivered; captura disparada por consumer de `OrderDelivered`, NO por la saga

El modelo hold+capture es la primitiva financiera correcta para delivery: si el courier no entrega, el dinero **nunca sale de la tarjeta del cliente** (solo se libera el hold). Stripe lo expone con `CaptureMethod="manual"`.

Quién dispara la captura: `OrderDeliveredCaptureConsumer` (Payments) reacciona al `OrderDeliveredIntegrationEvent` que **Dispatch ya publica** (`src/Services/Dispatch/Rappix.Dispatch.Infrastructure/Messaging/OrderTerminalEventsConsumer.cs`). **La saga de Orders no orquesta la captura** — mismo patrón que Tracking y Dispatch consumen terminales (los servicios "downstream" reaccionan, la saga se mantiene limpia de detalles de pago). Esto elimina la necesidad de un evento adicional en el contrato y mantiene `OrderStateMachine.cs` sin cambios.

### 3. Compensación: VOID pre-captura, NeedsReview post-captura, NUNCA auto-refund

Política implementada en `OrderTerminalCompensationConsumer` (multi-IConsumer `OrderCancelled` + `OrderFailed` + `RefundRequested`):

- **`Authorized` + `OrderCancelled/OrderFailed`** → `VoidAsync` (cancela el hold). El cliente nunca es cobrado.
- **`Captured` + `OrderCancelled/OrderFailed`** → `MarkNeedsReview` en el aggregate. **NO auto-refund**. Un humano decide vía dashboard (Fase 9+).
- **`Captured` + `RefundRequested` (saga emite explícitamente)** → `RefundAsync` real + `MarkRefunded` + publica `RefundCompletedIntegrationEvent`.

La regla "nunca auto-refund de dinero ya capturado" es consistente con `StockCommitFails_NeedsReview` que la saga ya emite (`OrderStateMachine.cs:139-144`). Auto-refundar requiere aprobación humana — riesgo de fraude.

### 4. Identidad `Payment.Id == OrderId` (no strongly-typed `PaymentId`)

`Payment : Entity<Guid>` con `Id` que es el `OrderId` externo. Justificación:

- **Cardinalidad 1↔1 con Order** (un pago por pedido).
- **El PK natural ES la unique constraint** que impide doble-inserción concurrente (Nivel 2 de idempotencia de dinero) sin índices adicionales.
- **Mismo razonamiento que `OrderTracking`** en Tracking (`src/Services/Tracking/Rappix.Tracking.Domain/OrderTrackings/OrderTracking.cs`): sin reglas de invariante sobre el id, no aporta el wrapper strongly-typed.
- **El contrato existente** `PaymentSucceededIntegrationEvent.PaymentId` ya admite el uso del OrderId como PaymentId (la saga solo lo registra como `Saga.PaymentId` para auditoría).

### 5. Idempotencia de dinero en 3 niveles complementarios

Cobrar dos veces es un bug que cuesta dinero real. Tres barreras complementarias, todas necesarias:

| Nivel | Cubre | Mecanismo |
|---|---|---|
| **1. Inbox EF (MassTransit)** | Broker re-entrega el mismo `MessageId`. | `AddEntityFrameworkOutbox<PaymentsDbContext>` + `AddConfigureEndpointsCallback((ctx,_,cfg) => cfg.UseEntityFrameworkOutbox<PaymentsDbContext>(ctx))`. Unique `(MessageId, ConsumerId)` en `InboxState`. |
| **2. Aggregate idempotente + PK natural sobre OrderId** | Race entre 2 instancias del consumer dentro del mismo MessageId no escrito aún en InboxState. | El aggregate `Payment.Authorize/Capture/Void/...` son no-op idempotentes con los mismos identificadores. El PK natural sobre `OrderId` previene la segunda inserción (DbUpdateException -> MassTransit retry -> el segundo consumer encuentra el row y no-op). |
| **3. Stripe Idempotency-Key** | Timeout HTTP mid-flight (Stripe creó el PI, perdimos la respuesta, MassTransit reintenta). | `RequestOptions { IdempotencyKey = $"pmt-{op}-{orderId}" }`. Stripe cachea respuesta 24h y devuelve la original sin crear un nuevo PaymentIntent. **Sin esto, doble cobro real con timeout de red.** |

El test crítico `PaymentRequestedConsumerTests.SequentialRedelivery_GatewayCalledOnce_OneRow_OnePublish` valida Nivel 2 contra el broker re-entrega secuencial (el caso realista en producción). El test complementario `ConcurrentPublish_RacePreventedByPk_OneRow_OnePublish_StripeKeyPreventsDoubleCharge` valida que aún en race concurrente, los Niveles 2 + 3 mantienen "no doble cobro real" aunque el gateway sea llamado >1 vez.

### 6. Mantener nombres `PaymentSucceeded` / `PaymentFailed` (no rename a `PaymentAuthorized`)

El plan original sugería renombrar `PaymentSucceededIntegrationEvent` a `PaymentAuthorizedIntegrationEvent` para reflejar el modelo hold+capture (semánticamente "succeeded" evoca "ya se cobró", pero técnicamente solo se autorizó).

**Decisión final**: MANTENER los nombres. Razones:
- Romper el contrato afecta a Orders (`OrderStateMachine.cs:222`) + a los 6 tests de saga + a la cola RabbitMQ (los mensajes en cola con el nombre viejo no se entregarían al consumer nuevo durante el despliegue).
- La ganancia semántica es baja: el flujo "Authorized → Captured" sigue siendo claro en código; cuando llegue Notifications (Fase 9+), se introduce `PaymentCapturedIntegrationEvent` como evento adicional con la semántica precisa "el dinero salió de la tarjeta".

### 7. Webhook Stripe **informativo** en Fase 8

`POST /api/v1/payments/webhooks/stripe` (`StripeWebhookEndpoint.cs`):
- `[AllowAnonymous]` — autenticación = firma del header `Stripe-Signature`, no JWT.
- Valida con `Stripe.EventUtility.ConstructEvent(json, sigHeader, webhookSecret)`. Firma inválida → 400 (sin el `WebhookSecret` un atacante NO puede falsificar eventos).
- **El handler solo loguea**. NO muta estado.

Justificación: el flujo principal con `pm_card_visa` + `Confirm=true` + `CaptureMethod=manual` en Stripe test mode es SÍNCRONO — `AuthorizeAsync` devuelve `requires_capture` directo, `CaptureAsync` devuelve `succeeded` directo. El webhook sería obligatorio solo para 3DS/async (payment methods reales del cliente, fuera de scope Fase 8). Cuando llegue ese flujo se cablea el handler para mutar estado.

Con `Payments:Gateway=Fake` el endpoint queda accesible pero sin `WebhookSecret` configurado todas las requests devuelven 400 (fail-safe — sin clave compartida no hay forma de verificar firmas). Para Stripe en local: `stripe listen --forward-to localhost:5008/api/v1/payments/webhooks/stripe` y exportar el `whsec_xxx` como `STRIPE_WEBHOOK_SECRET`.

### 8. `pm_card_visa` hardcoded en `StripePaymentGateway` (gap consciente)

El `AuthorizeAsync` del adaptador construye el `PaymentIntent` con `PaymentMethod = "pm_card_visa"` — un test payment method de Stripe que permite `Confirm=true` no-interactivo. Es la **única forma** de ejecutar un Authorize sin UI en test mode.

**Producción real** requiere que el cliente provea un `PaymentMethodId` desde el frontend (Stripe Elements / PaymentSheet) que se pasaría aquí en lugar del literal. Esto está **fuera de scope Fase 8** (sin frontend con UI de pago). Documentado como follow-up. El gap está aislado a 1 línea en 1 archivo (`StripePaymentGateway.cs`), trivial de sustituir cuando llegue el frontend.

## Consequences

- Orders queda totalmente desacoplado de la pasarela (Domain/Application no referencian `Stripe.net`).
- `OrdersOptions.Simulation.PaymentOutcome` y `PaymentDelayMs` se borran de la config (limpieza commit 10).
- 8 servicios .NET corriendo en compose (Identity, Merchants, Catalog, Pricing, Orders, Dispatch, Tracking, Payments).
- 1 BD nueva activa (`rappix_payments`, ya creada en `infra/local/postgres-init.sql:12`).
- `Stripe.net 51.1.0` agregado a CPM — versión stable verificada en NuGet al pinear (no asumida). Referenciado solo en `Rappix.Payments.Infrastructure.csproj` (gateway) y `Rappix.Payments.Api.csproj` (webhook validation).
- `Xunit.SkippableFact 1.5.61` agregado a CPM (solo `Rappix.Payments.Tests`).
- Smoke E2E sigue funcionando sin cuenta Stripe (Fake por default).
- 56 unit + integration tests (28 aggregate + 8 fake + 6 PaymentRequested + 5 OrderDeliveredCapture + 9 OrderTerminalCompensation) + 1 unit `StripePaymentGateway.Constructor_WithoutApiKey` + 5 smoke `[SkippableFact]` (saltan sin STRIPE_API_KEY).

## Follow-ups

- **Fase 9 Notifications**: crear `PaymentCapturedIntegrationEvent` para notificar al cliente del cobro.
- **Fase 9+ Multi-currency**: hoy se acepta cualquier 3-char ISO; agregar whitelist por merchant.
- **Refund automático con aprobación humana**: workflow vía dashboard admin.
- **Job de reconciliación nocturna**: cruzar nuestras filas `payments` con Stripe API.
- **Sustituir `pm_card_visa`** por `PaymentMethodId` real del cliente (requiere frontend con Stripe Elements).
- **Webhooks full async** para 3DS challenge (`payment_intent.requires_action`, `payment_intent.succeeded` async).
- **`PaymentCapturedIntegrationEvent`**: emitir desde `OrderDeliveredCaptureConsumer` cuando Notifications lo consuma.
- **Refactor saga**: que Orders NO emita `RefundRequestedIntegrationEvent` (Payments decide solo en `OrderCancelled/OrderFailed`). Modifica `OrderStateMachine.cs:159, :167, ~268` + tests — mejora aislada con su propio ADR.
- **Métricas IPaymentGateway** (Fase 9 Observability): counters/histograms OpenTelemetry por operación.
- **Rate limiting del webhook** (responsabilidad de ApiGateway/YARP futuro).
- **GET `/api/v1/payments/{orderId}` admin** (decisión usuario: diferido a Fase 9+ cuando llegue dashboard admin).
