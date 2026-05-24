# ADR 0011 — Diseño del API Gateway con YARP (defensa en profundidad, rate limit, CORS, health aggregator, WebSocket passthrough)

- **Estado:** Aceptado
- **Fecha:** 2026-05-23
- **Contexto de fase:** Fase 10 (API Gateway) — cierre operacional del backend de Rappix

## Contexto

Con Notifications (Fase 9) cerrado, los 9 microservicios .NET están desplegados cada uno en su propio puerto host (5001-5009), validando JWT por separado, sin punto de entrada único ni rate limit global ni CORS centralizado. El frontend Next.js futuro (y los clientes E2E) necesitan un único endpoint público estable. Cada servicio mantiene su propia auth y políticas de rol — eso seguirá así (zero-trust interno), pero la primera barrera operacional se pone delante.

La Fase 10 introduce **Rappix.Gateway.Api** (YARP 2.3.0), expuesto en `http://localhost:5000`, que añade auth + rate limit + CORS + agregación de health + passthrough de WebSockets sin tocar el código de los 9 servicios.

Este ADR registra las decisiones no obvias.

## Decisiones

### 1. Defensa en profundidad de auth — el gateway valida, los downstream también

El gateway replica el bloque `AddJwtBearer` **idéntico** al que usan los 9 servicios (issuer/audience/HS256 key compartidos, `ClockSkew = 30s`, `NameClaimType = "sub"`, `MapInboundClaims = false`). `AddAuthorization` con `FallbackPolicy = RequireAuthenticatedUser()` exige token por default; las rutas públicas se marcan `AuthorizationPolicy: "Anonymous"` por config YARP en `appsettings.json`. Los downstream **siguen validando JWT** y aplicando sus role policies (`RequireAdmin`/`RequireCustomer`/`RequireCourier`).

**Por qué la duplicación es valor**:
- **Zero-trust interno**: si alguien evade el gateway (red docker comprometida, port-forward al servicio individual), el servicio sigue exigiendo JWT válido.
- **Atajo de carga**: el 401 se devuelve **en el gateway** sin tocar el downstream. Un atacante que mande basura sin token no consume recursos de Identity/Orders/etc.
- **Cero acoplamiento de roles**: el gateway nunca aprende la matriz de roles. Si mañana se añade un nuevo role en Merchants, no hay que tocar el gateway. Un Customer que pegue al endpoint admin va a llegar al downstream (200/forward del gateway) y el `RequireAdmin` policy de Merchants devolverá 403. **El gateway nunca bloquea por rol — solo por autenticación**.

### 2. WebSocket passthrough para el hub de Tracking — el gateway NO inspecciona el JWT del query string

`/hubs/tracking` (SignalR del servicio Tracking) usa `?access_token=...` en query string para el handshake — el patrón estándar que SignalR + Bearer adopta cuando el browser no puede inyectar `Authorization` headers en el WebSocket upgrade. Tracking ya tiene un `OnMessageReceived` en su `AddJwtBearer` que extrae el token del query **solo si el path inicia con `/hubs/tracking`**.

**Decisión**: la ruta `tracking-hub` en YARP queda marcada `AuthorizationPolicy: "Anonymous"`. El gateway NO valida el JWT del query string. El handshake pasa transparente al cluster `tracking` (`HttpRequest.Version=1.1 / VersionPolicy=RequestVersionExact` — requisito del upgrade WebSocket), y Tracking valida el `access_token` en su `OnMessageReceived` (defensa en profundidad — el `[Authorize]` del `TrackingHub` falla si el token es inválido).

**Por qué no replicar `OnMessageReceived` en el gateway**:
- Acoplaría el gateway al detalle de cómo Tracking espera el token (query name `access_token`, path prefix exacto).
- Duplica config sin valor: si el gateway lo valida y pasa, Tracking lo vuelve a validar igual.
- Cambia el contrato existente — clientes que hoy conectan directamente a `tracking-api:5007` con `?access_token=...` siguen funcionando idénticamente vía el gateway.

**Costo aceptado**: handshakes sin token válido entran al gateway, pasan a Tracking, Tracking devuelve 401, cliente recibe 401. El **rate limit anónimo (100 req/min por IP)** sí los frena porque cada handshake es un request HTTP/1.1 contado. Suficiente para Fase 10.

`app.UseWebSockets()` se monta **antes** de `app.MapReverseProxy()` (YARP detecta `Upgrade: websocket` y hace passthrough nativo del frame).

### 3. Rate limiting con 2 políticas + 1 exención (webhook Stripe)

`Microsoft.AspNetCore.RateLimiting` (shared framework, sin PackageReference) con un `GlobalLimiter` que decide por partition:

| Partición | Selector | Política | Límite |
|---|---|---|---|
| `stripe-webhook` | `path.StartsWithSegments("/payments/webhooks/stripe")` | **NoLimiter** | exento |
| `u:{sub}` | `User.IsAuthenticated == true` | Sliding window (6 segments) | **300 req/min** |
| `ip:{RemoteIpAddress}` | resto | Fixed window | **100 req/min** |

Webhook Stripe está exento porque las llamadas vienen de IPs de Stripe (concentradas); rate-limit anónimo por IP las cortaría todas en un pico. La autenticidad la cubre la firma `Stripe-Signature` que el handler de Payments valida.

`OnRejected` devuelve **429** con header `Retry-After` (segundos restantes en la ventana) y body `application/problem+json`. `app.UseRateLimiter()` se monta **después** de `UseAuthentication`/`UseAuthorization` para que el partitioner ya tenga el claim `sub` resuelto.

Límites configurables vía `RateLimit:Anonymous:PermitLimit` / `WindowSeconds` y `RateLimit:Authenticated:*` — los tests overridean a `PermitLimit=3/5, Window=10s` para disparar 429 en pocas requests sin esperar 1 minuto. Los **300 req/min auth** son una estimación de Fase 10; ajustar tras observar Seq en producción.

### 4. Health aggregator con `failureStatus: Degraded` — el gateway NO cae por un downstream

`AddHealthChecks` itera los 9 clusters de `ReverseProxy:Clusters` y registra un `DownstreamUrlHealthCheck` por cada uno (custom `IHealthCheck` que lee el address en RUNTIME via `IConfiguration` — el `AspNetCore.HealthChecks.Uris.AddUrlGroup` lo lee en startup, lo cual rompe los tests con `WebApplicationFactory` que override config in-memory). `MapHealthChecks("/health")` con `UIResponseWriter.WriteHealthCheckUIResponse` devuelve JSON con un array de 9 entries.

**`failureStatus: Degraded`** (no `Unhealthy`): si Notifications devuelve 503, el entry de Notifications se marca `Degraded` pero el estado global agregado sigue dando HTTP 200. El gateway no se considera "caído" por un downstream caído — los orchestrators (k8s, Docker Swarm) NO lo reiniciarían por eso. Solo se cae el gateway si su propio proceso falla.

**`/health/live`** independiente devuelve 200 sin tocar downstreams — es el target del healthcheck de Docker compose (probe cada 30s no debe pingar 9 servicios cada vez).

**Pre-requisito**: los 5 servicios que NO tenían `/health` (Identity, Merchants, Catalog, Pricing, Orders) lo añadieron en commit aislado [d18b3f2..1ad14e9](commit antes del trabajo del gateway). Sin ese paso, el aggregator marcaba 5/9 downstream como Degraded permanentemente (404).

### 5. YARP 2.3.0, solo `Rappix.Gateway.Api` (sin Domain/Application/Infrastructure)

El gateway es **config-driven**: rutas y clusters viven en `appsettings.json` (`ReverseProxy:Routes` + `ReverseProxy:Clusters`). 15 rutas (incluida `tracking-hub` y `payments-webhook-stripe`) sobre 9 clusters. La distinción crítica `/api/v1/admin/merchants/*` vs `/api/v1/admin/pricing/*` es **disjunta por path** (matcher YARP por especificidad, sin `Order` explícito). Las rutas `/api/v1/auth/me` y `/api/v1/catalog/me/*` usan `Order: -10` para matchear ANTES de las rutas Anonymous más amplias del mismo prefijo (`/api/v1/auth/{**catch-all}` y `/api/v1/catalog/{**catch-all}`).

**Solo Api + Tests** (sin Domain/Application/Infrastructure): el gateway no tiene agregados, ni reglas de negocio, ni persistencia, ni mensajería. Las "decisiones" viven en `appsettings.json`. Replicar las capas DDD vacías sería ceremonia muerta — ADR-0001 acepta deviaciones cuando "el servicio no tiene dominio propio". Refs únicas a building blocks compartidos: `Observability` (Serilog→Seq idéntico al resto) y `WebApi` (CorrelationId middleware).

### 6. ASPNETCORE_ENVIRONMENT=Docker en compose + `appsettings.Docker.json`

`appsettings.json` (base) tiene destinos `http://localhost:500X/` para correr `dotnet run` fuera de Docker apuntando a los puertos host de los services. `appsettings.Docker.json` override los destinos a `http://*-api:8080/` (nombres internos de la red `rappix-net`). El compose usa `ASPNETCORE_ENVIRONMENT: Docker` (no `Development`/`Production`) para evitar tener que crear `appsettings.Production.json` con URLs de docker — Production real (cuando llegue) será diferente.

### 7. No consolidar OpenAPI en Fase 10

El landing `GET /` devuelve HTML estático con links a los 9 Scalar UIs en sus puertos host (`http://localhost:5001/scalar/v1`, etc.). Consolidar los 9 OpenAPI documents en un único Scalar agregado queda como follow-up — no aporta valor inmediato y `Microsoft.AspNetCore.OpenApi` 10 soporta merging (follow-up alguien con tiempo).

### 8. Bump de OpenTelemetry a 1.15.x (resuelve 3 CVEs + desbloquea WireMock.Net 2.6.0)

WireMock.Net 2.6.0 (mock de los 9 downstream en los tests) jala transitivamente `OpenTelemetry.Exporter.OpenTelemetryProtocol >= 1.15.3`. El CPM tenía OpenTelemetry pinneado en `1.10.0/1.10.1`, lo cual:
1. Disparaba `NU1109` (downgrade detectado vs WireMock).
2. Disparaba 3 `NU1902` (vulnerabilidades): `OpenTelemetry.Api` (CVE-2025-27513, fix 1.11.2) y `OpenTelemetry.Exporter.OpenTelemetryProtocol` (CVE-2026-42191, fix 1.15.3).

**Decisión**: subir las 5 versiones de OpenTelemetry en `Directory.Packages.props` a `1.15.3` (excepto `Instrumentation.AspNetCore` que la última estable es `1.15.2` y satisface el requirement transitivo de WireMock). Ningún csproj referencia OpenTelemetry directamente — el bump solo afecta a las deps transitivas. Cero código modificado en los 9 servicios.

## Consecuencias

**Positivas**:
- Single entry point en `:5000` — el frontend habla a un solo host, una sola IP/dominio expuesta al public.
- Telemetría unificada — Seq filtrable por `Service: gateway` muestra qué cluster sirvió cada request (Yarp logs).
- Cero impacto en los 430 tests previos — el gateway es proyecto nuevo. Los 5 services del Paso 0 (añadir `/health`) no rompen tests existentes.
- **30 tests del gateway verdes** (32 pasados + 1 SKIP del WS E2E opt-in): smoke, routing, auth (defensa en profundidad incluyendo "Customer pasa hacia /admin sin que el gateway lo bloquee"), rate-limit (3 partitions independientes), CORS (preflight allowed/disallowed), WebSockets sanity (negotiate llega al cluster sin token), health aggregator (1 entry Degraded → 200 global).
- Las 3 CVEs de OpenTelemetry transitivas resueltas como side-effect.

**Negativas / riesgos**:
- **SPOF**: una sola instancia del gateway. Mitigable en Fase 11 con `replicas: N` en compose/k8s + un load balancer (nginx/Traefik/L4 cloud) delante.
- **Latencia extra**: +1-3 ms por hop intra-docker. Despreciable para REST; para SignalR el WebSocket es persistente (un solo upgrade), latencia despreciable también.
- **Rate limit en memoria**: si se escala el gateway a N réplicas, cada una tiene su partition independiente → un atacante con N IPs efectivas. Follow-up: rate limiter Redis-backed (no soportado out-of-the-box por `Microsoft.AspNetCore.RateLimiting`; existen comunitarios como `RedisRateLimiting`).
- **Healthchecks de los 9 services NO definidos en compose**: el gateway usa `depends_on: service_started` (no `service_healthy`). Refleja el estado actual del repo. Follow-up: añadir `healthcheck:` a los 9 services API en compose para poder usar `service_healthy`.
- **TestServer no soporta WebSocket nativo**: el test SignalR Client va con `[SkippableFact]` opt-in via `GATEWAY_E2E_WS=1`. La validación real es el smoke E2E manual (Paso 14).

## Alternativas consideradas (descartadas)

- **Ocelot** — más maduro pero menos activo, menos performance que YARP. YARP es de Microsoft, .NET 10-compatible nativamente, .NET-only (sin proxy de procesos JVM/Go separados). Para Rappix (.NET 10 puro) YARP es la elección obvia.
- **Envoy/nginx delante** — requiere infraestructura adicional, no se integra con la stack .NET para auth/health checks, mayor latencia. Reservado para el L4 LB futuro.
- **Validar el JWT del query string en el gateway para el hub de SignalR** — ver §2 arriba; duplica config sin beneficio porque el destino ya valida.
- **Consolidar OpenAPI en Fase 10** — sobre-ingeniería; follow-up.
- **`AddUrlGroup` para los health checks** — lee el address en startup, no en runtime; rompe los tests con `WebApplicationFactory` que override config in-memory. Reemplazado por `DownstreamUrlHealthCheck` custom (§4).

## Follow-ups documentados

- Backplane Redis para SignalR cuando Tracking escale a múltiples réplicas (el gateway necesitaría sticky sessions o el backplane lo resuelve).
- Consolidar OpenAPI de los 9 servicios en un único Scalar agregado.
- Idle timeouts en SignalR (ajustar `Cluster.HttpClient.RequestTimeout` si aparecen desconexiones espontáneas tras observación).
- `healthcheck:` definido en los 9 services API de compose → `depends_on: service_healthy` en el gateway.
- Métricas Prometheus de YARP (`AddPrometheusExporter()` + `MapPrometheusScrapingEndpoint()`).
- Rate limiter Redis-backed para soportar N réplicas del gateway.
- Reintentos de migrate-on-startup (issue 57P03 conocido — con 10 servicios arrancando juntos es peor).
- Inspeccionar/refrescar tokens en el gateway si en el futuro se introduce OpenIddict (Authorization Code + PKCE).
