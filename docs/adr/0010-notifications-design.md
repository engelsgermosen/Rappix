# ADR 0010 — Diseño del Notifications Service (canal conmutable Fake/SendGrid, proyecciones locales, idempotencia por clave de negocio)

- **Estado:** Aceptado
- **Fecha:** 2026-05-23
- **Contexto de fase:** Fase 9 (Notifications Service) — última pieza del backend de Rappix

## Contexto

Con Payments (Fase 8) cerrado, el flujo completo del pedido emite todos sus eventos terminales reales (`OrderAccepted`, `CourierAssigned`, `OrderDelivered`, `OrderCancelled`, `OrderFailed`, `OrderCompleted`, `PaymentSucceeded`, `PaymentFailed`) pero ningún servicio notifica al usuario. La Fase 9 introduce **Rappix.Notifications**, un servicio que consume estos eventos y envía emails al cliente, merchant y courier vía un canal abstracto conmutable.

Este ADR registra las decisiones no obvias.

## Decisiones

### 1. Abstracción `INotificationChannel` con dos implementaciones conmutables por config (Fake | SendGrid)

`INotificationChannel` vive en `Rappix.Notifications.Application/Abstractions/` y expone una sola operación (`SendAsync(NotificationMessage, ct)`). Dos adaptadores en `Infrastructure/Channels/`:

- **`FakeNotificationChannel`** (default; `Notifications:Channel=Fake`): in-process, sin red, sin cuenta SendGrid. Loguea el email completo (ToName/ToEmail/Subject/Body) a Seq con un `[LoggerMessage]` source-gen. Permite que el smoke E2E y los tests corran sin SendGrid configurado.
- **`SendGridNotificationChannel`** (`Notifications:Channel=SendGrid`): wrap delgado sobre `SendGrid.SendGridClient` con `MailHelper.CreateSingleEmail`. Constructor exige `Notifications:SendGrid:ApiKey` no-vacío (fail-fast al arrancar). Extrae `X-Message-Id` del header de la respuesta para correlación con webhooks de bounces/spam (follow-up).

**Domain y Application jamás referencian `SendGrid`**. Solo Infrastructure (el adaptador) lo conoce. Cambiar a Mailgun/SES es swap del adaptador en Infrastructure, cero cambio en Domain/Application. Mirror exacto del patrón `IPaymentGateway` de Payments.

Identity ya tiene su propio `SendGridEmailSender` (acoplado al caso de uso "confirmation email"); **no se reutiliza**. Lo único que se comparte son las env vars (`SENDGRID_API_KEY`, `SENDGRID_FROM_EMAIL`, `SENDGRID_FROM_NAME`) — la cuenta SendGrid es la misma para ambos servicios.

### 2. Resolución de emails vía 3 proyecciones locales (cero gRPC sincrónico, cero modificación a contratos)

Los eventos del pedido NO llevan los emails de los 3 destinatarios — solo IDs. El problema crítico: `OrderSubmittedIntegrationEvent` lleva `CustomerUserId + MerchantId` (entity id), pero NO `MerchantOwnerUserId`; `OrderAccepted/Cancelled/Failed` no llevan `CustomerUserId`; `OrderDelivered` solo lleva `OrderId`. Para notificar, el servicio necesita resolver:

- Cliente: `CustomerUserId → Email`
- Merchant: `MerchantId → OwnerUserId → Email` (dos hops)
- Courier: `CourierId → Email` (con `CourierId == UserId` por diseño 1-1 de Dispatch)

**Decisión**: tres proyecciones locales mantenidas por consumers que escuchan eventos de Identity y Merchants:

| Proyección | PK | Pobladas desde | Consumidas por |
|---|---|---|---|
| `UserContact(UserId, Email, FirstName, LastName, UserType, EmailConfirmed)` | UserId | `UserRegisteredIntegrationEvent` (Create), `UserEmailConfirmedIntegrationEvent` (Update) | TODOS los consumers de pedido para resolver email + nombre del destinatario. |
| `MerchantContact(MerchantId, OwnerUserId)` | MerchantId | `MerchantApproved/Activated/Rejected/Suspended` (los 4 llevan ambos IDs) — upsert. | Cualquier consumer que necesite el OwnerUserId del merchant para luego buscar `UserContact`. |
| `NotificationOrder(OrderId, CustomerUserId, MerchantId, CourierUserId?)` | OrderId | `OrderSubmittedIntegrationEvent` (insert), `CourierAssignedIntegrationEvent` (SetCourier). | Consumers de `OrderAccepted/Cancelled/Failed/Delivered/Completed` para resolver IDs que sus eventos NO llevan. |

**Alternativas descartadas**:
- **Extender `OrderSubmittedIntegrationEvent` para llevar `MerchantOwnerUserId`** — descartado para no tocar Orders y mantener los contratos limpios de datos de presentación. Merchants ya publica 4 eventos con `MerchantId+OwnerUserId`; usarlos es más limpio que sumar un campo nuevo a un contrato existente.
- **gRPC sincrónico a Merchants/Identity** — descartado por (a) riesgo de URL default localhost (lección Fase 5 de Orders→Merchants), (b) acoplamiento sincrónico de Notifications a la disponibilidad de otros servicios.

**Cold-start risk** (proyección vacía cuando llega el primer pedido): el consumer logea Warning y SALTA el envío del email — la proyección se persiste igual, los logs son auditoría suficiente, y un follow-up job de reconciliación podría detectar "NotificationOrder con MerchantContact pero sin Notification" para reintentar. En local-dev (`docker compose down -v` + seed) el cold-start no aplica porque `MerchantApproved` siempre se publica con Notifications ya corriendo.

### 3. Idempotencia por clave de NEGOCIO (no por MessageId del broker) — DECISIÓN CRÍTICA

**El problema sutil que evitar**: dedup por `MessageId` (el approach default del inbox EF de MassTransit) NO funciona para el caso donde **dos eventos distintos del broker producen la MISMA notificación lógica**. El caso ancla:

- `OrderDeliveredIntegrationEvent` (Dispatch) → notifica al cliente "tu pedido fue entregado".
- `OrderCompletedIntegrationEvent` (Orders saga) → notifica al cliente "tu pedido fue entregado" (mismo mensaje lógico).

Ambos eventos tienen `MessageId` DIFERENTE. Un dedup por `MessageId` los dejaría pasar a ambos → el cliente recibe **2 emails idénticos** "tu pedido fue entregado". Sería el equivalente al doble-cobro de Payments pero con emails.

**Decisión**: idempotencia en 3 niveles **complementarios**, todos por clave de NEGOCIO `(RelatedOrderId, RecipientUserId, NotificationType)`:

| Nivel | Cubre | Mecanismo |
|---|---|---|
| **1. Inbox EF (MassTransit)** | Broker re-entrega EL MISMO `MessageId`. | `AddEntityFrameworkOutbox<NotificationsDbContext>` + `AddConfigureEndpointsCallback((ctx,_,cfg) => cfg.UseEntityFrameworkOutbox<NotificationsDbContext>(ctx))`. Unique `(MessageId, ConsumerId)` en `InboxState`. |
| **2. Lookup por clave de negocio en `NotifyHandler`** | Dos eventos distintos del broker (MessageIds diferentes) que mapean a la misma notificación lógica — caso `OrderDelivered+OrderCompleted`. | Antes de insertar la `Notification`, `INotificationRepository.GetByBusinessKeyAsync(orderId, recipientUserId, type)` — si existe, return Success no-op (no se llama al canal). Cubre el 99% de los casos en operación secuencial normal. |
| **3. Unique partial index `UX_Notification_BusinessKey` a nivel BD** | Race concurrente entre 2 workers que pierden el lookup del Nivel 2. | `CREATE UNIQUE INDEX ON Notifications(RelatedOrderId, RecipientUserId, NotificationType) WHERE RelatedOrderId IS NOT NULL`. `NotificationsDbContext.SaveChangesAsync` traduce SOLO esta violación (`SqlState 23505` + `ConstraintName` exacto) a `DuplicateNotificationException` que el `NotifyHandler` atrapa como no-op. **Cualquier otro `DbUpdateException` (FK, NOT NULL, otra constraint) propaga tal cual** — defensiva contra bugs ocultos. |

`SourceMessageId` se persiste en `Notification` como **columna informacional** (audit: "qué mensaje del broker produjo este email") pero **NO participa del unique index**. Esto es deliberado.

**Test crítico (commit 10)**: `DoubleDeliveryIdempotencyTests` con 3 sub-tests:
- Escenario A: mismo evento `OrderAccepted` publicado 2 veces → 1 fila + 1 channel call.
- Escenario B: `OrderDelivered` seguido de `OrderCompleted` (mismo OrderId) → 2 filas total (cust+merch), NO 4. Channel.Received(2) total, NO 4.
- Bonus: orden invertido (`OrderCompleted` primero, `OrderDelivered` después) → mismo resultado simétrico.

### 4. Plantillas hardcoded en C# (texto plano)

`NotificationTemplates` es una clase estática con 10 métodos por `(NotificationType × RecipientRole)` que devuelven `NotificationContent(Subject, Body)`. Texto plano en Fase 9 (HTML como follow-up). Sin tabla de templates, sin engine de rendering (Razor/string.Format), sin admin UI.

Justificación: scope mínimo, testeable como funciones puras, sin schema extra. Cambios = redeploy (aceptable porque las plantillas son pocas y estables). El día que se quiera permitir editar sin redeploy, se introduce una tabla `notification_templates` con seed migration; **no se hace hoy**.

### 5. Sin Redis, sin gRPC, sin idempotency middleware, sin SignalR

Notifications NO expone `POST/PUT/PATCH` mutantes (solo consume eventos). No se llama `app.UseIdempotency()` → no requiere `IDistributedCache` → **no requiere Redis**. Mismo razonamiento que Tracking. Tampoco expone Hub SignalR (a diferencia de Tracking que sí pushea en vivo al cliente) — el "push" de Notifications es por email asincrónico.

Sin gRPC inbound ni outbound: no se hace `MapGrpcService<>`, no se inyectan clientes gRPC. El puerto 5019 del compose queda mapeado a 8081 PERO sin listener (el Dockerfile EXPOSE solo 8080). Si en el futuro se necesita HTTP/2 cleartext, se añade un `ListenAnyIP(8081, Http2)` en `Program.cs` + `EXPOSE 8081` en Dockerfile + se reactiva el puerto en compose.

## Consecuencias

**Positivas**:
- Cero acople sincrónico — Notifications puede arrancar/caerse sin afectar al resto.
- 3 proyecciones locales = autonomía total para resolver destinatarios.
- Dedup por clave de negocio cubre el caso `OrderDelivered+OrderCompleted` que un dedup por MessageId NO atraparía.
- Switch Fake/SendGrid permite smoke E2E del flujo completo SIN cuenta SendGrid.

**Negativas / riesgos**:
- **Cold-start gap**: si Notifications arranca después de un `MerchantApproved`, ese merchant no recibe notificaciones hasta backfill manual. Mitigación local-dev: `down -v` siempre. Mitigación producción: follow-up backfill script.
- **Cambios de email post-registro**: el `UserContact` se sobreescribe desde `UserRegistered` y `UserEmailConfirmed`. NO se cubre el caso "user actualiza su email vía Identity" porque Identity hoy no publica `UserEmailChangedIntegrationEvent`. Follow-up.

## Alternativas consideradas (descartadas)

- **Extender `OrderSubmittedIntegrationEvent` para llevar `MerchantOwnerUserId`** — descartado (sección 2 arriba).
- **gRPC sincrónico a Identity/Merchants** — descartado (sección 2 arriba).
- **`IEmailSender` reutilizado de Identity** — descartado: el `IEmailSender` actual está acoplado al método `SendEmailConfirmationAsync(toEmail, toName, confirmationUrl)`. Una abstracción genérica requiere otra interface (`INotificationChannel`); pivotear ambos servicios a la misma interfaz queda como follow-up cuando convenga (no aporta valor en Fase 9).

## Follow-ups

- HTML email templates (cuerpo HTML + texto fallback, plantilla por idioma).
- Canales adicionales: Push (Firebase/APNs) y SMS (Twilio) detrás de la misma `INotificationChannel` con sub-tipos o segunda interfaz multi-canal.
- Preferencias de notificación por usuario (opt-in/opt-out por `NotificationType`).
- Backfill script de proyecciones (`tools/backfill-notifications-contacts.ps1`) para cubrir cold-start en producción.
- Reintento automático de notificaciones `Failed` (job Quartz que lea el historial y reintente el envío).
- Endpoint admin `GET /api/v1/notifications/me` para consultar el historial del usuario autenticado (plumbing ya cableado en Program.cs: JWT bearer + ApiVersionSet + grupo `/api/v1`).
- SendGrid webhook (`POST /api/v1/notifications/webhooks/sendgrid`) para procesar bounces/spam reports y marcar `UserContact.EmailConfirmed = false` o desactivar el envío.
- Test de race level 3 con RabbitMQ real + `WebApplicationFactory<Program>` (los tests actuales usan ITestHarness in-memory que cubre Niveles 1 y 2 pero no el race concurrente puro).
