# Configuración del Merchants Service (Fase 2)

Guía para arrancar el Merchants Service y probar el flujo end-to-end: alta por **choreography**
(Identity → Merchants), completar el comercio, aprobación admin, búsqueda geoespacial, logo y gRPC.

> Merchants **valida** los JWT que emite Identity: usa la **misma** `Jwt:SigningKey`, `Issuer` y
> `Audience`. En docker reutiliza `IDENTITY_JWT_SIGNINGKEY` del `.env`. No emite tokens propios.

---

## 1. Requisitos e infraestructura

```powershell
docker compose up -d postgres redis rabbitmq seq minio
```

- **PostgreSQL+PostGIS** (`postgis/postgis:17-3.5`): el `postgres-init.sql` crea `rappix_merchants` y
  habilita la extensión `postgis`.
- **MinIO** (`localhost:9000` API, `localhost:9001` consola, `rappix`/`rappix_minio_2026`): el bucket
  `merchants-logos` lo crea automáticamente un `IHostedService` al arrancar el servicio.

---

## 2. Migración (esquema `merchants`)

```powershell
dotnet ef database update `
  --project src/Services/Merchants/Rappix.Merchants.Infrastructure `
  --startup-project src/Services/Merchants/Rappix.Merchants.Api
```

Crea las tablas `merchants`, `service_areas` (con columnas `geometry`/`geography`, índices GiST y el
CHECK de forma), `operating_hours` y las tablas inbox/outbox de MassTransit.

---

## 3. Arrancar el servicio

```powershell
# Local (dos puertos cleartext: 8080 REST, 8081 gRPC). Define la clave JWT igual que Identity:
$env:Jwt__SigningKey = "TU_CLAVE_COMPARTIDA_CON_IDENTITY"
dotnet run --project src/Services/Merchants/Rappix.Merchants.Api
#   REST  -> http://localhost:8080/scalar/v1
#   gRPC  -> http://localhost:8081

# O en contenedor (REST 5002, gRPC 5012; requiere .env con IDENTITY_JWT_SIGNINGKEY):
docker compose up -d --build identity-api merchants-api
#   REST  -> http://localhost:5002/scalar/v1
#   gRPC  -> localhost:5012
```

---

## 4. Crear un admin (para el workflow de aprobación)

Admin no es self-service. Registra un usuario normal y promuévelo en la BD de Identity, luego
**vuelve a iniciar sesión** para obtener un JWT con `userType=Admin`:

```sql
-- Conéctate a rappix_identity
UPDATE identity.users SET "UserType" = 'Admin' WHERE "Email" = 'admin@rappix.test';
```

```powershell
docker exec -it rappix-postgres psql -U rappix -d rappix_identity `
  -c "UPDATE identity.users SET \"UserType\" = 'Admin' WHERE \"Email\" = 'admin@rappix.test';"
```

---

## 5. Smoke test del flujo choreography (puertos de docker: 5001 / 5002)

```bash
# 1) Registrarse como Merchant en Identity (dispara UserRegisteredIntegrationEvent)
curl -sX POST http://localhost:5001/api/v1/auth/register \
  -H 'Content-Type: application/json' \
  -d '{"email":"meri@rappix.test","password":"Merch4ntPass!","firstName":"Meri","lastName":"Comercio","accountType":"Merchant"}'

# 2) (opcional) Confirmar el email vía el enlace que loguea SendGrid; submit-for-approval lo exige.

# 3) Login -> copia accessToken
curl -sX POST http://localhost:5001/api/v1/auth/login \
  -H 'Content-Type: application/json' \
  -d '{"identifier":"meri@rappix.test","password":"Merch4ntPass!"}'
TOKEN=...   # accessToken del paso anterior

# 4) El consumer ya creó el Draft -> verlo
curl -s http://localhost:5002/api/v1/merchants/me -H "Authorization: Bearer $TOKEN"

# 5) Completar perfil + zona (círculo) + horarios
curl -sX PUT http://localhost:5002/api/v1/merchants/me -H "Authorization: Bearer $TOKEN" \
  -H 'Content-Type: application/json' \
  -d '{"name":"Tienda Lulu","slug":"tienda-lulu","rnc":"131246803","description":"Mi tienda","verticalType":"Food"}'

curl -sX POST http://localhost:5002/api/v1/merchants/me/service-areas -H "Authorization: Bearer $TOKEN" \
  -H 'Content-Type: application/json' \
  -d '{"type":"Circle","centerLatitude":18.4861,"centerLongitude":-69.9312,"radiusMeters":3000}'

curl -sX PUT http://localhost:5002/api/v1/merchants/me/operating-hours -H "Authorization: Bearer $TOKEN" \
  -H 'Content-Type: application/json' \
  -d '{"hours":[{"dayOfWeek":"Monday","opensAt":"08:00","closesAt":"20:00"}]}'

# 6) Enviar a aprobación (Draft -> Pending)
curl -sX POST http://localhost:5002/api/v1/merchants/me/submit-for-approval -H "Authorization: Bearer $TOKEN"

# 7) Como admin (JWT con userType=Admin): listar pendientes y aprobar
ADMIN=...   # accessToken de un usuario con userType=Admin
curl -s "http://localhost:5002/api/v1/admin/merchants?status=Pending" -H "Authorization: Bearer $ADMIN"
curl -sX POST http://localhost:5002/api/v1/admin/merchants/<MERCHANT_ID>/approve -H "Authorization: Bearer $ADMIN"

# 8) Sin auth: búsqueda geoespacial (debe aparecer si el punto cae en su zona)
curl -s "http://localhost:5002/api/v1/merchants/nearby?lat=18.4861&lng=-69.9312"

# 9) (opcional) Subir logo (multipart, máx 2 MB, PNG/JPEG/WebP)
curl -sX POST http://localhost:5002/api/v1/merchants/me/logo -H "Authorization: Bearer $TOKEN" \
  -F 'file=@logo.png;type=image/png'
```

Verifica en **Seq** (<http://localhost:5341>) los logs de ambos servicios y en **RabbitMQ**
(<http://localhost:15672>, `rappix`) el exchange `Rappix.Contracts.Identity:UserRegisteredIntegrationEvent`
consumido por la cola `merchants-user-registered`.

---

## 6. Probar el gRPC interno con grpcurl

El servicio gRPC `merchants.v1.MerchantValidationService` escucha en `localhost:5012` (h2c). Como no
hay reflection habilitada, se pasa el `.proto` explícitamente:

```bash
grpcurl -plaintext \
  -import-path src/Services/Merchants/Rappix.Merchants.Api/Protos -proto merchants.proto \
  -d '{"merchant_id":"<MERCHANT_ID>"}' \
  localhost:5012 merchants.v1.MerchantValidationService/IsMerchantActive
# -> {"isActive": true, "status": "Active"}

grpcurl -plaintext \
  -import-path src/Services/Merchants/Rappix.Merchants.Api/Protos -proto merchants.proto \
  -d '{"merchant_id":"<MERCHANT_ID>"}' \
  localhost:5012 merchants.v1.MerchantValidationService/GetMerchantBasicInfo
```

> Los clientes gRPC deben usar HTTP/2 *prior-knowledge* (h2c), ya que el puerto 8081 es cleartext.

---

## 7. Correr las pruebas

```powershell
dotnet test src/Services/Merchants/Rappix.Merchants.Tests
```

Requiere Docker (Testcontainers levanta PostgreSQL+PostGIS efímeros). Cubre el agregado y su máquina
de estados, el discriminador de zonas, el validador de logos (ImageSharp), el ciclo del owner por
HTTP, el workflow admin, la búsqueda geoespacial, el servicio gRPC y el consumer (harness in-memory).
