# Configuración del Pricing Service (Fase 4)

Guía para arrancar el Pricing Service y probar el flujo de cotización: el cliente arma un carrito,
**cotiza** (`POST /pricing/quotes`) y recibe un desglose transparente con `quoteId` y expiración; Orders
(fase futura) **consumirá** esa cotización por gRPC. Los precios salen de **Catalog vía gRPC** (con fallback
al cache local) y el merchant se valida contra **Merchants vía gRPC**.

> Pricing **valida** los JWT que emite Identity: usa la **misma** `Jwt:SigningKey`, `Issuer` y `Audience`.
> En docker reutiliza `IDENTITY_JWT_SIGNINGKEY` del `.env`. No emite tokens propios. Cotizar requiere
> `userType=Customer`; administrar surge/cupones requiere `userType=Admin`.

---

## 1. Requisitos e infraestructura

```powershell
docker compose up -d postgres redis rabbitmq seq minio
```

- **PostgreSQL** (`postgis/postgis:17-3.5`): el `postgres-init.sql` crea `rappix_pricing`. No usa PostGIS.
- **Redis**: respaldo de la idempotencia del middleware REST (`Idempotency-Key`).
- **RabbitMQ**: outbox de `QuoteCreated`/`CouponRedeemed` y consumo de `ItemCreated` de Catalog.
- **Catalog** y **Merchants** deben estar arriba para los gRPC de precios y validación (con resiliencia +
  fallback: si Catalog cae se usa el `ItemPriceCache` local; si Merchants cae se procede sin bloquear).

---

## 2. Migración (esquema `pricing`)

```powershell
dotnet ef database update `
  --project src/Services/Pricing/Rappix.Pricing.Infrastructure `
  --startup-project src/Services/Pricing/Rappix.Pricing.Api
```

Crea `quotes` (con el desglose embebido y las columnas `numeric(19,4)`), `quote_lines`, `coupons` (con el
token de concurrencia `xmin` y el código único), `coupon_redemptions`, `surge_rules`, `item_price_cache` y
las tablas inbox/outbox.

---

## 3. Arrancar el servicio

```powershell
# Local (dos puertos cleartext: 8080 REST, 8081 gRPC). Define la clave JWT igual que Identity:
$env:Jwt__SigningKey = "TU_CLAVE_COMPARTIDA_CON_IDENTITY"
$env:Grpc__CatalogUrl = "http://localhost:8081"     # servidor gRPC de Catalog
$env:Grpc__MerchantsUrl = "http://localhost:8081"   # servidor gRPC de Merchants
dotnet run --project src/Services/Pricing/Rappix.Pricing.Api
#   REST  -> http://localhost:8080/scalar/v1
#   gRPC  -> http://localhost:8081

# O en contenedor (REST 5004, gRPC 5014; requiere .env con IDENTITY_JWT_SIGNINGKEY):
docker compose up -d --build identity-api merchants-api catalog-api pricing-api
#   REST  -> http://localhost:5004/scalar/v1
#   gRPC  -> localhost:5014
```

> En docker cada servicio gRPC vive en su propio host (`catalog-api:8081`, `merchants-api:8081`); en local
> ambos comparten `localhost:8081`, así que para un end-to-end completo conviene usar contenedores.

---

## 4. Smoke test del flujo (puertos de docker: Identity 5001 · Merchants 5002 · Catalog 5003 · Pricing 5004)

Prepara primero el catálogo siguiendo las guías previas: completa el flujo de Merchants hasta `approve`
(eso crea el catálogo por choreography) y crea items en Catalog (ver `docs/setup-catalog.md`). Necesitarás
el `merchantId`, los `itemId` y dos tokens de Identity: uno `Customer` y uno `Admin`.

```bash
CUSTOMER=...   # accessToken con userType=Customer
ADMIN=...      # accessToken con userType=Admin
MERCHANT_ID=...
ITEM_ID=...

# 1) Admin: crear un cupón de 10% (global, vigente)
curl -sX POST http://localhost:5004/api/v1/admin/pricing/coupons -H "Authorization: Bearer $ADMIN" \
  -H 'Content-Type: application/json' -d '{
    "code":"WELCOME10","discountType":"Percentage","value":10,
    "validFromUtc":"2026-01-01T00:00:00Z","validUntilUtc":"2027-01-01T00:00:00Z",
    "maxUses":1000,"minOrderAmount":100,"perUserLimit":3
  }'

# 2) Admin: crear una regla de surge (almuerzo 12-14h local, x1.3, zona Z1)
curl -sX POST http://localhost:5004/api/v1/admin/pricing/surge-rules -H "Authorization: Bearer $ADMIN" \
  -H 'Content-Type: application/json' \
  -d '{"zoneId":"Z1","vertical":"Food","startHour":12,"endHour":14,"multiplier":1.3,"priority":1}'

# 3) Cliente: cotizar el carrito (precio de los items resuelto por gRPC a Catalog)
curl -sX POST http://localhost:5004/api/v1/pricing/quotes -H "Authorization: Bearer $CUSTOMER" \
  -H 'Content-Type: application/json' -d "{
    \"merchantId\":\"$MERCHANT_ID\",\"vertical\":\"Food\",\"distanceKm\":5,\"zoneId\":\"Z1\",
    \"tip\":50,\"couponCode\":\"WELCOME10\",\"isFirstOrder\":true,
    \"lines\":[{\"itemId\":\"$ITEM_ID\",\"quantity\":2,\"modifierTotal\":0}]
  }"
# -> { "quoteId":"...", "currency":"DOP", "breakdown":{
#      "subtotal":..., "surgeMultiplier":1.3, "surgeAmount":..., "discountAmount":...,
#      "deliveryFee":135.00, "serviceFee":..., "tax":..., "tip":50.00, "total":... },
#      "couponCode":"WELCOME10", "status":"Active", "expiresAtUtc":"..." }
QUOTE_ID=...

# 4) Recuperar la cotización vigente
curl -s http://localhost:5004/api/v1/pricing/quotes/$QUOTE_ID -H "Authorization: Bearer $CUSTOMER"

# 5) Tras 10 minutos (o con una cotización vencida): GET devuelve 409 Pricing.Quote.Expired
#    (expiración perezosa: la cotización queda marcada Expired al leerse vencida).
```

Verifica el **desglose**: subtotal de Catalog × surge − descuento del cupón + envío + service fee + ITBIS +
propina, con el total = suma exacta de los componentes. En **Seq** (<http://localhost:5341>) verás el
consumer cacheando precios de `ItemCreated` y los fallbacks gRPC si Catalog/Merchants no responden.

---

## 5. Probar el gRPC interno (PricingService) con grpcurl

El servicio `pricing.v1.PricingService` escucha en `localhost:5014` (h2c). Los montos viajan como string:

```bash
grpcurl -plaintext \
  -import-path src/Services/Pricing/Rappix.Pricing.Api/Protos -proto pricing.proto \
  -d '{"customer_user_id":"<GUID>","merchant_id":"<MERCHANT_ID>","vertical":"Food","distance_km":"5","tip":"0","lines":[{"item_id":"<ITEM_ID>","quantity":2,"modifier_total":"0"}]}' \
  localhost:5014 pricing.v1.PricingService/QuotePrice
# -> { "success": true, "quoteId": "...", "breakdown": { "total": "...", ... }, "status": "Active" }

# Consumir la cotización (lo invocará Orders): marca Consumed y redime el cupón si lo hubo.
grpcurl -plaintext \
  -import-path src/Services/Pricing/Rappix.Pricing.Api/Protos -proto pricing.proto \
  -d '{"quote_id":"<QUOTE_ID>"}' \
  localhost:5014 pricing.v1.PricingService/ConsumeQuote
# -> { "success": true, "status": "Consumed" }
```

> Los clientes gRPC deben usar HTTP/2 *prior-knowledge* (h2c): el puerto 8081 es cleartext.

---

## 6. Correr las pruebas

```powershell
dotnet test src/Services/Pricing/Rappix.Pricing.Tests
```

Requiere Docker (Testcontainers levanta PostgreSQL efímero; los clientes gRPC de Catalog y Merchants se
sustituyen con NSubstitute, sin RabbitMQ). Cubre **exhaustivamente** el `QuoteCalculator` (orden de cálculo,
cada componente, tope de descuento, base gravable, propina no gravable, redondeo bancario), los agregados y
value objects, las estrategias de surge/envío/descuento, el ciclo de vida de la cotización por HTTP, la
administración de cupones y surge, la **no-superación de `MaxUses` con `xmin` bajo consumos concurrentes**,
el servidor gRPC y el consumer de precios.
