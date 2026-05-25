# Rappix Courier — portal del repartidor

Portal web del repartidor de Rappix. Es una app Next.js 15 independiente, hermana
de `frontends/customer/` (puerto 3000) y `frontends/merchant/` (puerto 3001).

> **Detalle por commit**: este README se completa en el commit 8 del plan
> (`docs(frontend): README + FINDINGS.md`). Por ahora describe lo mínimo para
> arrancar el dev server tras `pnpm install`.

## Stack

- Next.js 15 + React 19 + TypeScript estricto + App Router.
- Tailwind 3 + Radix UI primitives (vendoreados en `src/components/ui/`).
- TanStack Query v5 + Zustand 5 (auth).
- `@vis.gl/react-google-maps` (Google Maps oficial).
- Sonner (toasts), lucide-react (icons).

## Correr en desarrollo

```powershell
# 1. Backend + infra
docker compose up -d
./tools/seed-smoke.ps1   # crea courier@rappix.test entre otros

# 2. Portal Courier
cd frontends/courier
pnpm install
Copy-Item .env.local.example .env.local
# Edita .env.local para poner tu Google Maps API key (opcional; sin ella el
# mapa se sustituye por un placeholder).
pnpm dev                  # http://localhost:3002
```

## Variables de entorno

| Nombre | Default | Requerida |
|---|---|---|
| `NEXT_PUBLIC_API_URL` | `http://localhost:5000` | sí (gateway YARP) |
| `NEXT_PUBLIC_GOOGLE_MAPS_API_KEY` | (vacío) | no — sin ella el mapa muestra un placeholder, el resto funciona |
| `NEXT_PUBLIC_GOOGLE_MAPS_MAP_ID` | (vacío) | no — Map ID estilizado de Google Cloud; default de Google si vacío |
| `NEXT_PUBLIC_DEFAULT_LAT` | `18.4719` | no — centro inicial del mapa |
| `NEXT_PUBLIC_DEFAULT_LNG` | `-69.9000` | no |

## Scripts

| | |
|---|---|
| `pnpm dev` | dev server en :3002 |
| `pnpm build` | build de producción |
| `pnpm start` | servir build en :3002 |
| `pnpm lint` | next lint |
| `pnpm typecheck` | tsc --noEmit |
