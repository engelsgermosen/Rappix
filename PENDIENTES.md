# Pendientes Rappix

Actualizado: 2026-10-28

## Entorno local
- [x] Subir memoria de Docker Desktop a 12 GB (evita el corte de dockerd en builds completos)
- [x] Guardar el seed bash como `tools/seed-smoke.sh` (idempotente; el `.ps1` original queda intacto)
- [x] Confirmar que `docker-compose.override.yml` esté en `.git/info/exclude` y no aparezca en `git status`
- [ ] Gateway en `:5100` por conflicto con AirPlay (macOS). Para volver a `:5000`: desactivar Receptor AirPlay, borrar el override y reconstruir los 3 frontends (`NEXT_PUBLIC_API_URL` es build-time)
- [ ] Cambiar credenciales de RabbitMQ y MinIO si algún día se expone fuera de la máquina local

## Claves de terceros (desactivadas por ahora)
- [ ] Google Maps (courier-web): `COURIER_GOOGLE_MAPS_API_KEY` y `COURIER_GOOGLE_MAPS_MAP_ID` en `.env`, luego `docker compose up -d --build courier-web`. Restringir la key por HTTP referrer
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
- [ ] Unificar `X-Correlation-Id` con `Activity.Current.TraceId` en `CorrelationIdMiddleware`
- [ ] Quartz: `OpenTelemetry.Instrumentation.Quartz` si los jobs importan
- [ ] Producción: sampler `ParentBased(TraceIdRatioBased)` y OpenTelemetry Collector delante del backend

## Repositorio
- [x] Ordenar ramas: PRs a main con merge commit, borrado de ramas mergeadas y tags `backup/*` de respaldo

## Tests
- [x] `Catalog.MerchantEventConsumerTests.MerchantSuspended_DisablesCatalog` intermitente con las suites en paralelo: esperaba con `Harness.Consumed.Any` y leía la base al instante. `Consumed` también cuenta los consumes con fault y su espera depende de la inactividad del harness. Ahora sondea el catálogo hasta el estado esperado (timeout 30 s) y, si vence, falla con el último estado y los faults
- [ ] Mismo patrón frágil (`Consumed.Any` + lectura inmediata de la base), sin tocar:
  - Catalog: `MerchantEventConsumerTests.MerchantApproved_CreatesEnabledCatalog`
  - Tracking: `CourierAssignedConsumerTests`, `CourierLocationUpdatedConsumerTests`, `OrderAcceptedConsumerTests`, `OrderSubmittedConsumerTests`, `OrderTerminalEventsConsumerTests`
  - Notifications: `CourierAssignedConsumerTests`, `MerchantLifecycleConsumerTests`, `OrderAcceptedConsumerTests`, `OrderSubmittedConsumerTests`, `OrderTerminalEventsConsumerTests`, `UserRegisteredConsumerTests`
- [ ] `Task.Delay(500)` fijo como espera, sin tocar: `Tracking.OrderSubmittedConsumerTests` (segundo publish) y `Notifications.DoubleDeliveryIdempotencyTests`
- [ ] Bucles de espera que **vuelven en silencio** al agotar ~5-6 s (cuentan `Consumed`, incluidos los faults), sin tocar: `Catalog.MerchantEventConsumerTests.WaitForConsumedAsync`, `Dispatch.UserRegisteredConsumerTests`, `Dispatch.CourierRequestedConsumerTests`, `Merchants.UserRegisteredConsumerTests`, `Payments.PaymentRequestedConsumerTests`, `Payments.OrderDeliveredCaptureConsumerTests`, `Payments.OrderTerminalCompensationConsumerTests`. Referencia correcta: `Dispatch.OrderTerminalEventsConsumerTests` lanza `TimeoutException`
- [ ] `Rappix.Merchants.Tests` se colgó una vez (>15 min sin avanzar, CPU ~0) en la 3.ª corrida completa en paralelo al reproducir el test de Catalog; no se repitió en 5 corridas posteriores con `--blame-hang-timeout 8m`. Sin diagnóstico: si reaparece, correr con `--blame-hang-timeout 5m --blame-hang-dump-type mini` para identificar el test

## Hallazgos del flujo E2E (sin tocar lógica de negocio)
- [ ] `frontends/COURIER_API_MAP.md` está desactualizado: dice que el courier no puede marcar la entrega, pero existe `POST /api/v1/couriers/me/current-assignment/delivered` (es lo que usa `tools/e2e-order-flow.sh`). Revisar también los otros API maps
- [ ] El seam temporal `POST /api/v1/orders/{id}/mark-delivered` sigue expuesto y **no comprueba ownership** (cualquier usuario autenticado puede completar cualquier pedido). Ya no hace falta para llegar a `Completed`: decidir si se elimina
- [ ] La saga no tiene estados intermedios de preparación ni de recogida (Preparing/PickedUp/InTransit); Tracking salta de `CourierAssigned` a `Delivered`. Follow-up de ADR-0008

## Verificaciones manuales mías
- [ ] Aceptar la licencia de Xcode: `sudo xcodebuild -license` (mientras tanto git funciona con `DEVELOPER_DIR=/Library/Developer/CommandLineTools`)
- [ ] Revisar visualmente en Aspire (http://localhost:18888) la traza de un pedido vía gateway y la métrica `orders.placed` (recurso `orders`)
- [ ] Comprobar en Aspire que `orders.completed` sube tras `tools/e2e-order-flow.sh` (el contador se incrementa en la transición a `Completed`; Aspire no tiene API de métricas)
- [ ] Borrar los tags `backup/*` cuando confirme que main está bien
- [ ] Probar `tools/seed-smoke.sh` desde base vacía la próxima vez que reinicie el entorno desde cero (requiere `docker compose down -v`, lo decido yo)
- [ ] Revisar y limpiar la zona de cobertura duplicada de "Tienda Smoke" y los pedidos de prueba
- [ ] Configurar claves de terceros (Google Maps, Stripe, SendGrid, Google OAuth) y restringir la key de Maps por HTTP referrer
- [ ] Rotar credenciales de RabbitMQ/MinIO si se expone fuera de local
