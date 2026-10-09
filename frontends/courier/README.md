# Rappix Courier — portal del repartidor

Portal web del repartidor de Rappix. Es una app Next.js 15 **independiente**
de `frontends/customer/` (puerto 3000) y `frontends/merchant/` (puerto 3001) —
copia los patrones, no comparte paquete. Tres apps separadas, tres puertos.

Construido en Fase 14 contra el backend Fase 13.6 (ver
[`frontends/COURIER_API_MAP.md`](../COURIER_API_MAP.md) — el mapa de API
es la fuente de verdad).

## Diseño

Pensado para usarse **en la calle**, en celular al sol, con o sin guantes.
Móvil-first pero responsive: en desktop el contenido se centra (no se estira
una columna de 390px) y la pantalla de pedido activo se abre a 2 columnas
(mapa grande izquierda + panel info derecha).

| Color | Hex | Uso |
|---|---|---|
| Primario (brand) | `#534AB7` | Headers, marcadores de comercio en el mapa, status Online dot |
| Acento (accent) | `#D85A30` | Marcadores de cliente en el mapa, links de acción |
| Verde (success) | `#1D9E75` | Botón "Conectarme" y "Entregué al cliente" |
| Rojo (reject) | `#A32D2D` | Botón "Desconectarme" |

**Tipografía**: Geist Sans (Google Fonts). **Idioma**: español RD, sentence
case (capitalizada solo la primera letra), `RD$` para dinero.

## Stack

| Capa | Versión / paquete |
|---|---|
| Framework | Next.js 15.1.3 + React 19 (App Router) |
| Lenguaje | TypeScript 5.7 strict |
| Estilos | Tailwind 3.4 + brand palette |
| UI primitives | shadcn/ui-style (Radix UI vendoreado en `src/components/ui/`) |
| Estado servidor | TanStack Query 5.62 |
| Estado cliente | Zustand 5 (auth) |
| Mapas | `@vis.gl/react-google-maps` 1.5 (Google Maps oficial) |
| Toasts | Sonner 1.7 |
| Iconos | lucide-react 0.469 |

**NO trae** `@microsoft/signalr` ni `leaflet` — el courier emite ubicación por
REST (`POST /api/v1/couriers/me/location`), el hub SignalR es exclusivo del
cliente (`CustomerUserId`). Confirmado en [COURIER_API_MAP §E](../COURIER_API_MAP.md#e-location-flow--rest-not-signalr-architecture-a).

## Correr en desarrollo

```powershell
# 1. Backend + infra (desde la raíz del repo)
docker compose up -d
./tools/seed-smoke.ps1   # crea courier@rappix.test (entre otros)

# 2. Portal Courier
cd frontends/courier
pnpm install
Copy-Item .env.local.example .env.local
# Edita .env.local: NEXT_PUBLIC_GOOGLE_MAPS_API_KEY es OPCIONAL — sin ella
# el mapa se sustituye por un placeholder con un link "Abrir en Google Maps".
pnpm dev                  # http://localhost:3002
```

Para que las acciones de **ubicación** funcionen el navegador pide permiso de
geolocalización la primera vez que vas Online. Si lo deniegas, el portal
sigue funcionando pero el cliente no podrá verte moviéndote en el mapa.

## Variables de entorno

| Nombre | Default | Requerida |
|---|---|---|
| `NEXT_PUBLIC_API_URL` | `http://localhost:5000` | **sí** (gateway YARP) |
| `NEXT_PUBLIC_GOOGLE_MAPS_API_KEY` | (vacío) | no — sin ella el mapa muestra placeholder, el flujo entero sigue |
| `NEXT_PUBLIC_GOOGLE_MAPS_MAP_ID` | (vacío) | no — Map ID estilizado de Google Cloud Console; default de Google si vacío |
| `NEXT_PUBLIC_DEFAULT_LAT` | `18.4719` | no — Santo Domingo |
| `NEXT_PUBLIC_DEFAULT_LNG` | `-69.9000` | no |

**Cómo obtener una API key**: https://console.cloud.google.com/google/maps-apis
→ habilita **Maps JavaScript API** → crea credencial → **restringe** a
`http://localhost:3002/*` en dev (luego al origen real en prod). Para el
Map ID estilizado: https://console.cloud.google.com/google/maps-apis/studio/maps
→ usa un preset Light / Minimal con baja saturación.

## Pantallas

| Ruta | Qué hace |
|---|---|
| `/login` | Email + contraseña. Rechaza con banner persistente si el JWT no es `userType=Courier`. |
| `/register` | Crea cuenta con `accountType=Courier` + auto-login + redirige a `/`. |
| `/` (autenticado) | **Status-aware**: Offline-sin-vehículo → CTA "Configurar vehículo"; Offline-con-vehículo → toggle verde "Conectarme"; Online idle → toggle rojo "Desconectarme" + tarjeta "Esperando pedido"; Busy → pantalla de pedido activo (mapa + panel). |
| `/vehiculo` | 3 tarjetas seleccionables Moto/Bici/Carro + placa opcional + capacidad kg opcional. |
| `/cuenta` | Perfil (nombre/apellido/teléfono), cambio de contraseña (cierra sesión tras éxito porque el backend revoca tokens), logout. |

## Endpoints que toca

7 endpoints courier-exclusivos en Dispatch (verificados en código de Fase 13.6):

```
GET    /api/v1/couriers/me                                  → CourierResponse
PUT    /api/v1/couriers/me/vehicle                          → CourierResponse
POST   /api/v1/couriers/me/online                           → CourierResponse
POST   /api/v1/couriers/me/offline                          → CourierResponse
POST   /api/v1/couriers/me/location                         → CourierResponse
GET    /api/v1/couriers/me/current-assignment               → 204 | 200 + snapshot
POST   /api/v1/couriers/me/current-assignment/delivered     → 204
```

Compartidos con los otros portales: `/auth/{register,login,refresh,logout,me}`,
`/users/me`, `/users/me/change-password`.

## Patrones reusados de customer/merchant

- `src/lib/api/{client,errors,types}.ts`: `apiFetch` con bearer + 401-refresh
  coalescente + `Idempotency-Key` automático en `POST/PUT/DELETE` + parseo
  de RFC 7807 ProblemDetails y switch por `err.code` en el caller.
- `src/lib/auth/{token-store,store}.ts`: Zustand + localStorage `rappix.courier.auth`
  (key distinta de customer/merchant para coexistir en mismo dominio) +
  `isCourier` guard + `WrongPortalError`.
- `src/lib/errors/index.ts`: `describeError(err)` con `CODE_MESSAGES` para
  cada código de Identity + Dispatch courier (`VehicleRequired`,
  `BusyCannotGoOffline`, `NoActiveAssignment`, etc.).

## Scripts

| Comando | Hace |
|---|---|
| `pnpm dev` | Dev server en `:3002` |
| `pnpm build` | Build de producción (Next.js static prerender) |
| `pnpm start` | Sirve el build en `:3002` |
| `pnpm lint` | `next lint` |
| `pnpm typecheck` | `tsc --noEmit` |

## Hallazgos del backend

Ver [FINDINGS.md](./FINDINGS.md) — qué cosas del diseño quedan a medias por
gaps del backend, y qué se trabajó alrededor.
