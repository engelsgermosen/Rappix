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
| Dispatch | Asignación de couriers | 5006 |
| Tracking | GPS vivo y ETA | 5007 |
| Payments | Stripe y splits | 5008 |
| Notifications | Push, email, SMS | 5009 |
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
| gRPC interno | `PricingService.QuotePrice` · `ConsumeQuote` |

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

## Roadmap

- [x] Fase 0 — Setup, BuildingBlocks, plantilla de servicio
- [x] Fase 1 — Identity + Merchants
- [x] Fase 2 — Catalog + Pricing
- [ ] Fase 3 — Orders + Payments (saga)
- [ ] Fase 4 — Dispatch + Tracking (tiempo real)
- [ ] Fase 5 — Notifications + Ratings
- [ ] Fase 6 — Back-office + pulido final

## Licencia

MIT.
