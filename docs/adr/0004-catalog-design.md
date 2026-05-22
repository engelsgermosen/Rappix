# ADR 0004 — Diseño del Catalog Service (JSONB por vertical, full-text, gRPC cliente+servidor, stock)

- **Estado:** Aceptado
- **Fecha:** 2026-05-21
- **Contexto de fase:** Fase 3 (Catalog Service)

## Contexto

El Catalog Service gestiona el catálogo de cada merchant: items multi-vertical con atributos
flexibles, categorías, grupos de modificadores, stock en vivo y búsqueda full-text. Es el primer
servicio que **consume un cliente gRPC** (habla con `MerchantValidationService` de Merchants) además
de exponer **su propio servidor gRPC**. Reaplica la plantilla de Identity/Merchants y reutiliza los
building blocks compartidos. Este ADR registra las decisiones no triviales.

## Decisiones

### 1. Atributos por vertical en JSONB, no columnas por vertical ni EAV

Un item tiene atributos que dependen del vertical (Food: `spicyLevel`, `calories`, `isVegetarian`…;
Pharmacy: `requiresPrescription`, `activeIngredient`…). Se modelan como un diccionario
`Dictionary<string,string>` mapeado a una columna **`jsonb`** (vía `ValueConverter` a JSON serializado
+ `ValueComparer` para el change-tracking de la colección).

- **Por qué JSONB:** evita una tabla EAV (consultas penosas) y evita decenas de columnas nullable por
  vertical. El esquema permanece estable aunque los atributos de un vertical cambien.
- **Validación en la capa de aplicación:** las reglas por vertical (`VerticalAttributeRules`, función
  pura) validan claves permitidas, obligatorias y tipos (booleano/numérico) **antes** de persistir.
  El dominio solo almacena el bag; la semántica vive en Application (fácil de testear y de extender).
- **Trade-off:** no se consulta por atributos en SQL (la validación es post-carga). Si en el futuro
  hace falta filtrar por atributo, se añade un índice GIN de contención sobre la columna jsonb.

### 2. Búsqueda full-text en español con columna generada `tsvector` + índice GIN

Los items se buscan por nombre/descripción con full-text **español** (stemming, p. ej. "empanadas"
encuentra "empanada"). Se mapea una **columna generada almacenada** `search_vector tsvector`
(`to_tsvector('spanish', coalesce(Name,'') || ' ' || coalesce(Description,''))`, `STORED`) con un
índice **GIN**, consultada vía `EF.Property<NpgsqlTsVector>(item,"SearchVector").Matches(plainto_tsquery('spanish', term))`.

- **Por qué columna generada + GIN:** el índice GIN sobre la columna se usa de forma fiable (no
  depende de que la expresión del query coincida con un índice de expresión). La columna se mantiene
  sola en cada `INSERT`/`UPDATE`.
- **Inmutabilidad:** `to_tsvector('spanish', …)` con el regconfig literal es inmutable, requisito de
  las columnas generadas en PostgreSQL.
- **Fallback (no necesario):** si el `tsvector` generado hubiera dado guerra (mapeo EF o migración),
  el plan era degradar a `ILIKE`. Verificado contra PostgreSQL 17 real: el full-text funciona, así que
  ILIKE queda solo como fallback documentado.

### 3. Stock como agregado independiente con concurrencia optimista (xmin)

`StockLevel` es un **agregado separado** (PK = `ItemId`, 1:1 con el item) en su propia tabla, con el
token de concurrencia **`xmin`** de PostgreSQL (`Property<uint>("xmin").HasColumnType("xid").IsConcurrencyToken()`,
columna de sistema que Npgsql excluye del `CREATE TABLE`).

- **Por qué separado del item:** dos decrementos concurrentes (pedidos) chocan **solo** en la fila de
  stock, no con ediciones del item. Evita conflictos falsos entre editar el item y vender.
- **Evita la sobreventa:** ante decrementos concurrentes, uno gana y el otro recibe
  `DbUpdateConcurrencyException`, que el DbContext traduce a una `ConcurrencyConflictException` y el
  handler a `Error.Conflict` (`Catalog.Stock.ConcurrencyConflict`). El llamador reintenta. Probado con
  20 decrementos concurrentes sobre stock 10 → exactamente 10 éxitos, 10 insuficientes, final 0.
- **EF sin acoplar:** la `ConcurrencyConflictException` vive en Application; Infraestructura la lanza y
  los handlers la atrapan sin referenciar EF Core.

### 4. Gating por estado del merchant: eventos (cache local) + gRPC (autoritativo) con fallback

El catálogo refleja si el merchant está activo (`IsEnabled`). El estado se mantiene por **choreography**:

- `MerchantApprovedIntegrationEvent` → crea el catálogo (habilitado) y cachea `VerticalType` + `OwnerUserId`.
- `MerchantActivatedIntegrationEvent` / `MerchantSuspendedIntegrationEvent` → habilita/deshabilita.
- Idempotencia: inbox EF de MassTransit + `ExistsByMerchantAsync` en el comando.

Para lecturas críticas entre servicios (`GetItemPricing` por gRPC, que consumirán Orders/Pricing) se
consulta el **estado autoritativo** del merchant vía el cliente gRPC a Merchants
(`IMerchantValidationClient`). Si el servicio Merchants no responde (timeout / circuito abierto), se
hace **fallback al `IsEnabled` local cacheado**. Las escrituras del owner se gatean solo con el flag
local (sin gRPC en el camino caliente de escritura).

### 5. Primer cliente gRPC del proyecto, con resiliencia, + servidor gRPC propio

- **Cliente:** copia *client-only* de `merchants.proto` (csharp_namespace propio), registrado con
  `AddGrpcClient<MerchantValidationService...Client>().AddStandardResilienceHandler()`
  (Microsoft.Extensions.Http.Resilience + Polly: retry + circuit breaker). El adaptador
  `MerchantValidationGrpcClient` traduce `RpcException` a "servicio no disponible" para el fallback.
- **Servidor:** `CatalogValidationService.GetItemPricing` en el puerto 8081 (h2c), delega en MediatR.
  El precio viaja como **string** para preservar la precisión decimal del dinero. Dos puertos Kestrel
  cleartext (8080 REST / 8081 gRPC), idempotencia excluida del puerto gRPC (igual que Merchants,
  ADR-0003). `<Protobuf GrpcServices="Both">` para que las pruebas usen el cliente generado sobre el
  TestServer.

### 6. Fronteras de agregado y reuso (regla de tres)

- `MerchantCatalog` (raíz; **se llama así, no `Catalog`**, para no chocar el tipo con el namespace
  `Rappix.Catalog`) contiene las `Category` como entidades hijas. `Item` es un **agregado aparte**
  (numeroso, editable y buscable de forma independiente) que referencia la categoría **por id**.
  `Money` se mapea con `OwnsOne` (monto + moneda). Ids fuertemente tipados solo en las raíces
  (`CatalogId`, `ItemId`); las entidades hijas usan `Guid` (igual que Merchants).
- **Reuso:** consume los building blocks promovidos en la regla de tres —
  `BuildingBlocks.Storage` (MinIO, bucket `catalog-items`), `BuildingBlocks.Imaging` (validación de
  fotos), `BuildingBlocks.WebApi.Authentication` (`GetUserId`)— sin recrear nada.

## Consecuencias

- **Positivas:** esquema estable y flexible por vertical; búsqueda en español indexada; stock sin
  sobreventa; lecturas entre servicios resilientes (gRPC con fallback); reuso real de building blocks.
- **Negativas / costes:** consistencia eventual del gating (un suspend tarda milisegundos en reflejarse
  vía evento; el gRPC mitiga en el camino crítico); no se consulta por atributos en SQL; el `GetItemPricing`
  añade una llamada gRPC por consulta (cacheable a futuro).
- **TODO futuro:** índice GIN de contención sobre `attributes` si se filtra por atributo; thumbnails de
  fotos; consumir el contrato de Pricing (Fase 4) para precios dinámicos; ranking por `ts_rank` en la búsqueda.
