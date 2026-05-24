# FINDINGS — Customer Frontend

Things this session uncovered that the FE works around today but that the BACKEND team should eventually pick up. **No code under `/src` was modified.**

---

## 1. SignalR tracking hub — `Failed to fetch` on negotiate (RESOLVED on the FE)

**Symptom**
The browser console showed:
> `Failed to complete negotiation with the server: TypeError: Failed to fetch`

on every load of `/pedidos/[id]`. WebSocket never connected; the live-tracking UI fell back to TanStack Query polling.

**Root cause (FE-side)**
- The gateway's `DevelopmentCors` policy (verified at `src/ApiGateway/Rappix.Gateway.Api/Program.cs:73-90`) uses `.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()`. ASP.NET CORS deliberately forbids combining `AllowAnyOrigin` with `AllowCredentials`, so the dev response is `Access-Control-Allow-Origin: *` with NO `Access-Control-Allow-Credentials: true`.
- `@microsoft/signalr` defaults `withCredentials: true` on the negotiate fetch. Browsers reject `Access-Control-Allow-Origin: *` together with credentials → CORS error surfaced as "Failed to fetch".

**Resolution**
One-line fix in `frontends/customer/src/lib/tracking/hub.ts`: set `withCredentials: false` in `.withUrl(...)`. We don't use cookies anyway — the JWT travels in the `Authorization` header for HTTP transports and in `?access_token=` for the WS upgrade.

**Status**: ✅ Fixed in this session, no backend change required. Works in BOTH dev (wildcard origin) AND prod (`ProductionCors` uses specific origins, which would accept credentials, but we explicitly disable them anyway since we have no use for cookies).

**Notes for the backend team** (informational only — no action required)
- `app.UseWebSockets()` is correctly placed before `MapReverseProxy()` (line 198 → 205). YARP cluster uses `Version: 1.1` / `VersionPolicy: RequestVersionExact` — canonical recipe for WebSocket passthrough.
- The Tracking service does NOT (and should not) have its own CORS policy — only the gateway is browser-exposed.
- The JWT-in-query extraction (`OnMessageReceived` at `Tracking/Program.cs:87-99`) only fires when the path starts with `/hubs/tracking`. That's correct; the negotiate POST passes the token via the Authorization header instead, which the default `JwtBearer` middleware reads natively.

---

## 2. No backend service for delivery addresses (CONFIRMED — handled on FE)

**Context**
Per user direction, there is no addresses service in the backend. The order body accepts the delivery address inline (`street`, `reference?`, `latitude`, `longitude`).

**FE workaround**
We built `frontends/customer/src/lib/addresses/` with a swap-ready abstraction:
- `AddressRepository` interface (list / getDefault / add / update / remove / setDefault)
- `localStorageAddressRepository` — the only impl today, persisted under `rappix.customer.addresses`
- `useAddressStore` (Zustand) — UI-facing reactive mirror

**Future migration path (when the backend lands)**
1. Add a `RemoteAddressRepository` implementing the same interface against the new endpoints. Whatever shape `GET /api/v1/addresses`, `POST`, `PUT`, `DELETE`, `POST /default` returns, the repo adapts it to `SavedAddress`.
2. Call `useAddressStore.getState().setRepository(remoteRepo)` once at boot (likely from `AuthProvider` after login, since addresses are scoped to a user).
3. **Zero component changes** — the header pill, `/direcciones` page, checkout picker, and address form dialog all consume `useAddressStore` exclusively.

**Implications for the eventual backend design**
- `SavedAddress` has FE-only metadata (`id`, `label`, `isDefault`, `createdAtIso`) that the order body deliberately ignores via `toOrderAddress(...)`. The future backend should mirror this — keep `label` / `isDefault` on the addresses table, but the order body still takes just `street/reference/lat/lng`. No need to change `POST /orders`.
- Geocoding is a separate concern. Today the form requires the user to enter `latitude`/`longitude` directly (defaulted to Santo Domingo center via `NEXT_PUBLIC_DEFAULT_LAT`/`_LNG`). Whenever a geocoder lands (server- or client-side), the form can stop showing the lat/lng inputs.

---

## 3. Login role validation (HANDLED on FE, security note)

This session enforces "Customer only" at the FE on login + hydrate. It's a defense-in-depth measure — the BACKEND should ALSO refuse non-Customer access to customer-only endpoints (which it already does for most: `Pricing`, `Orders` require `RequireCustomer`). The FE block prevents a wrong-portal user from seeing the customer UI shell at all.

No backend change requested. Just noting the layered defense.

---

## 4. Pre-existing backend gaps (carried over from `API_MAP.md`)

Not new — listed in `API_MAP.md` under "Follow-ups for the backend". Repeated here for one-stop visibility:

1. **Pricing needs `distanceKm`** but Merchants doesn't expose pickup coords publicly. FE sends a constant.
2. **No public courier snapshot** — Tracking exposes only `courierId`. We render "Repartidor #<shortId>".
3. **No public Catalog categories** — items grouped client-side by hashing.
4. **Payments has no REST in Phase 8** — checkout shows a method picker, no Stripe Elements.
5. **No tracking ETA** — synthesized from `createdAtUtc`.
6. **No public `isOpen`** / operating hours / cover image on merchants.
7. **`GET /orders` returns a bare array** — no pagination envelope.

---

_Document maintained by the FE team. Update with NEW findings as they appear._
