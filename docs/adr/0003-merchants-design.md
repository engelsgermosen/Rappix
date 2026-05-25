# ADR 0003 — Diseño del Merchants Service (choreography, geo, gRPC y logos)

- **Estado:** Aceptado
- **Fecha:** 2026-05-21
- **Contexto de fase:** Fase 2 (Merchants Service)

## Contexto

El Merchants Service gestiona comercios multi-vertical (Food, Pharmacy, Grocery, Parcel) con un
workflow de aprobación admin, zonas de cobertura geoespaciales, logos y un endpoint gRPC interno.
Es el primer servicio con **consumer de MassTransit**, **PostGIS**, **gRPC** y **MinIO** del proyecto.
Este ADR registra las decisiones de diseño no triviales.

## Decisiones

### 1. Alta de comercios por choreography (eventual consistency), no orquestación

Cuando un usuario se registra en Identity con `accountType=Merchant`, Identity publica
`UserRegisteredIntegrationEvent` (que ya transporta `UserType`). Merchants tiene un
`UserRegisteredConsumer` que, si `UserType == "Merchant"`, crea un `Merchant` en estado `Draft`.

- **Por qué choreography:** desacopla los servicios (Identity no conoce a Merchants) y evita una
  llamada síncrona en el camino crítico del registro. El precio es consistencia eventual: el Draft
  aparece milisegundos después del registro.
- **Idempotencia en dos capas:** el **inbox EF** de MassTransit garantiza once-only por `MessageId`,
  y el handler además verifica `ExistsByOwnerAsync` (cubre re-publicaciones con `MessageId` distinto).
- **Enabler (Fase 1):** se añadió `accountType` a `register` en Identity. Sin un disparador real
  (Identity v1 siempre creaba Customer), la choreography no tendría origen y un `UPDATE` manual en BD
  no re-publica el evento.

### 2. Dos columnas espaciales con discriminador, no una sola geometría

`ServiceArea` es **polígono o círculo** (nunca ambos), discriminado por `ServiceAreaType`:

- `Polygon` → columna `geometry(Polygon,4326)` (polígono custom).
- `Circle` → `Center` `geography(Point,4326)` + `RadiusMeters` (int).

Un **CHECK constraint** (`ck_service_areas_shape`) exige que exactamente una forma esté presente
según el tipo. Índices **GiST** sobre ambas columnas espaciales aceleran las consultas `nearby`.

- **Por qué no una sola geometría:** un círculo "verdadero" no es un polígono; aproximarlo con un
  buffer poligonal pierde precisión y complica `ST_DWithin`. Con `geography(Point)` + radio, las
  distancias se calculan en **metros reales** sobre el elipsoide.
- **geometry vs geography:** el polígono usa `geometry` (planar, suficiente para contención
  `ST_Contains` en zonas urbanas); el centro del círculo usa `geography` para que `ST_DWithin`
  mida metros sin reproyectar.

### 3. SRID 4326 (WGS 84) en todas las geometrías

Es el estándar global lat/lng (el que usan GPS, Mapbox y los clientes). Se centraliza la
construcción en un helper `GeoFactory` con el **orden de ejes correcto (X = longitud, Y = latitud)**,
el error más común con NetTopologySuite.

### 4. La búsqueda `nearby` usa SQL PostGIS explícito (no LINQ)

`SearchNearbyAsync` se implementa con `FromSqlRaw`: la unión de polígonos (`ST_Contains`, geometry) y
círculos (`ST_DWithin` sobre geography, con radio por fila) no se traduce de forma fiable en LINQ
(mezcla de tipos geometry/geography y cast a geography por fila). El SQL ordena por la zona más
cercana y pagina. Las geometrías de polígono sí se consultan bien con `ST_Contains`, pero el conjunto
mixto justifica el SQL explícito.

### 5. gRPC en dos puertos cleartext separados (no un puerto mixto)

Kestrel expone **8080 = REST (HTTP/1.1)** y **8081 = gRPC (HTTP/2 h2c)** en puertos distintos.

- **Por qué:** sin TLS, HTTP/2 no negocia con HTTP/1.1 en el mismo puerto (no hay ALPN); un puerto
  mixto cae a HTTP/1.1 y gRPC falla. La opción `Http1AndHttp2` cleartext en un solo puerto no es
  fiable. Con TLS se podría compartir, pero en dev/Docker interno se usa cleartext.
- **Configuración en código** (`ConfigureKestrel` con `ListenAnyIP`), **no** `ASPNETCORE_URLS`
  (que sobreescribiría los endpoints). docker-compose mapea `5002:8080` y `5012:8081`; otros
  servicios llaman gRPC en `http://merchants-api:8081`.
- La idempotencia (`UseIdempotency`) se **excluye del puerto gRPC** vía `UseWhen` (no aplica a gRPC).

### 6. Validación de logos con SixLabors.ImageSharp (solo header)

El endpoint de logo valida, **en orden**: tamaño ≤ 2 MB (early exit en el endpoint), formato por
magic bytes (**PNG/JPEG/WebP**; se rechaza SVG por XSS y GIF por complejidad), dimensiones entre
100×100 y 2000×2000, y relación de aspecto ≤ 5:1. Se lee **solo el header** (`Image.IdentifyAsync` /
`DetectFormatAsync`), sin decodificar la imagen completa. En Fase 2 **no** se regenera la imagen
(thumbnails/WebP quedan como TODO). El blob se guarda en MinIO; en BD solo vive la *object key*.

- **MinIO con doble endpoint:** las subidas usan el host interno (`minio:9000`), pero las URLs
  firmadas se generan con un cliente sobre el host **público** (`localhost:9000`) para que sean
  alcanzables desde el navegador del host.

## Consecuencias

- **Positivas:** servicios desacoplados; geo preciso e indexado; un contrato gRPC interno tipado para
  Orders/Catalog (Fases siguientes); validación de imágenes robusta sin almacenar basura.
- **Negativas / costes:** consistencia eventual (el Draft no es inmediato); SQL PostGIS a mano para
  `nearby` (menos portable que LINQ, documentado en código); dos puertos que gestionar para gRPC.
- **TODO futuro:** consumer de `MerchantRatingUpdatedIntegrationEvent` (Fase 5) para `AverageRating`
  /`TotalReviews`; thumbnails/normalización de logos; TLS para gRPC en entornos no locales.

## Follow-up Fase 13.6 — texto de pickup address

`Merchant.PickupLocation` es `Point` PostGIS (`SetPickupLocation(Point pickupLocation, …)`): solo
lat/lng, sin campo de calle. Esto deja un hueco en la asignación del courier: el portal recibe
`pickup: { merchantName, latitude, longitude }` y debe renderizar las coordenadas sin texto humano
("Recoger en: Av. X 123"). Para cerrarlo cuando se priorice:

- Agregar `PickupAddress` (string opcional, ≤ 200 chars) al agregado `Merchant`.
- Aceptarlo en `PUT /api/v1/merchants/me/pickup-location` (campo opcional para retro-compat).
- Exponerlo en `MerchantBasicInfoResponse` (gRPC) — el contrato ya viaja, solo es un campo más.
- Propagarlo a `MerchantInfo` en Orders → `Order` aggregate (snapshot al crear pedido) →
  `OrderSubmittedDomainEvent` → `CourierRequestedIntegrationEvent` → `AssignmentSnapshot.PickupAddress`
  en Dispatch → response del courier. Solo es un campo más en cadena, sin nuevos eventos.

Mientras tanto: el portal courier usa `pickup.latitude/longitude` con `pickup.merchantName` como
etiqueta humana y un botón "Abrir en mapa" — suficiente para entregar en v1.
