# Pendientes Rappix

Actualizado: 2026-10-28

## Entorno local
- [x] Subir memoria de Docker Desktop a 12 GB (evita el corte de dockerd en builds completos)
- [x] Guardar el seed bash como `tools/seed-smoke.sh` (idempotente; el `.ps1` original queda intacto)
- [x] Confirmar que `docker-compose.override.yml` esté en `.git/info/exclude` y no aparezca en `git status`
- [ ] Gateway en `:5100` por conflicto con AirPlay (macOS). Para volver a `:5000`: desactivar Receptor AirPlay, borrar el override y reconstruir los 3 frontends (`NEXT_PUBLIC_API_URL` es build-time)
- [ ] Cambiar credenciales de RabbitMQ y MinIO si algún día se expone fuera de la máquina local

## Claves de terceros (desactivadas por ahora)
- [ ] Google Maps (customer-web, merchant-web, courier-web): `GOOGLE_MAPS_API_KEY` y `GOOGLE_MAPS_MAP_ID` en `.env` (activar solo "Maps JavaScript API"), luego reconstruir los 3 frontends de uno en uno. Restringir la key por HTTP referrer (`localhost:3000-3002` + prod). `COURIER_GOOGLE_MAPS_*` queda como alias legado de courier-web
- [ ] Map ID real de Google Maps para producción: sin `GOOGLE_MAPS_MAP_ID` los mapas usan `DEMO_MAP_ID`, válido solo en desarrollo
- [ ] Stripe (customer-web y Payments): `CUSTOMER_STRIPE_PUBLISHABLE_KEY` en `.env`, luego reconstruir customer-web
- [ ] SendGrid (Notifications en modo Fake: los emails solo se registran en Seq)
- [ ] Google OAuth (login con Google no funciona)

## Documentación
- [x] README: cita `tools/apply-migrations.sh`, que no existe (las migraciones EF se aplican al arrancar cada servicio)
- [x] README: RabbitMQ no es `guest/guest`
- [x] README: dice "Back-office" donde ahora está courier
- [ ] Ratings: solo existe la base `rappix_ratings` vacía, sin código ni servicio en el compose. Decidir si se implementa o se quita la DB

## Infraestructura
- [x] Healthchecks en el compose para servicios .NET y gateway (`/health`, gateway `/health/live`)
- [x] Healthchecks de los 3 frontends (route handler `/api/health`, sondeado con node) y de Seq (`/health`)
- [ ] Aspire Dashboard sin healthcheck: la imagen es distroless (sin shell, curl, wget ni node). Solo con un sidecar o una imagen propia
- [ ] Actualizar Next.js (15.1.3 tiene avisos de seguridad); correr `pnpm audit` en los 3 frontends, en un cambio aparte

## Observabilidad
- [x] Validar el flujo completo de un pedido hasta `Completed`: `tools/e2e-order-flow.sh` (vía gateway). Trazas de crear, aceptar, ubicación y entrega relacionadas en Aspire; la revisión visual queda en "Verificaciones manuales mías"
- [x] Filtrar el span de telemetría sobre telemetría (`opentelemetry.proto.collector.logs.v1.LogsService/Export`)
- [x] Instrumentar el gateway: `builder.AddRappixObservability("gateway")`, para que la traza empiece en la entrada
- [x] Instrumentar Pricing (Orders lo llama por gRPC) y luego Catalog y Merchants
- [x] Instrumentar Identity, Dispatch (con Redis), Tracking (más la fuente SignalR), Payments y Notifications: los 9 servicios y el gateway quedan con OTel
- [x] Redis instrumentado en Pricing y Merchants (patrón de Orders: `IConnectionMultiplexer` compartido con el cache)
- [x] TraceId y SpanId en los logs de consola (`UseRappixLogging`)
- [x] Spans del hub SignalR de Tracking verificados con `tools/signalr-tracking-probe.mjs`: `TrackingHub/OnConnectedAsync`, `Subscribe`, `Unsubscribe` y `OnDisconnectedAsync` (fuente `Microsoft.AspNetCore.SignalR.Server`). Cada invocación del hub es una traza propia, no hija del `GET /hubs/tracking` del WebSocket
- [ ] Unificar `X-Correlation-Id` con `Activity.Current.TraceId` en `CorrelationIdMiddleware`
- [ ] Quartz: `OpenTelemetry.Instrumentation.Quartz` si los jobs importan
- [ ] Producción: sampler `ParentBased(TraceIdRatioBased)` y OpenTelemetry Collector delante del backend

## Repositorio
- [x] Ordenar ramas: PRs a main con merge commit, borrado de ramas mergeadas y tags `backup/*` de respaldo

## Tests
- [x] `Catalog.MerchantEventConsumerTests.MerchantSuspended_DisablesCatalog` intermitente con las suites en paralelo: esperaba con `Harness.Consumed.Any` y leía la base al instante. `Consumed` también cuenta los consumes con fault y su espera depende de la inactividad del harness. Ahora sondea el catálogo hasta el estado esperado (timeout 30 s) y, si vence, falla con el último estado y los faults
- [x] Helper compartido `Rappix.BuildingBlocks.Testing.Eventually` (`UntilAsync` sondea con timeout y falla con el último estado y los faults; `ConsumedAsync` espera consumos terminados y falla si alguno tuvo fault). Aplicado:
  - (a) los 7 bucles que volvían en silencio al agotar el tiempo (Catalog, Dispatch ×2, Merchants, Payments ×3): ningún test empezó a fallar al quitar el falso verde
  - (b) los 2 `Task.Delay(500)` (`Tracking.OrderSubmittedConsumerTests`, `Notifications.DoubleDeliveryIdempotencyTests`)
  - (c) 37 `Consumed.Any` de Tracking y Notifications (la mayoría ignoraba el resultado) y `Catalog.MerchantApproved_CreatesEnabledCatalog`
- [x] Cuelgue de los tests (no solo Merchants): reproducido 3/3 con `--blame-hang-timeout 5m` en Pricing, Catalog, Merchants y Orders. Los dumps muestran el fixture parado en la espera `pg_isready` de Testcontainers 4.1 (`docker exec` que nunca termina con ~15-20 postgis emulados arrancando a la vez; esa espera no tiene timeout). Corregido con `ContainerStartup.StartAsync` (máx. 3 arranques simultáneos entre procesos, timeout de 3 min): 5/5 corridas completas sin fallos ni bloqueos. Forma recomendada de correr los tests en el README
- [ ] Testcontainers 4.1: la espera de readiness de Postgres no se puede reemplazar ni acotar (`WithWaitStrategy` suma a la de por defecto). Revisar al actualizar el paquete (hay 4.16) si ya acepta timeout/token y si se puede quitar el semáforo de `ContainerStartup`
- [ ] Una corrida aislada de Merchants falló una vez con `RegexMatchTimeoutException` en la inicialización estática de Testcontainers (`ResourceReaper`/`TestcontainersSettings.OS`) mientras compilaba en paralelo (CPU saturada). No se repitió; si reaparece, mirar la versión de Testcontainers

## Hallazgos del flujo E2E (sin tocar lógica de negocio)
- [x] `frontends/COURIER_API_MAP.md` actualizado con `POST /api/v1/couriers/me/current-assignment/delivered` y el snapshot de `current-assignment`; `MERCHANT_API_MAP.md` corregido; `customer/API_MAP.md` revisado sin discrepancias
- [x] Eliminado el seam temporal de entrega de Orders (no comprobaba ownership). `tools/smoke-tracking-e2e.ps1` y `tools/seed-smoke.ps1` usan ahora `POST /api/v1/couriers/me/current-assignment/delivered` (no ejecutados: no hay `pwsh` en esta máquina)
- [ ] **Decisión de producto**: la saga no tiene estados intermedios de preparación ni de recogida (Preparing/PickedUp/InTransit); Tracking salta de `CourierAssigned` a `Delivered` y ni el comercio ni el courier pueden marcarlos. Decidir si se modelan (follow-up de ADR-0008: requiere nuevos eventos, estados de saga y endpoints)

## Verificaciones manuales mías
- [ ] Aceptar la licencia de Xcode: `sudo xcodebuild -license` (mientras tanto git funciona con `DEVELOPER_DIR=/Library/Developer/CommandLineTools`)
- [ ] Revisar visualmente en Aspire (http://localhost:18888) la traza de un pedido vía gateway y la métrica `orders.placed` (recurso `orders`)
- [ ] Comprobar en Aspire que `orders.completed` sube tras `tools/e2e-order-flow.sh` (el contador se incrementa en la transición a `Completed`; Aspire no tiene API de métricas)
- [ ] Borrar los tags `backup/*` cuando confirme que main está bien
- [ ] Ejecutar `tools/smoke-tracking-e2e.ps1` con PowerShell (`brew install powershell`): migrado al endpoint de entrega del courier sin poder probarlo (no hay `pwsh` aquí)
- [ ] Probar `tools/seed-smoke.sh` desde base vacía la próxima vez que reinicie el entorno desde cero (requiere `docker compose down -v`, lo decido yo)
- [ ] Revisar y limpiar la zona de cobertura duplicada de "Tienda Smoke" y los pedidos de prueba
- [ ] Configurar claves de terceros (Google Maps, Stripe, SendGrid, Google OAuth) y restringir la key de Maps por HTTP referrer
- [ ] Rotar credenciales de RabbitMQ/MinIO si se expone fuera de local
