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
- [ ] Healthchecks de los 3 frontends (Next.js no expone `/health`; habría que añadir una ruta o sondear `/`)
- [ ] Actualizar Next.js (15.1.3 tiene avisos de seguridad); correr `pnpm audit` en los 3 frontends, en un cambio aparte

## Observabilidad (piloto en Orders)
- [ ] Validar el flujo completo: pedido desde `localhost:3000` y revisar Trazas, Métricas (`orders.placed`) y Logs en `localhost:18888`
- [x] Filtrar el span de telemetría sobre telemetría (`opentelemetry.proto.collector.logs.v1.LogsService/Export`)
- [x] Instrumentar el gateway: `builder.AddRappixObservability("gateway")`, para que la traza empiece en la entrada
- [x] Instrumentar Pricing (Orders lo llama por gRPC) y luego Catalog y Merchants
- [ ] Unificar `X-Correlation-Id` con `Activity.Current.TraceId` en `CorrelationIdMiddleware`
- [ ] Quartz: `OpenTelemetry.Instrumentation.Quartz` si los jobs importan
- [ ] Producción: sampler `ParentBased(TraceIdRatioBased)` y OpenTelemetry Collector delante del backend

## Repositorio
- [ ] Ordenar ramas: commits separados (OTel/Orders y frontends/Docker), PRs a main, merge y borrado de ramas, con tags de respaldo

## Verificaciones manuales mías
- [ ] Aceptar la licencia de Xcode: `sudo xcodebuild -license` (mientras tanto git funciona con `DEVELOPER_DIR=/Library/Developer/CommandLineTools`)
- [ ] Revisar visualmente en Aspire (http://localhost:18888) la traza de un pedido vía gateway y la métrica `orders.placed` (recurso `orders`)
- [ ] Borrar los tags `backup/*` cuando confirme que main está bien
- [ ] Probar `tools/seed-smoke.sh` desde base vacía la próxima vez que reinicie el entorno desde cero (requiere `docker compose down -v`, lo decido yo)
- [ ] Revisar y limpiar la zona de cobertura duplicada de "Tienda Smoke" y los pedidos de prueba
- [ ] Configurar claves de terceros (Google Maps, Stripe, SendGrid, Google OAuth) y restringir la key de Maps por HTTP referrer
- [ ] Rotar credenciales de RabbitMQ/MinIO si se expone fuera de local
