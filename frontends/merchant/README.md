# Rappix — Portal Comercio (Merchant)

Portal web de gestión para los comercios de Rappix. Construido con Next.js 15
(App Router), reutilizando toda la fontanería del portal Cliente
(`frontends/customer`) y consumiendo el mismo API Gateway YARP.

Es **una aplicación independiente** del portal Cliente — vive en su propio
directorio, se despliega por separado y tiene su propio `package.json`. Comparten
backend, no código.

## Stack

| Pieza | Versión |
|---|---|
| Next.js | 15.1.3 (App Router) |
| React | 19.0.0 |
| TypeScript | 5.7.2 strict |
| Tailwind CSS | 3.4.17 |
| TanStack Query | 5.62.7 |
| Zustand | 5.0.2 |
| Radix UI primitives | 1.x / 2.x |
| Leaflet + react-leaflet | 1.9.4 / 5.0.0 |
| Sonner | 1.7.1 (toasts) |
| lucide-react | 0.469.0 (icons) |

No usa SignalR (el backend no expone hub para comercios — gap documentado en
[FINDINGS.md](./FINDINGS.md) §9).

## Cómo correrlo

### 1. Backend + infra

Desde la raíz del repo (`rappix/`):

```powershell
docker compose up -d
```

Espera a que los 9 microservicios + Postgres + Redis + RabbitMQ + MinIO + Seq
estén verdes (`docker compose ps`).

### 2. Seed de datos (recomendado)

Para probar el flujo end-to-end, corre el seed que deja un comercio **Active**:

```powershell
./tools/seed-smoke.ps1
```

El seed crea estas cuentas (contraseña común: `Sup3rSecret!`):

| Email | Rol | Estado tras seed |
|---|---|---|
| `comercio@rappix.test` | Merchant | **Active** (listo para recibir pedidos) |
| `cliente@rappix.test` | Customer | Email confirmado, listo para pedir |
| `admin@rappix.test` | Admin | Para acciones administrativas via curl |
| `courier@rappix.test` | Courier | Para el portal de repartidores |

### 3. Portal Comercio (este proyecto)

```powershell
cd frontends/merchant
pnpm install        # primera vez
Copy-Item .env.local.example .env.local
pnpm dev            # http://localhost:3001
```

Las variables de entorno mínimas:

```
NEXT_PUBLIC_API_URL=http://localhost:5000   # gateway YARP
NEXT_PUBLIC_DEFAULT_LAT=18.4719             # mapa por defecto = Santo Domingo
NEXT_PUBLIC_DEFAULT_LNG=-69.9000
```

### 4. Portal Cliente (opcional, para probar pedidos end-to-end)

En otra terminal:

```powershell
cd frontends/customer
pnpm dev            # http://localhost:3000
```

## Flujo end-to-end de prueba

1. Abre `http://localhost:3001/login` y entra como **`comercio@rappix.test` / `Sup3rSecret!`**. Como el comercio del seed está `Active`, el portal redirige a `/console/pedidos` (vacío).
2. En otra pestaña abre `http://localhost:3000`, regístrate o entra con `cliente@rappix.test`, busca el comercio "Tienda Smoke", arma carrito y coloca un pedido.
3. Vuelve al portal Comercio: el pedido aparece en **≤ 10 segundos** (polling de `/orders/merchant/pending`).
4. Click **Aceptar pedido** → optimistic update lo quita; la saga procesa pago + asigna courier + entrega solo.
5. Click **Rechazar** abre un Dialog para escribir la razón opcional; al confirmar, la saga compensa (libera stock, anula quote) y cancela.

## Estructura de carpetas

```
src/
├── app/
│   ├── layout.tsx                     Providers + Geist fonts
│   ├── globals.css                    Tailwind base + Leaflet z-index
│   ├── page.tsx                       Router por merchant.status
│   ├── login/page.tsx                 Split layout (hero + form)
│   ├── register/page.tsx              accountType=Merchant always
│   ├── onboarding/                    Wizard de 5 pasos (Draft only)
│   │   ├── layout.tsx
│   │   ├── datos-basicos/page.tsx
│   │   ├── horarios/page.tsx
│   │   ├── zona/page.tsx              Mapa Leaflet (Circle MVP)
│   │   ├── ubicacion/page.tsx         Mapa Leaflet (pickup pin)
│   │   └── logo/page.tsx
│   ├── en-revision/page.tsx           Pending (polling 15s)
│   ├── suspendido/page.tsx            Suspended | Rejected
│   └── console/                       Active | Paused only
│       ├── layout.tsx                 Sidebar + RequireMerchantStatus
│       ├── pedidos/page.tsx           Polling 10s + accept/reject optimistic
│       ├── catalogo/page.tsx          Categorías + items + stock + foto
│       ├── configuracion/page.tsx     5 cards editables
│       └── cuenta/page.tsx            Perfil + contraseña + logout
├── components/
│   ├── auth/                          RequireAuth, RequireMerchantStatus, WrongPortalBanner, AuthShell
│   ├── catalogo/                      CategoriesRail, ItemsTable, NewItemDialog, StockDialog, ItemPhoto
│   ├── configuracion/                 DatosCard, HorariosCard, ZonaCard, PickupCard, LogoCard
│   ├── layout/                        ConsoleSidebar, WizardSidebar, RappixLogo
│   ├── maps/                          ServiceAreaMap + loader, PickupMap + loader
│   ├── onboarding/                    StepShell + completedStepsFor()
│   ├── pedidos/                       OrderCard, PedidosEmptyState, RefreshBanner
│   ├── providers/                     QueryProvider, AuthProvider, ToastProvider
│   └── ui/                            16 shadcn primitives (button, card, dialog, etc.)
└── lib/
    ├── env.ts                         Type-safe env access
    ├── utils.ts                       cn, formatMoney, shortId, relativeTime, DAY_LABEL, formatDistanceKm
    ├── api/
    │   ├── client.ts                  Fetch wrapper con JWT + refresh + idempotency
    │   ├── errors.ts                  ApiError class
    │   ├── types.ts                   DTOs del backend
    │   ├── identity.ts                /auth/me, /users/me, change-password
    │   ├── merchants-me.ts            Endpoints del merchant owner
    │   ├── merchants-public.ts        /merchants/{id}/logo-url
    │   ├── catalog-me.ts              CRUD del catálogo
    │   ├── catalog-public.ts          /items/{id}/photo-url
    │   └── merchant-orders.ts         pending list + accept + reject
    ├── auth/
    │   ├── token-store.ts             localStorage rappix.merchant.auth
    │   └── store.ts                   Zustand store (isMerchant guard)
    ├── errors/index.ts                describeError(err): mensaje es-DO
    └── stock-cache/store.ts           Memoria de último stock leído por item
```

## Decisiones de diseño

- **No paquete compartido con el portal Cliente.** Los dos portales son apps
  independientes que casualmente comparten patrones. Copiar es más barato que
  acoplar.
- **Estado del comercio manda la ruta.** El root `/` no tiene UI propia: lee
  `/merchants/me` y redirige según `status`. Esto evita que un Draft vea
  pantallas de catálogo o que un Pending vea el wizard.
- **Polling en vez de SignalR.** El backend no expone hub para comercios; el
  portal poll-ea cada 10 s con TanStack Query.
- **Mapas con Leaflet + OpenStreetMap.** Sin Mapbox ni Google. Los dos
  componentes de mapa (`service-area-map.tsx`, `pickup-map.tsx`) cargan via
  `next/dynamic({ ssr: false })` para que Leaflet no toque `window` durante SSR.
- **Forms con `useState` plano.** No react-hook-form, no zod. Cada form tiene
  ≤6 fields; el wrapper de Server-side error de TanStack ya da los mensajes via
  `describeError(err)`.
- **Fidelidad al backend sobre fidelidad al diseño.** Cuando el diseño mostraba
  controles sin endpoint (pausar pedidos, KPIs diarios, etc.) la UI se eliminó
  y se documentó en [FINDINGS.md](./FINDINGS.md). Sin mocks.

## Scripts

| Comando | Qué hace |
|---|---|
| `pnpm dev` | Next.js dev en `:3001` |
| `pnpm build` | Production build (Static rendering de las 14 rutas) |
| `pnpm start` | Servidor de producción en `:3001` |
| `pnpm typecheck` | `tsc --noEmit` |
| `pnpm lint` | `next lint` |

## Convenciones

- Idioma: **español es-DO** en toda la UI. Variables de código en inglés.
- Moneda: **RD$** (DOP) con `formatMoney()`. Cero decimales si el monto es entero.
- Commits: `feat(frontend): <descripción imperativa>` por fase.
- Fechas: `Intl.DateTimeFormat("es-DO")`, sin date-fns.
