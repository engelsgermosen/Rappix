# Configuración del Catalog Service (Fase 3)

Guía para arrancar el Catalog Service y probar el flujo end-to-end: el catálogo se crea por
**choreography** cuando Merchants aprueba un comercio (Merchants → Catalog), el owner crea items
multi-vertical con atributos y stock, se busca por full-text en español, y otros servicios consultan
precio/comprabilidad por **gRPC**.

> Catalog **valida** los JWT que emite Identity: usa la **misma** `Jwt:SigningKey`, `Issuer` y
> `Audience`. En docker reutiliza `IDENTITY_JWT_SIGNINGKEY` del `.env`. No emite tokens propios.

---

## 1. Requisitos e infraestructura

```powershell
docker compose up -d postgres redis rabbitmq seq minio
```

- **PostgreSQL** (`postgis/postgis:17-3.5`): el `postgres-init.sql` crea `rappix_catalog`. El full-text
  en español usa la config `spanish` incluida en PostgreSQL (no requiere extensión). No usa PostGIS.
- **MinIO** (`localhost:9000` API, `localhost:9001` consola, `rappix`/`rappix_minio_2026`): el bucket
  `catalog-items` lo crea automáticamente un `IHostedService` al arrancar el servicio.
- **Merchants** debe estar arriba para el gRPC de validación (con resiliencia + fallback al gating local).

---

## 2. Migración (esquema `catalog`)

```powershell
dotnet ef database update `
  --project src/Services/Catalog/Rappix.Catalog.Infrastructure `
  --startup-project src/Services/Catalog/Rappix.Catalog.Api
```

Crea `catalogs`, `categories`, `items` (con `attributes jsonb`, la columna generada
`search_vector tsvector` + índice **GIN**, y las columnas de `Money`), `modifiers`,
`modifier_options`, `stock_levels` (con el token de concurrencia `xmin`) y las tablas inbox/outbox.

---

## 3. Arrancar el servicio

```powershell
# Local (dos puertos cleartext: 8080 REST, 8081 gRPC). Define la clave JWT igual que Identity:
$env:Jwt__SigningKey = "TU_CLAVE_COMPARTIDA_CON_IDENTITY"
$env:Grpc__MerchantsUrl = "http://localhost:8081"   # servidor gRPC de Merchants
dotnet run --project src/Services/Catalog/Rappix.Catalog.Api
#   REST  -> http://localhost:8080/scalar/v1
#   gRPC  -> http://localhost:8081

# O en contenedor (REST 5003, gRPC 5013; requiere .env con IDENTITY_JWT_SIGNINGKEY):
docker compose up -d --build identity-api merchants-api catalog-api
#   REST  -> http://localhost:5003/scalar/v1
#   gRPC  -> localhost:5013
```

---

## 4. Smoke test del flujo (puertos de docker: Identity 5001 · Merchants 5002 · Catalog 5003)

El catálogo nace cuando Merchants **aprueba** un comercio. Primero completa el flujo de Merchants
(ver `docs/setup-merchants.md` pasos 5–7) hasta `approve`. Eso publica
`MerchantApprovedIntegrationEvent`, que el consumer de Catalog convierte en un catálogo habilitado.

```bash
TOKEN=...   # accessToken del merchant (userType=Merchant), el mismo de Merchants

# 1) El consumer ya creó el catálogo del merchant -> verlo (resuelve el MerchantId por el OwnerUserId del JWT)
curl -s http://localhost:5003/api/v1/catalog/me -H "Authorization: Bearer $TOKEN"

# 2) Crear una categoría
curl -sX POST http://localhost:5003/api/v1/catalog/me/categories -H "Authorization: Bearer $TOKEN" \
  -H 'Content-Type: application/json' \
  -d '{"name":"Pizzas","sortOrder":1}'

# 3) Crear un item con stock y atributos del vertical Food (valida atributos por vertical)
curl -sX POST http://localhost:5003/api/v1/catalog/me/items -H "Authorization: Bearer $TOKEN" \
  -H 'Content-Type: application/json' \
  -d '{"name":"Pizza Pepperoni","description":"Masa delgada","priceAmount":450.00,"currency":"DOP","tracksInventory":true,"initialStock":20,"attributes":{"calories":"1200","isVegetarian":"false"}}'
ITEM_ID=...   # id del item creado

# 4) Subir foto (multipart, máx 4 MB, PNG/JPEG/WebP)
curl -sX POST http://localhost:5003/api/v1/catalog/me/items/$ITEM_ID/photo -H "Authorization: Bearer $TOKEN" \
  -F 'file=@pizza.png;type=image/png'

# 5) Ajustar stock (Set = absoluto, Restock = suma)
curl -sX PUT http://localhost:5003/api/v1/catalog/me/items/$ITEM_ID/stock -H "Authorization: Bearer $TOKEN" \
  -H 'Content-Type: application/json' -d '{"mode":"Restock","quantity":10}'

# 6) Sin auth: búsqueda full-text en español (stemming: "pizzas" encuentra "Pizza")
curl -s "http://localhost:5003/api/v1/catalog/items/search?q=pizzas"

# 7) Sin auth: vista pública del item (solo si está disponible y el catálogo habilitado)
curl -s http://localhost:5003/api/v1/catalog/items/$ITEM_ID
```

Verifica en **Seq** (<http://localhost:5341>) los logs del consumer creando el catálogo, y en
**RabbitMQ** (<http://localhost:15672>) el exchange
`Rappix.Contracts.Merchants:MerchantApprovedIntegrationEvent` consumido por la cola
`catalog-merchant-approved`.

---

## 5. Probar el gRPC interno (GetItemPricing) con grpcurl

El servicio `catalog.v1.CatalogValidationService` escucha en `localhost:5013` (h2c). Sin reflection,
se pasa el `.proto` explícitamente:

```bash
grpcurl -plaintext \
  -import-path src/Services/Catalog/Rappix.Catalog.Api/Protos -proto catalog.proto \
  -d '{"item_id":"<ITEM_ID>"}' \
  localhost:5013 catalog.v1.CatalogValidationService/GetItemPricing
# -> {"found": true, "priceAmount": "450.00", "currency": "DOP", "isPurchasable": true, "stockQuantity": 30, ...}
```

`isPurchasable` combina el estado autoritativo del merchant (consultado por gRPC a Merchants, con
**fallback** al gating local si Merchants no responde), la disponibilidad del item y el stock.

> Los clientes gRPC deben usar HTTP/2 *prior-knowledge* (h2c): el puerto 8081 es cleartext.

---

## 6. Correr las pruebas

```powershell
dotnet test src/Services/Catalog/Rappix.Catalog.Tests
```

Requiere Docker (Testcontainers levanta PostgreSQL efímero; MinIO y el cliente gRPC de Merchants se
sustituyen con NSubstitute, sin RabbitMQ). Cubre los agregados, la validación de atributos por
vertical, el ciclo del owner por HTTP, la búsqueda full-text (con stemming), la no-sobreventa con
`xmin` bajo decrementos concurrentes, el servidor gRPC (incluido el fallback de gating) y los tres
consumers de eventos de Merchants.
