# Rappix

> Plataforma de delivery multi-vertical (comida, farmacia, supermercado, paquetería) construida con arquitectura de microservicios en .NET 10.

## Visión

Rappix es un marketplace de tres lados (clientes, comercios, repartidores) que demuestra patrones modernos de arquitectura distribuida en .NET 10: sagas, outbox pattern, event-driven, geo-distributed dispatching, real-time tracking y splits de pago.

## Stack

| Capa | Tecnología |
|---|---|
| Runtime | .NET 10 LTS · C# 14 |
| Web | ASP.NET Core 10 (Minimal APIs + Controllers) |
| Gateway | YARP |
| Mediator | MediatR · FluentValidation |
| Mensajería | MassTransit 8 · RabbitMQ 3.13 |
| ORM | EF Core 10 · Dapper (queries hot) |
| BD | PostgreSQL 17 + PostGIS · Redis 7 |
| Identity | JWT propio (HS256) |
| Tiempo real | SignalR (backplane Redis) |
| RPC interno | gRPC |
| Jobs | Hangfire |
| Logging | Serilog → Seq |
| Object storage | MinIO |
| Frontend | Next.js 15 · TypeScript · Tailwind · shadcn/ui |
| Mapas | Mapbox GL JS |
| Containers | Docker · Docker Compose |
| Tests | xUnit · Testcontainers · FluentAssertions |

## Microservicios

| Servicio | Responsabilidad | Puerto |
|---|---|---|
| Identity | Auth, JWT, roles | 5001 |
| Merchants | Comercios y zonas | 5002 |
| Catalog | Items y stock | 5003 |
| Pricing | Cálculo de tarifas | 5004 |
| Orders | Saga del pedido | 5005 |
| Dispatch | Asignación de couriers | 5006 / gRPC 5016 |
| Tracking | Push en vivo (SignalR) | 5007 / HTTP/2 5017 |
| Payments | Stripe (hold+capture) | 5008 / HTTP/2 5018 |
| Notifications | Email (Fake/SendGrid conmutable) | 5009 |
| Ratings | Calificaciones | 5010 |
| Gateway | YARP | 5000 |

## Servicios implementados

### Identity (Fase 1) · puerto 5001
Auth con JWT propio (HS256), refresh con rotación + detección de robo, confirmación de email
(SendGrid), Google OAuth y outbox MassTransit. Guía: [docs/setup-identity.md](docs/setup-identity.md).

### Merchants (Fase 2) · REST 5002 · gRPC 5012
Comercios multi-vertical, zonas de cobertura PostGIS (`nearby`), workflow de aprobación admin, logos
en MinIO (validados con ImageSharp) y un servicio gRPC interno. Alta por **choreography**: consume
`UserRegisteredIntegrationEvent` de Identity y crea un Merchant `Draft`.
Guía: [docs/setup-merchants.md](docs/setup-merchants.md) · diseño: [ADR-0003](docs/adr/0003-merchants-design.md).

| Grupo | Endpoints |
|---|---|
| Owner (`userType=Merchant`) | `GET/PUT /merchants/me` · `PUT /merchants/me/operating-hours` · `POST`/`DELETE /merchants/me/service-areas` · `POST /merchants/me/logo` · `POST /merchants/me/submit-for-approval` |
| Público | `GET /merchants/nearby` · `GET /merchants/{id}` · `GET /merchants/by-slug/{slug}` · `GET /merchants/{id}/logo-url` |
| Admin (`userType=Admin`) | `GET /admin/merchants?status=` · `POST .../approve` · `.../reject` · `.../suspend` · `.../unsuspend` · `PUT .../commission` |
| gRPC interno | `MerchantValidationService.IsMerchantActive` · `GetMerchantBasicInfo` |

### Catalog (Fase 3) · REST 5003 · gRPC 5013
Catálogo por merchant: items multi-vertical con **atributos JSONB** validados por vertical, categorías,
modificadores, **stock con concurrencia optimista (`xmin`, sin sobreventa)** y **búsqueda full-text en
español** (`tsvector` generado + GIN). El catálogo se crea por **choreography** (consume
`MerchantApprovedIntegrationEvent`) y se gatea con eventos de activación/suspensión. Primer **cliente
gRPC** (valida el merchant contra Merchants con resiliencia + fallback al gating local) y servidor gRPC
propio. Reutiliza los building blocks compartidos Storage/Imaging/WebApi.Auth (regla de tres).
Guía: [docs/setup-catalog.md](docs/setup-catalog.md) · diseño: [ADR-0004](docs/adr/0004-catalog-design.md).

| Grupo | Endpoints |
|---|---|
| Owner (`userType=Merchant`) | `GET /catalog/me` · `POST`/`DELETE /catalog/me/categories` · `POST`/`GET`/`PUT`/`DELETE /catalog/me/items` · `PUT .../availability` · `PUT .../attributes` · `POST .../modifiers` · `PUT .../stock` · `POST .../photo` |
| Público | `GET /catalog/items/search?q=` · `GET /catalog/items/{id}` · `GET /catalog/items/{id}/photo-url` |
| gRPC interno | `CatalogValidationService.GetItemPricing` |

### Pricing (Fase 4) · REST 5004 · gRPC 5014
Motor de tarifas: cotiza el precio total de un pedido y emite una **cotización (Quote) persistida con
expiración** (10 min, expiración perezosa al leer). Desglose transparente y **determinístico** (orden:
subtotal → surge → descuento → fees → ITBIS → propina → total; todo `decimal(19,4)`, redondeo bancario una
sola vez al final). **Strategy pattern**: surge por zona + franja horaria × *demand factor* (cap x3,
configurable), tarifa de envío por vertical y descuentos (primera compra + cupones). **Cupones** completos
(código único, `MaxUses` con `xmin`, monto mínimo, límite por usuario; el uso se redime al **consumir** la
cotización, no al cotizar). Dos **clientes gRPC** (precios de Catalog con fallback al cache local;
validación de Merchants) y servidor gRPC propio. Cachea precios por `ItemCreatedIntegrationEvent`.
Guía: [docs/setup-pricing.md](docs/setup-pricing.md) · diseño: [ADR-0005](docs/adr/0005-pricing-design.md).

| Grupo | Endpoints |
|---|---|
| Cliente (`userType=Customer`) | `POST /pricing/quotes` · `GET /pricing/quotes/{id}` |
| Admin (`userType=Admin`) | `GET`/`POST`/`PUT`/`DELETE /admin/pricing/surge-rules` · `GET`/`POST`/`PUT`/`DELETE /admin/pricing/coupons` |
| gRPC interno | `PricingService.QuotePrice` · `ConsumeQuote` · `GetQuote` · `RevertQuoteConsumption` |

### Orders (Fase 5) · REST 5005 · gRPC 5015
El corazón transaccional: arma el pedido (**snapshot inmutable** del quote) y orquesta un flujo distribuido con
una **saga orquestada** (MassTransit State Machine, estado persistido en EF Core). Consume el quote (congela
precio + redime cupón) → **reserva stock** (hold) en Catalog → notifica al merchant (timeout 5 min) → cobro
(2 min) → courier (3 min) → confirma stock → en curso → entregado. **Compensaciones en orden inverso** ante
cualquier fallo (libera el hold, revierte quote y cupón, reembolsa si se cobró); `StockCommitFailed` tras
cobrar va a `NeedsReview`, nunca auto-reembolsa. **Timeouts durables** con Quartz + Postgres. Dispatch
(Fase 6) y Payments (Fase 8) responden los contratos reales; solo la entrega final sigue simulada vía
un `SimulatedDeliveryResponder` enchufable. Dos clientes gRPC (Pricing; reserva de Catalog).
Diseño: [ADR-0006](docs/adr/0006-orders-design.md).

| Grupo | Endpoints |
|---|---|
| Cliente (`userType=Customer`) | `POST /orders` · `GET /orders/{id}` · `GET /orders` · `POST /orders/{id}/cancel` |
| Merchant (`userType=Merchant`) | `GET /orders/merchant/pending` · `POST /orders/{id}/accept` · `POST /orders/{id}/reject` |
| Seam temporal (Dispatch, Fase 6) | `POST /orders/{id}/mark-delivered` |

### Tracking (Fase 7) · REST + SignalR 5007 · HTTP/2 5017 (reservado)
Read model dedicado que proyecta los eventos de Orders y Dispatch a un estado por pedido y empuja al
cliente final, en tiempo real vía SignalR, la **ubicación del courier asignado + el estado del pedido**.
Sin Redis (no idempotency middleware, no Geo, sin SignalR backplane en Fase 7). Sin clientes gRPC.
Ownership por `JWT.sub == OrderTracking.CustomerUserId` validado en `Subscribe` y en `GET`; 404 y 403
indistinguibles para no filtrar existencia. Estados visibles al cliente: `Placed → MerchantAccepted →
CourierAssigned → Delivered/Cancelled/Failed`. Diseño: [ADR-0008](docs/adr/0008-tracking-design.md);
setup y smoke E2E: [docs/setup-tracking.md](docs/setup-tracking.md); flujo de mensajes:
[docs/diagrams/tracking-flow.md](docs/diagrams/tracking-flow.md).

| Superficie | Endpoint |
|---|---|
| Hub SignalR (push en vivo) | `/hubs/tracking` — `Subscribe(orderId)` / `Unsubscribe(orderId)` |
| Snapshot REST (fallback)   | `GET /api/v1/tracking/orders/{orderId}` |

### Payments (Fase 8) · REST + webhook 5008 · HTTP/2 5018 (reservado)
Cobra al cliente vía **Stripe** (o un **Fake** conmutable in-process) en modelo **hold + capture**:
autoriza al confirmar el pedido (saga `AwaitingPayment`), captura al `OrderDelivered` (Dispatch).
Compensación **Void** pre-captura (hold cancelado, cliente nunca cobrado) o **NeedsReview** post-captura
(humano decide vía dashboard, nunca auto-refund). Refund **explícito** cuando la saga emite
`RefundRequestedIntegrationEvent`. **Idempotencia de dinero en 3 niveles**: inbox EF (broker re-entrega) +
PK natural `Payment.Id == OrderId` (race entre instancias) + Stripe Idempotency-Key (timeout HTTP
mid-flight). Webhook firmado en `POST /api/v1/payments/webhooks/stripe` (informativo en Fase 8 — flujo
síncrono con `pm_card_visa`). Reemplaza al `SimulatedPaymentResponder` de Orders. Sin Redis
(no idempotency middleware). Diseño: [ADR-0009](docs/adr/0009-payments-design.md); setup y smoke
E2E: [docs/setup-payments.md](docs/setup-payments.md).

| Superficie | Endpoint |
|---|---|
| Webhook Stripe (firma `Stripe-Signature`) | `POST /api/v1/payments/webhooks/stripe` |
| Consumers (bus) | `PaymentRequestedIntegrationEvent` (autoriza hold) · `OrderDeliveredIntegrationEvent` (captura) · `OrderCancelled`/`OrderFailed` (void o NeedsReview) · `RefundRequestedIntegrationEvent` (refund explícito) |

### Notifications (Fase 9) · REST 5009
Notifica a los **3 actores** del pedido (cliente, merchant, courier) por **email** vía canal conmutable
(**Fake** por defecto — log-only en Seq, smoke E2E sin SendGrid — o **SendGrid** opt-in con la misma
cuenta de Identity). Consume 9 integration events: 2 de Identity (UserRegistered, UserEmailConfirmed) +
4 de Merchants (Approved/Activated/Rejected/Suspended) + 3 de Orders/Dispatch para el flujo del pedido
(OrderSubmitted, OrderAccepted, CourierAssigned + los 4 terminales OrderDelivered/Completed/Cancelled/Failed).
**Resolución de emails sin acoplar**: 3 proyecciones locales (`UserContact`, `MerchantContact`,
`NotificationOrder`) pobladas desde los eventos — cero gRPC sincronico, cero modificación a contratos
existentes. **Idempotencia por clave de NEGOCIO** (no por MessageId del broker): unique partial index
`UX_Notification_BusinessKey` sobre `(RelatedOrderId, RecipientUserId, NotificationType)` deduplica
el caso clave `OrderDelivered` + `OrderCompleted` (dos eventos distintos -> misma notificación lógica
-> 1 email, no 2). Sin Redis, sin gRPC, sin idempotency middleware. Diseño:
[ADR-0010](docs/adr/0010-notifications-design.md).

| Superficie | Endpoint |
|---|---|
| Health check | `GET /health` |
| Consumers (bus) | `UserRegisteredIntegrationEvent` · `UserEmailConfirmedIntegrationEvent` · `MerchantApproved/Activated/Rejected/Suspended` · `OrderSubmittedIntegrationEvent` · `OrderAcceptedIntegrationEvent` · `CourierAssignedIntegrationEvent` · `OrderDelivered/Completed/Cancelled/Failed` |

## Estructura

```
rappix/
├── src/
│   ├── BuildingBlocks/     # código compartido (Core, Messaging, Observability, WebApi)
│   ├── Services/           # los 10 microservicios
│   ├── ApiGateway/         # YARP
│   └── Contracts/          # eventos públicos entre servicios
├── frontends/              # 3 apps Next.js (cliente, merchant, back-office)
├── tools/                  # scripts utilitarios
├── infra/local/            # config para infraestructura local
└── docs/                   # ADRs, diagramas, docs
```

## Arrancar local

Requisitos: Docker Desktop, .NET 10 SDK, Node 20+, pnpm.

```bash
# 1) Levantar infraestructura
docker compose -f docker-compose.yml up -d postgres redis rabbitmq seq minio

# 2) Restaurar y compilar la solución
dotnet restore
dotnet build

# 3) Aplicar migraciones (cada servicio)
./tools/apply-migrations.sh

# 4) Levantar todos los servicios
docker compose up -d
```

UIs disponibles:
- Seq: http://localhost:5341
- RabbitMQ: http://localhost:15672 (guest/guest)
- MinIO: http://localhost:9001
- Gateway API: http://localhost:5000
- Portal cliente: http://localhost:3000
- Portal merchant: http://localhost:3001
- Back-office: http://localhost:3002

### Smoke E2E de la saga de Orders

`tools/seed-smoke.ps1` (PowerShell) automatiza el setup para probar la saga de Orders end-to-end:
registra cliente/merchant/admin, confirma sus emails, aprueba el merchant, crea catálogo + ítem con
stock y emite una cotización; al final imprime los tokens, IDs y los comandos listos para crear el
pedido → accept → mark-delivered observando Seq.

Requisitos: PowerShell 5+, el contenedor postgres `rappix-postgres` y los 5 servicios + infra arriba
(`docker compose up -d`). Pensado para un stack recién levantado (`docker compose down -v` + `up`).

```powershell
./tools/seed-smoke.ps1
```

## Roadmap

- [x] Fase 0 — Setup, BuildingBlocks, plantilla de servicio
- [x] Fase 1 — Identity
- [x] Fase 2 — Merchants
- [x] Fase 3 — Catalog
- [x] Fase 4 — Pricing
- [x] Fase 5 — Orders (saga)
- [x] Fase 6 — Dispatch
- [x] Fase 7 — Tracking (push en vivo)
- [x] Fase 8 — Payments (Stripe hold+capture)
- [x] Fase 9 — Notifications (email Fake/SendGrid + 3 proyecciones locales + dedup por clave de negocio)
- [ ] Fase 10 — Observability + Ratings + Back-office + pulido final

## Licencia

MIT.
