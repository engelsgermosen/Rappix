# Payments (Fase 8) — Setup, smoke E2E y modo Stripe

Rappix.Payments cobra al cliente vía Stripe (o un Fake conmutable in-process) en modelo hold+capture:

- **Authorize (hold)** al confirmar el pedido — la saga llega a `AwaitingPayment` y publica `PaymentRequestedIntegrationEvent`. Payments autoriza el hold contra el gateway y publica `PaymentSucceededIntegrationEvent`. Cero dinero movido todavía.
- **Capture** al recibir `OrderDeliveredIntegrationEvent` de Dispatch (terminal feliz). El dinero sale de la tarjeta del cliente.
- **Compensación**:
  - Si la saga cancela ANTES de capturar (timeout merchant, sin courier, cancelación del cliente) → Payments hace **Void** del hold. Cliente nunca cobrado.
  - Si la saga falla DESPUÉS de capturar (race raro, ej. `StockCommitFailed` post-pago) → Payments marca **NeedsReview** (no auto-refund — humano decide vía dashboard, follow-up Fase 9+).
  - Si la saga emite `RefundRequestedIntegrationEvent` explícitamente (race PaymentSucceeded tardío post-cancelación) y el aggregate está Captured → Payments hace **Refund** real + publica `RefundCompletedIntegrationEvent`.

Puerto host **5008** (container 8080, REST + webhook). Reservado: puerto host **5018** (container 8081, HTTP/2 h2c) para un futuro `PaymentsGrpc`. Sin servicio gRPC expuesto en Fase 8.

---

## 1. Arrancar el servicio (default: Fake gateway, sin Stripe)

`Payments:Gateway=Fake` está por defecto en `appsettings.json` y en `docker-compose.yml` — no requiere cuenta Stripe, ni Stripe CLI, ni claves. El smoke E2E corre así out-of-the-box.

```powershell
# Stack completo (8 servicios .NET + infra):
docker compose up -d --build

# Solo Payments (asume el resto ya levantado):
docker compose up -d --build payments-api
```

Verifica:

```powershell
docker ps --filter "name=rappix-payments"
docker logs rappix-payments --tail 30
# Esperado:
#   Now listening on: http://[::]:8080
#   Now listening on: http://[::]:8081
#   Bus started: rabbitmq://rabbitmq:5672/
#   Application started
curl http://localhost:5008/health
# {"service":"payments","status":"ok"}
```

---

## 2. Smoke E2E (happy path con Fake)

Con el stack levantado y los pasos previos de seed corridos (`tools/seed-smoke.ps1` — registro de usuarios, merchant aprobado, catalog, courier online):

1. **POST `/api/v1/orders`** (cliente).
   - Saga: `ValidatingQuote → ReservingStock → AwaitingMerchant`.
2. **POST `/api/v1/orders/{id}/accept`** (merchant).
   - Saga: `AwaitingMerchant → AwaitingPayment`. Publica `PaymentRequestedIntegrationEvent`.
   - En Seq (`service: payments`): `PaymentRequestedConsumer` autoriza vía Fake → `pi_fake_xxx` → publica `PaymentSucceededIntegrationEvent`.
   - Saga: `AwaitingPayment → AwaitingCourier`. Publica `CourierRequestedIntegrationEvent`.
   - Dispatch asigna courier.
3. **POST `/api/v1/orders/{id}/mark-delivered`**.
   - Dispatch publica `OrderDeliveredIntegrationEvent`.
   - En Seq (`service: payments`): `OrderDeliveredCaptureConsumer` ejecuta `gateway.CaptureAsync` → `payment.Capture()` → SaveChanges. El Payment queda en `Captured`.

Validar en BD:

```powershell
docker exec rappix-postgres psql -U rappix -d rappix_payments -c `
  "SELECT id, status, amount, currency, provider_payment_intent_id FROM payments.payments WHERE id = '<orderId>';"
```

Esperado:

```
                  id                  |  status   | amount  | currency | provider_payment_intent_id
--------------------------------------+-----------+---------+----------+----------------------------
 01999c8e-...                         | Captured  |  250.50 | DOP      | pi_fake_<guid>N
```

---

## 3. Smoke E2E (compensación pre-captura, void)

Para ejercer el Void: forzar un timeout corto del merchant y NO aceptar el pedido.

```powershell
# Reducir el timeout del merchant a 15s (override en compose):
docker compose stop orders-api
$env:Orders__Timeouts__Merchant = "00:00:15"
docker compose up -d orders-api

# POST /api/v1/orders, NO aceptar.
# Tras 15s, la saga timeout -> compensa -> publica OrderCancelledIntegrationEvent.

# Validar en BD: el Payment quedo Voided (cliente nunca cobrado).
docker exec rappix-postgres psql -U rappix -d rappix_payments -c `
  "SELECT id, status, reason FROM payments.payments WHERE id = '<orderId>';"
```

Esperado:

```
                  id                  | status |        reason
--------------------------------------+--------+------------------------
 01999c8e-...                         | Voided | El merchant no acepto a tiempo
```

---

## 4. Smoke con Stripe REAL (manual, opcional, NO en CI)

Para validar el adaptador `StripePaymentGateway` contra Stripe test mode.

### 4.a. Pre-requisitos

- Cuenta Stripe (test mode es gratis): https://dashboard.stripe.com/test
- Stripe CLI: `winget install Stripe.StripeCLI` (Windows) o `brew install stripe/stripe-cli/stripe` (macOS).

### 4.b. Setup local

```powershell
# Terminal 1: webhook listener (reenvia eventos de Stripe a tu Payments local).
stripe login
stripe listen --forward-to localhost:5008/api/v1/payments/webhooks/stripe
# Copia el "Your webhook signing secret is whsec_xxx"
```

```powershell
# Editar .env (raiz del repo, gitignored):
PAYMENTS_GATEWAY=Stripe
STRIPE_API_KEY=sk_test_xxx       # del dashboard de Stripe -> Developers -> API keys
STRIPE_WEBHOOK_SECRET=whsec_xxx  # de la salida de `stripe listen`
```

```powershell
# Terminal 2: relanzar Payments con la nueva config.
docker compose stop payments-api
docker compose up -d --build payments-api
docker logs rappix-payments --tail 10
# Esperado: arranco con StripePaymentGateway (no se ve un mensaje explicito, pero
# al hacer un pedido se vera "pi_xxx" en lugar de "pi_fake_xxx").
```

### 4.c. Repetir el flujo happy

Mismo flujo de la sección 2. En BD ahora se ve `provider_payment_intent_id = pi_xxx` (no `pi_fake_xxx`).

Verificar en el [Stripe Dashboard test mode](https://dashboard.stripe.com/test/payments): aparece un `PaymentIntent` con `Amount = 250.50 DOP`, status `requires_capture` (después del Authorize) y `succeeded` (después del Capture).

En la terminal 1 de `stripe listen` se ven los eventos webhook entrando — Payments los loguea a Seq como informativos (`service: payments`, `Webhook Stripe recibido: type=payment_intent.succeeded ...`).

### 4.d. Tests `[SkippableFact]` contra Stripe real (validacion manual del adaptador)

```powershell
$env:STRIPE_API_KEY = "sk_test_xxx"
dotnet test --filter "FullyQualifiedName~StripePaymentGatewayTests"
```

Sin la env var, los 5 smoke tests se SALTAN automáticamente (no fallan CI). Con la var, crean PaymentIntents/Refunds REALES en tu cuenta de test — consumen rate limit, pero son baratos.

---

## 5. Idempotencia de dinero — qué garantiza qué

La defensa contra doble cobro tiene 3 niveles complementarios (ADR-0009 #5):

| Nivel | Cubre | Mecanismo en código |
|---|---|---|
| **1. Inbox EF (MassTransit)** | Broker re-entrega el mismo `MessageId`. | `AddEntityFrameworkOutbox<PaymentsDbContext>` + `AddConfigureEndpointsCallback`. Unique `(MessageId, ConsumerId)` en `InboxState`. |
| **2. Aggregate idempotente + PK natural** | Race entre 2 instancias del consumer dentro del mismo MessageId no escrito aún en InboxState. | `Payment.Authorize/Capture/Void/...` son no-op idempotentes. PK natural sobre `OrderId` previene segunda inserción. |
| **3. Stripe Idempotency-Key** | Timeout HTTP mid-flight (Stripe creó el PI, perdimos la respuesta, retry). | `RequestOptions { IdempotencyKey = $"pmt-{op}-{orderId}" }`. Stripe cachea respuesta 24h. |

Tests:
- `PaymentRequestedConsumerTests.SequentialRedelivery_GatewayCalledOnce_OneRow_OnePublish` — Nivel 2 con re-entrega secuencial (caso realista).
- `PaymentRequestedConsumerTests.ConcurrentPublish_RacePreventedByPk_OneRow_OnePublish_StripeKeyPreventsDoubleCharge` — Niveles 2 + 3 con race concurrente.
- `OrderTerminalCompensationConsumerTests.Captured_PlusRefundRequested_RefundsAndPublishesCompleted` — guardrail: asserta DB Status + Publish via outbox para detectar el bug "SaveChanges olvidado" (vigilance #3).
- `StripePaymentGatewayTests.AuthorizeAsync_SameIdempotencyKey_ReturnsSameIntentId` (`[SkippableFact]`) — valida Nivel 3 directamente contra Stripe real.

---

## 6. Troubleshooting

| Síntoma | Causa | Solución |
|---|---|---|
| `payments-api` se reinicia en loop, log `Jwt:SigningKey no esta configurado` | Falta `IDENTITY_JWT_SIGNINGKEY` en `.env` | Definir igual que para Identity. |
| Webhook devuelve `400 webhook_not_configured` | `STRIPE_WEBHOOK_SECRET` vacío o `Payments:Gateway=Fake` | Setear `STRIPE_WEBHOOK_SECRET=whsec_xxx` y `PAYMENTS_GATEWAY=Stripe` en `.env`, relanzar. |
| Webhook devuelve `400 invalid_signature` | `WebhookSecret` no coincide con la firma de Stripe | Verificar que el secret copiado es el de la sesión actual de `stripe listen` (cambia cada vez). |
| Pago se queda en `Pending` | El consumer no se ejecutó (RabbitMQ caído o inbox dedup) | `docker logs rappix-rabbitmq`, `docker logs rappix-payments`. |
| `Payments:Gateway=Stripe aun no esta cableado` | El check de `RegisterPaymentGateway` rechaza Stripe | Ese mensaje pertenece a commits 1-8 de Fase 8; en commit 9 ya se cablea — actualizar al main del repo. |
| Tests integration tardan >2 min | Cada `IAsyncLifetime` arranca su Postgres container (~10s) | Es esperado — patrón de aislamiento de Dispatch. Filtrar a un test específico con `--filter`. |

---

## 7. Referencias

- ADR-0009: decisiones de diseño (`docs/adr/0009-payments-design.md`).
- Plan Fase 8: `~/.claude/plans/fase-8-reflective-pretzel.md`.
- Stripe.net docs: https://docs.stripe.com/api?lang=dotnet
- Stripe webhooks: https://docs.stripe.com/webhooks
