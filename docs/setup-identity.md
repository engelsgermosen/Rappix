# Configuración del Identity Service (Fase 1)

Guía paso a paso para configurar los secretos del servicio Identity: **JWT**, **SendGrid** (email
de confirmación) y **Google OAuth**. Todos los secretos viven fuera de git (en
`appsettings.Development.json` para `dotnet run`, o en `.env` para `docker compose`).

> **Importante:** nunca commitees claves reales. `appsettings.Development.json`, `appsettings.Local.json`
> y `.env` ya están en `.gitignore`. La plantilla de variables es [.env.example](../.env.example).

---

## 1. Clave de firma JWT

El access token se firma con HS256 usando una clave simétrica de **256 bits o más** (32+ caracteres).

Genera una clave aleatoria:

```powershell
# PowerShell
[Convert]::ToBase64String((1..48 | ForEach-Object { Get-Random -Maximum 256 }))
```

```bash
# bash / Git Bash
openssl rand -base64 48
```

Guarda el valor en `IDENTITY_JWT_SIGNINGKEY` (para docker) o en `Jwt:SigningKey` (para `dotnet run`).

---

## 2. SendGrid (email de confirmación)

1. Crea una cuenta gratuita en <https://signup.sendgrid.com/>.
2. **Single Sender Verification** (remitente verificado): ve a
   <https://app.sendgrid.com/settings/sender_auth/senders> → **Create New Sender**. Usa un email
   real tuyo (p. ej. `tu-correo@gmail.com`) y confírmalo desde tu bandeja. Ese será tu `FromEmail`.
3. **API key**: ve a <https://app.sendgrid.com/settings/api_keys> → **Create API Key** → permiso
   *Restricted Access* con **Mail Send: Full Access** → copia la clave (empieza con `SG.`). Solo se
   muestra una vez.
4. Configura:
   - `SENDGRID_API_KEY` = la clave `SG.xxxxx`
   - `SENDGRID_FROM_EMAIL` = el remitente verificado en el paso 2
   - `SENDGRID_FROM_NAME` = `Rappix` (o lo que prefieras)

> Sin `SENDGRID_API_KEY`, el registro **sigue funcionando**: solo se omite el envío del email y se
> registra una advertencia. Puedes confirmar manualmente con el endpoint de confirmación.

---

## 3. Google OAuth

1. Entra a <https://console.cloud.google.com/> y crea (o elige) un proyecto.
2. **OAuth consent screen** (<https://console.cloud.google.com/apis/credentials/consent>):
   tipo *External*, completa nombre de la app y el correo de soporte, y en *Test users* agrega tu
   email de Google para poder probar.
3. **Credenciales** (<https://console.cloud.google.com/apis/credentials>): **Create Credentials →
   OAuth client ID → Web application**.
4. En **Authorized redirect URIs** agrega la URI **exacta** (debe coincidir con `Google:CallbackPath`,
   que por defecto es `/signin-google`):
   - `https://localhost:5001/signin-google` — al correr con `dotnet run` (HTTPS).
   - `http://localhost:5001/signin-google` — al correr con `docker compose` (HTTP). Google permite
     `http` solo para `localhost`.
5. Copia el **Client ID** y el **Client secret** y configúralos:
   - `GOOGLE_CLIENT_ID` = `xxxxx.apps.googleusercontent.com`
   - `GOOGLE_CLIENT_SECRET` = `GOCSPX-xxxxx`

> Si `Google:ClientId`/`ClientSecret` están vacíos, el esquema de Google no se registra y los
> endpoints `/auth/google*` no estarán disponibles (el resto del servicio funciona igual).

---

## 4. Cómo aplicar la configuración

### Opción A — `dotnet run` (local, sin contenedor)

Edita `src/Services/Identity/Rappix.Identity.Api/appsettings.Development.json` (gitignored):

```json
{
  "Jwt": { "SigningKey": "TU_CLAVE_GENERADA" },
  "SendGrid": { "ApiKey": "SG.xxxxx", "FromEmail": "tu-correo@gmail.com" },
  "Google": { "ClientId": "xxxxx.apps.googleusercontent.com", "ClientSecret": "GOCSPX-xxxxx" }
}
```

O por variables de entorno (PowerShell), antes de `dotnet run`:

```powershell
$env:Jwt__SigningKey = "TU_CLAVE_GENERADA"
$env:SendGrid__ApiKey = "SG.xxxxx"
$env:SendGrid__FromEmail = "tu-correo@gmail.com"
$env:Google__ClientId = "xxxxx.apps.googleusercontent.com"
$env:Google__ClientSecret = "GOCSPX-xxxxx"
```

### Opción B — `docker compose`

Copia la plantilla y completa los valores:

```powershell
copy .env.example .env   # PowerShell
```

Edita `.env` con tu clave JWT y, opcionalmente, SendGrid/Google. `docker compose` inyecta esas
variables en el contenedor `identity-api`.

---

## 5. Arrancar el servicio

```powershell
# 1. Infraestructura
docker compose up -d postgres redis rabbitmq seq

# 2. Migración (crea el esquema 'identity')
dotnet ef database update --project src/Services/Identity/Rappix.Identity.Infrastructure --startup-project src/Services/Identity/Rappix.Identity.Api

# 3a. Correr local
dotnet run --project src/Services/Identity/Rappix.Identity.Api
#     -> https://localhost:5001/swagger

# 3b. O correr en contenedor (requiere .env con IDENTITY_JWT_SIGNINGKEY)
docker compose up -d --build identity-api
#     -> http://localhost:5001/swagger
```

Dashboards útiles: Swagger UI (`/swagger`), Seq (<http://localhost:5341>),
RabbitMQ (<http://localhost:15672>, usuario `rappix`).
