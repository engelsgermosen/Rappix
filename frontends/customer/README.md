# Rappix Customer Portal

The customer-facing web app for **Rappix**, a multi-vertical delivery platform (food, pharmacy, supermarket, parcels) for the Dominican Republic. Built with Next.js 15 + TypeScript, consuming the 9-microservice .NET backend through a single API Gateway.

> Backend lives in `/src` (don't edit it from here). Frontend talks **only** to the gateway. See [`API_MAP.md`](./API_MAP.md) for the complete endpoint inventory the FE consumes.

---

## Stack

| Layer | Choice |
|---|---|
| Framework | Next.js 15 (App Router) + React 19 |
| Language  | TypeScript (strict) |
| Styling   | Tailwind CSS 3.4 + custom design tokens (brand `#534AB7`, accent `#D85A30`) |
| UI atoms  | Vendored shadcn-style primitives (Button, Card, Dialog, Tabs, Dropdown, …) on Radix UI |
| Data      | TanStack Query v5 (server cache) + Zustand (cart + checkout + auth) |
| Realtime  | `@microsoft/signalr` for the Tracking hub (JWT via `?access_token=`) |
| Maps      | Leaflet + react-leaflet (dynamic-imported, client-only) |
| Payments  | Stripe Elements is **NOT** wired — Phase 8 Payments service is event-driven, no `client_secret` endpoint exists. The UI ships a payment-method picker until a Phase 9+ endpoint lands. |
| Toasts    | Sonner |
| Icons     | lucide-react |

---

## Prerequisites

- Node.js ≥ 20 (tested on 22.12)
- npm ≥ 10
- The Rappix backend running locally: `docker compose up` from the repo root (`/` or wherever your `docker-compose.yml` lives). The gateway listens on `http://localhost:5000` by default.

You don't need any individual service URL — the frontend talks **only** to the gateway, which forwards to identity (5001), merchants (5002), catalog (5003), pricing (5004), orders (5005), dispatch (5006), tracking (5007), payments (5008), notifications (5009).

---

## Setup

```bash
cd frontends/customer
npm install
cp .env.local.example .env.local   # already pre-populated for local dev
npm run dev                        # http://localhost:3000
```

`npm run build` produces a clean production bundle (verified — 10 routes, ~105 kB shared JS).

### Environment

| Var | Default | Purpose |
|---|---|---|
| `NEXT_PUBLIC_API_URL` | `http://localhost:5000` | API Gateway base. |
| `NEXT_PUBLIC_STRIPE_PUBLISHABLE_KEY` | `pk_test_REPLACE_ME` | Placeholder. Not consumed today — Stripe Elements is a follow-up (see API_MAP.md). |
| `NEXT_PUBLIC_DEFAULT_LAT` / `_LNG` | `18.4719 / -69.9` | Santo Domingo. Used to call `GET /merchants/nearby` since the FE has no geolocation flow yet. |
| `NEXT_PUBLIC_DEFAULT_DISTANCE_KM` | `2.5` | Used as the `distanceKm` field in `POST /pricing/quotes`. Merchants doesn't expose pickup coords today; remove this constant once the backend ships per-merchant `lat/lng`. |

---

## Project structure

```
src/
  app/
    layout.tsx                  – Root shell (Geist fonts + providers + header/footer)
    page.tsx                    – /            Home (merchants nearby + vertical chips)
    login/page.tsx              – /login       Auth: identifier + password
    register/page.tsx           – /register    Auth: create customer
    comercios/[slug]/page.tsx   – /comercios/<slug>  Merchant detail + catalog + cart FAB
    stores/[id]/page.tsx        – /stores/<id> Alias → redirects to /comercios/<slug>
    carrito/page.tsx            – /carrito     Cart (Zustand-persisted)
    checkout/page.tsx           – /checkout    Address + payment + quote + place order
    pedidos/page.tsx            – /pedidos     My orders
    pedidos/[id]/page.tsx       – /pedidos/<id>  Live tracking (Leaflet + SignalR)
    cuenta/page.tsx             – /cuenta      Profile (Identity /me + /users/me)

  components/
    layout/                     – Header, footer, search bar, cart button, user menu
    providers/                  – React Query, auth hydration, toaster
    ui/                         – shadcn-style primitives (Button, Card, Dialog, …)
    merchants/                  – Cards, hero, vertical chips, promo banner
    catalog/                    – Item cards, sections, "Ver carrito" FAB
    checkout/                   – Address card, payment picker, tip picker, countdown
    orders/                     – Status badge
    tracking/                   – Map (dynamic), timeline, courier card
    auth/                       – Auth shell + RequireAuth guard

  lib/
    api/                        – One file per service: types, client, errors
    auth/                       – tokenStore (localStorage) + Zustand store with 401-refresh
    cart/                       – Cart store + ephemeral checkout store
    orders/                     – Status enum helpers (labels, terminal/cancellable)
    tracking/                   – SignalR connection lifecycle (Subscribe/Unsubscribe + auto-reconnect)
    env.ts                      – Type-safe env access (throws if API_URL missing)
    utils.ts                    – formatMoney (RD$), idempotency key, etc.
```

---

## Auth & session model

- **JWT** lives in `localStorage` (`rappix.customer.auth`), wrapped by [`tokenStore`](src/lib/auth/token-store.ts). The Zustand store is a UI mirror.
- **Access token** expires in 15 min. The fetch wrapper transparently calls `POST /auth/refresh` on the first 401, retries once, and on a hard failure clears the store. The 401 then propagates to the UI and `RequireAuth` redirects to `/login?redirect=…`.
- **Refresh token** is opaque base64url, 90-day TTL. Refresh rotates both tokens; reusing an old refresh token revokes every session (Identity is paranoid by design).
- **SignalR**: the hub is anonymous at the gateway level; the JWT travels in the query string (`?access_token=…`). The backend's `JwtBearerEvents.OnMessageReceived` extracts it only when the path starts with `/hubs/tracking`. The frontend uses `accessTokenFactory` so reconnections always carry a fresh token.
- **Protected routes**: `/carrito`, `/checkout`, `/pedidos*`, `/cuenta` are gated client-side by [`RequireAuth`](src/components/auth/require-auth.tsx). Home and merchant detail are public (Identity isn't required to browse).

---

## End-to-end test (with the backend + seed running)

1. `docker compose up -d` at the repo root.
2. Run the seed/migrate scripts that come with the backend so there are merchants and items.
3. `cd frontends/customer && npm run dev` → open <http://localhost:3000>.
4. **Register**: `/register` with a real-looking email. Identity will send a confirmation email (or log it to Seq in dev — the link goes to `/api/v1/auth/confirm-email?...`, an HTML page).
5. **Browse**: go back to `/`. You should see merchant cards from `/api/v1/merchants/nearby?lat=18.4719&lng=-69.9`. Click a card → `/comercios/<slug>`.
6. **Add to cart**: click the `+` button on a few items. The cart counter in the header updates. A floating orange bar appears at the bottom.
7. **Checkout**: open the cart, click "Continuar al checkout". TanStack Query calls `POST /api/v1/pricing/quotes`; the right column shows the breakdown + a 10-minute countdown.
8. **Place order**: click "Pagar y pedir". The FE sends a fresh `Idempotency-Key` and `POST /api/v1/orders`. On success you land on `/pedidos/<id>`.
9. **Tracking**: the page calls `GET /api/v1/tracking/orders/<id>` for the snapshot and opens a SignalR connection. As the saga moves forward (or as you simulate location pushes from the backend), the map marker animates and the timeline ticks. With the default Fake payment gateway, the saga goes `Submitted → AwaitingMerchant` and waits for a merchant to accept (or, depending on seeded behavior, auto-advances).

---

## Caveats / backend follow-ups (do not edit `/src` from here)

1. **Pricing needs `distanceKm`**, but Merchants doesn't expose pickup lat/lng to customers. Today the FE sends a constant from `NEXT_PUBLIC_DEFAULT_DISTANCE_KM`. Add `deliveryAddress` to `CreateQuote` or expose pickup coords on `GET /merchants/{id}`.
2. **Courier display** (`Marco P. ⭐4.9 • Moto • Placa K42-189`) cannot be sourced — Dispatch is courier-only and the rating field doesn't exist yet. Tracking surfaces only `courierId`. We render `Repartidor #<shortId>` and flag this here.
3. **Catalog categories** have no public endpoint. The store-detail page groups items by `categoryId` and synthesizes section labels per vertical. Add `GET /catalog/merchants/{id}/categories` to fix.
4. **Payments REST** doesn't exist in Phase 8. The customer FE renders a payment method picker but does not collect card data. To enable real Stripe Elements + 3DS, add `POST /api/v1/payments/intents` returning `{ clientSecret, paymentId }` and remove the hardcoded `pm_card_visa` in `StripePaymentGateway.cs`.
5. **Merchant "Cerrado" / operating hours / cover image** are not in `/merchants` public responses — Cerrado is a deterministic placeholder derived from the merchant id.
6. **ETA** for tracking has no backend signal — we synthesize "Llega en ~N min" from `createdAtUtc` until Tracking adds an `etaMinutes` field.
7. **Orders list pagination envelope** — `GET /orders` returns a bare array. Adding `PagedResult<T>` (like Catalog) would let us show "Página X de Y".
8. **CORS** — gateway allows `http://localhost:3000` already. If you change the dev port, update `Cors.AllowedOrigins` in the gateway's `appsettings.json`.

---

## Scripts

| Command | What it does |
|---|---|
| `npm run dev` | Next.js dev server on :3000 (HMR). |
| `npm run build` | Production build (statically renders 7 routes, server-renders 3 dynamic). |
| `npm run start` | Boot the production server (run `build` first). |
| `npm run lint` | ESLint via `next lint`. |
| `npm run typecheck` | `tsc --noEmit`. |

---

## Design notes

- Brand purple (`#534AB7`) drives the header, primary CTAs, totals, and the "active" tab underline. Coral (`#D85A30`) is reserved for the cart button, "POPULAR" / promo badges, ratings, and the order-confirmation FAB.
- Cards use 12 px radius (`rounded-xl`) and 1 px subtle borders — no heavy shadows except on hover.
- Hero panels use vertical-tinted diagonal stripe gradients (`hero-food`, `hero-pharmacy`, `hero-grocery`, `hero-parcel`, `hero-closed`) matching the screenshots.
- Mobile-first: header collapses address + search into a second row under 1024 px; cards grow from a single column to 4 columns at `xl`.
- Sentence case throughout. Currency is rendered as `RD$ N` (no decimals when integer).
