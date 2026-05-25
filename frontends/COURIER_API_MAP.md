# Rappix API Map — Courier portal

> Built from reading `/src` directly (no guessing). All paths below are reached **only** through the API Gateway at `NEXT_PUBLIC_API_URL` (default `http://localhost:5000`). YARP preserves paths — the gateway URL is the only base the frontend uses. The courier portal will live on `http://localhost:3002` (already in the gateway's CORS allowlist, see [appsettings.json:22](src/ApiGateway/Rappix.Gateway.Api/appsettings.json:22)).
>
> JSON conventions across services (same as the customer/merchant portals):
> - Money is `decimal` rounded to 2 places, sent as a plain JSON number (not cents). The currency field accompanies it (default `"DOP"`).
> - Enums are serialized as PascalCase strings (`"Online"`, `"Moto"`, `"CourierAssigned"`).
> - Strongly-typed Ids unwrap to bare GUID strings on the wire — never `{ value: "..." }`.
> - Errors are RFC 7807 ProblemDetails: `{ status, title, detail, type: "https://rappix.dev/errors/<Code>", code }`. Switch on `code`, not `detail`.
> - Coordinates are sent as `{ latitude, longitude }` doubles (NOT `{ lat, lng }`) on courier inputs — the Tracking GET REST does use `{ lat, lng }`, but no courier consumes that.

---

## 0. Who is a "Courier" and how the backend recognizes one

- **User type**: a user is created as a Courier by registering with `accountType: "Courier"` (whitelisted in [RegisterCommandValidator.cs:37-40](src/Services/Identity/Rappix.Identity.Application/Users/Register/RegisterCommandValidator.cs:37)). `UserType.Courier` is persisted in Identity ([UserType.cs:13](src/Services/Identity/Rappix.Identity.Domain/Users/UserType.cs:13)) and **embedded in every JWT** as the `userType` claim ([JwtTokenService.cs:57](src/Services/Identity/Rappix.Identity.Infrastructure/Authentication/JwtTokenService.cs:57)).
- **Authorization policy**: there is **one** service that defines `RequireCourier` — Dispatch, as `policy.RequireClaim("userType", "Courier")` ([Dispatch.Api/Program.cs:82](src/Services/Dispatch/Rappix.Dispatch.Api/Program.cs:82)). The policy is verified globally across the codebase: only `CourierEndpoints.cs` attaches it. **No other service has a courier-only endpoint.**
- **Courier aggregate**: each Courier user has exactly one `CourierProfile` row in Dispatch. The id is **1-to-1 with the userId** of Identity (`CourierId.FromUserId(userId)`, see [CourierId.cs:11](src/Services/Dispatch/Rappix.Dispatch.Domain/Couriers/CourierId.cs:11)) — there is no separate courier id. A `UserRegisteredConsumer` in Dispatch ([UserRegisteredConsumer.cs:20](src/Services/Dispatch/Rappix.Dispatch.Infrastructure/Messaging/UserRegisteredConsumer.cs:20)) creates the profile in `Offline` automatically when a user registers with `userType=Courier` (idempotent via `ExistsAsync` + the MassTransit EF inbox).
- **Ownership inside Dispatch**: every handler resolves the courier by `CourierId.FromUserId(JWT.sub)`. There is no `courierId` in any URL — everything is `/me`, scoped by the JWT. The courier cannot impersonate another courier even if they craft a different URL because there is no URL parameter to craft.
- **Lifecycle states** ([CourierStatus.cs](src/Services/Dispatch/Rappix.Dispatch.Domain/Couriers/CourierStatus.cs)): `Offline` (default after register, not in Redis Geo) → `Online` (requires `Vehicle != null`, ZADD to Redis Geo) → `Busy` (assigned to a pedido, removed from Redis Geo until released by an order-terminal event). Transition rules: `Online → Offline` always allowed; **`Busy → Offline` is forbidden** (`Dispatch.Courier.BusyCannotGoOffline` 409); `Online → Busy` and `Busy → Online` are only driven by the dispatch flow (atomic claim / `OrderTerminalEventsConsumer`), not by REST.
- **The courier never assigns themselves**. Assignment is driven server-side by the `CourierRequestedConsumer` ([CourierRequestedConsumer.cs](src/Services/Dispatch/Rappix.Dispatch.Infrastructure/Messaging/CourierRequestedConsumer.cs)) running a GEOSEARCH + atomic claim (`UPDATE ... WHERE Status='Online' RETURNING`). The portal cannot offer "accept/reject" — see Gap §D.5.

---

## A. Courier-exclusive routes

Endpoints that **only** a Courier can call. All require a JWT with `userType=Courier` (`RequireCourier` policy), and ownership is the JWT subject itself (every handler does `CourierId.FromUserId(JWT.sub)` — no URL parameter).

### A.1 Identity — there are none

Identity has **no courier-only routes**. Login/register/me/profile/change-password are shared with all roles. See section B below.

### A.2 Dispatch service — own profile, vehicle, online/offline, location, current assignment

Root group: `/api/v1/couriers` with `RequireCourier`. Defined in [CourierEndpoints.cs](src/Services/Dispatch/Rappix.Dispatch.Api/Endpoints/CourierEndpoints.cs). Six endpoints total — **this is the entire courier-facing surface area of the backend today.**

| Method | Path | Auth | Body / Query | Response | Notes |
|---|---|---|---|---|---|
| GET | `/api/v1/couriers/me` | bearer + Courier | — | `CourierResponse` | Returns the courier owned by `JWT.sub`. Includes `status` (`Offline\|Online\|Busy`), `vehicle`, `lastLocation`. **The portal reads `status` to decide what UI to show** (onboarding/vehicle setup vs go-online button vs assignment view). `404 Dispatch.Courier.NotFound` if the consumer hasn't projected the row yet (very brief race after register; retry once). |
| PUT | `/api/v1/couriers/me/vehicle` | bearer + Courier | `UpdateVehicleRequest { vehicleType, plate?, capacityKg? }` | `CourierResponse` | `vehicleType ∈ {"Moto", "Bici", "Carro"}` (case-insensitive, see [UpdateVehicleCommand.cs:22-27](src/Services/Dispatch/Rappix.Dispatch.Application/Couriers/UpdateVehicle/UpdateVehicleCommand.cs:22)). `plate` is optional (Bici riders may not have one) and is **trimmed + uppercased** server-side; max 20 chars (`Dispatch.Courier.InvalidVehiclePlate` 400). `capacityKg` is optional and must be `> 0` if provided (`Dispatch.Courier.InvalidVehicleCapacity` 400). Allowed in any status (even `Busy` — to fix a plate typo mid-shift). |
| POST | `/api/v1/couriers/me/online` | bearer + Courier | — | `CourierResponse` | Transitions `Offline → Online`. **Requires `Vehicle != null`** (`Dispatch.Courier.VehicleRequired` 400, "Configura tu vehiculo antes de ir online"). Idempotent (already-online → no-op success). Forbidden from `Busy` (`Dispatch.Courier.InvalidTransition` 409). Side effects: if `LastLocation` is known, the handler does a `GEOADD` to Redis after `SaveChanges` so the courier is immediately a candidate for matching. |
| POST | `/api/v1/couriers/me/offline` | bearer + Courier | — | `CourierResponse` | Transitions `Online → Offline`. **Forbidden if `Busy`** (`Dispatch.Courier.BusyCannotGoOffline` 409) — the courier must wait for the order to reach a terminal state before disconnecting. Idempotent. Side effects: `ZREM` from Redis Geo so no more matches are attempted. |
| POST | `/api/v1/couriers/me/location` | bearer + Courier | `ReportLocationRequest { latitude, longitude }` | `CourierResponse` | **This is the location heartbeat.** See section **§E (Location flow)** for the full discussion. `latitude ∈ [-90, 90]`, `longitude ∈ [-180, 180]` (FluentValidation, see [ReportLocationCommand.cs:19-26](src/Services/Dispatch/Rappix.Dispatch.Application/Couriers/ReportLocation/ReportLocationCommand.cs:19)). Allowed in any status (Offline couriers can still call it; their position just isn't put back in Redis until they go Online). Always raises `CourierLocationReportedDomainEvent` → `CourierLocationUpdatedIntegrationEvent` to the bus (Tracking consumes it). |
| GET | `/api/v1/couriers/me/current-assignment` | bearer + Courier | — | `CurrentAssignmentResponse` **or 204 No Content** | Returns the active `CourierAssignment` for this courier (the row with `ReleasedAtUtc IS NULL`). **Crucially, returns 204 (no body) when there is no active assignment** — the portal must handle `204` distinctly from `200`. Payload only contains `assignmentId`, `orderId`, `assignedAtUtc` — **no pickup address, no delivery address, no customer info, no items, no totals**. See Gap §D.5.1. |

```ts
type CourierStatus = "Offline" | "Online" | "Busy";
type VehicleType   = "Moto" | "Bici" | "Carro";

type VehicleResponse = {
  type: VehicleType;
  plate: string | null;        // uppercased, ≤ 20 chars; null for Bici without plate
  capacityKg: number | null;   // > 0 when set
};

type LocationResponse = {
  latitude: number;            // [-90, 90]
  longitude: number;           // [-180, 180]
  reportedAtUtc: string;       // ISO-8601 UTC
};

type CourierResponse = {
  courierId: string;           // == userId of Identity (1-1)
  firstName: string;           // cached from UserRegistered
  status: CourierStatus;
  vehicle: VehicleResponse | null;     // null until PUT /me/vehicle
  lastLocation: LocationResponse | null; // null until POST /me/location
  createdAtUtc: string;
  updatedAtUtc: string | null;
};

type CurrentAssignmentResponse = {
  assignmentId: string;        // GUID v7 of the CourierAssignment row
  orderId: string;             // the assigned Order
  assignedAtUtc: string;       // ISO-8601 UTC
};
```

> ⚠️ **There is no `mark-picked-up`, no `mark-in-transit`, no `accept-assignment`, no `reject-assignment`, no `mark-delivered` for couriers, no `assignment-history`, no `earnings`.** The only state transition the courier can drive is location heartbeats. See section §D.5 for everything missing.

### A.3 Orders — there are none

Orders has **zero** courier-targeted endpoints. The only "any authenticated user" endpoint on Orders is the seam `/orders/{orderId}/mark-delivered` (§C below) — it has no ownership check at all and is marked TEMPORARY. **Do not call it from the courier portal**; it will be removed.

### A.4 Tracking — there are none for the courier

Tracking's REST and Hub both check `JWT.sub == OrderTracking.CustomerUserId` ([GetOrderTrackingQuery.cs:25](src/Services/Tracking/Rappix.Tracking.Application/OrderTrackings/GetOrderTracking/GetOrderTrackingQuery.cs:25), [TrackingHub.cs:50-54](src/Services/Tracking/Rappix.Tracking.Api/Hubs/TrackingHub.cs:50)). A courier calling `GET /api/v1/tracking/orders/{orderId}` for the very order they're delivering gets `404 Tracking.OrderTracking.NotFound` (the same code as "doesn't exist" — intentionally indistinguishable). The Tracking hub only pushes to the order's customer group; **the courier is never a recipient.** See Gap §D.5.

---

## B. Public / shared routes the courier uses

### B.1 Identity (port 5001 direct; reached via gateway `/api/v1/auth/*` and `/api/v1/users/*`) — login, register, refresh, profile

All shared with Customers, Merchants, Admins. The courier portal uses these as-is. Same shape as the merchant portal, with one difference: `accountType: "Courier"` on register.

| Method | Path | Auth | Notes |
|---|---|---|---|
| POST | `/api/v1/auth/register` | anon | `{ email, phoneNumber?, password, firstName, lastName, accountType }` → `UserResponse`. **For the courier portal pass `accountType: "Courier"`** — this triggers Dispatch to create the `Offline` `CourierProfile` (idempotent, via the `UserRegisteredIntegrationEvent` consumer at [UserRegisteredConsumer.cs:23](src/Services/Dispatch/Rappix.Dispatch.Infrastructure/Messaging/UserRegisteredConsumer.cs:23)). Other values (`"Customer"`, `"Merchant"`) are whitelisted in [RegisterCommandValidator.cs:37-40](src/Services/Identity/Rappix.Identity.Application/Users/Register/RegisterCommandValidator.cs:37); anything else returns 400. |
| POST | `/api/v1/auth/login` | anon | `{ identifier, password }` → `AuthResponse`. `identifier` is email **or** phone. The courier portal can hard-fail if `user.userType !== "Courier"` to prevent customers/merchants from logging in here. |
| POST | `/api/v1/auth/refresh` | anon | `{ refreshToken }` → new `AuthResponse`. Rotates both tokens. On `Identity.Token.ReuseDetected` all sessions are revoked → force re-login. |
| POST | `/api/v1/auth/logout` | anon | `{ refreshToken }` → 204. |
| GET | `/api/v1/auth/confirm-email?userId=&token=` | anon | HTML response. Used by the email-confirmation link. Unlike the merchant portal, **the backend does NOT require `email_confirmed=true` for any courier-side endpoint today** (there is no `submit-for-approval` step for couriers — they just go Online). The portal can still enforce it as a UX gate before allowing PUT `/me/vehicle`. |
| POST | `/api/v1/auth/resend-confirmation` | anon | `{ email }` → 204. Always succeeds (no email-enumeration leak). |
| GET | `/api/v1/auth/google` | anon | Browser challenge → 302 to Google. The callback creates the user with `UserType.Customer` by default (Google sign-in doesn't carry `accountType`). **For couriers, prefer email/password and pass `accountType: "Courier"` explicitly** — Google sign-up would never produce a Courier today. |
| GET | `/api/v1/auth/me` | bearer | → `UserResponse`. Same shape across roles. The courier portal calls this on app start to (a) prove the token is still valid and (b) confirm `userType === "Courier"`. |
| PUT | `/api/v1/users/me` | bearer | `{ firstName, lastName, phoneNumber? }` → `UserResponse`. The courier's personal profile (Identity-level — not the same as `CourierProfile` in Dispatch, which only stores `firstName` as a cache). **`firstName` changes here are NOT propagated to Dispatch's `CourierProfile.FirstName`** (it's set once when the `UserRegistered` event fires; no follow-up `UserUpdated` event exists). Ambiguous gap — see §D.5.10. |
| POST | `/api/v1/users/me/change-password` | bearer | `{ currentPassword, newPassword }` → 204. Revokes all active refresh tokens. |

```ts
type UserType = "Customer" | "Merchant" | "Courier" | "Admin";

type UserResponse = {
  id: string;
  email: string;
  phoneNumber: string | null;
  firstName: string;
  lastName: string;
  userType: UserType;
  emailConfirmed: boolean;
  phoneConfirmed: boolean;
  createdAtUtc: string;
};

type AuthResponse = {
  accessToken: string;             // HS256 JWT
  accessTokenExpiresAtUtc: string; // ISO-8601 UTC
  refreshToken: string;            // opaque
  user: UserResponse;
};
```

### B.2 Health

| Method | Path | Notes |
|---|---|---|
| GET | `/health` on the gateway | Aggregated JSON over the 9 downstreams (`Healthy\|Degraded\|Unhealthy`). The courier portal can use it as a "backend up?" check on startup. |

---

## C. Services the courier does NOT use (verified, do not call)

These services are protected with policies that explicitly exclude `userType=Courier`, or they enforce ownership that the courier's `JWT.sub` cannot satisfy. Verified by `RequireCourier` only appearing in Dispatch, and by reading each handler's ownership check.

| Service | Path prefix at gateway | Policy | What it's for | Why the courier doesn't use it |
|---|---|---|---|---|
| **Pricing (customer)** | `/api/v1/pricing/{**}` | `RequireCustomer` ([PricingEndpoints.cs:20](src/Services/Pricing/Rappix.Pricing.Api/Endpoints/PricingEndpoints.cs:20)) | `POST /quotes`, `GET /quotes/{id}` — customer quotes a cart before placing the order. | Couriers don't quote. `403`. |
| **Pricing (admin)** | `/api/v1/admin/pricing/{**}` | `RequireAdmin` | Surge rules + coupon CRUD. | Not the courier. |
| **Orders (customer)** | `POST /api/v1/orders`, `GET /api/v1/orders/{id}`, `GET /api/v1/orders`, `POST /api/v1/orders/{id}/cancel` | `RequireCustomer` ([OrderEndpoints.cs:22](src/Services/Orders/Rappix.Orders.Api/Endpoints/OrderEndpoints.cs:22)) | Place / list-own / get-own / cancel. | Courier gets `403`. The `GetOrderQuery` handler also compares `order.CustomerUserId == JWT.sub` so even if the policy were lifted, a courier's id would 404. **This is exactly why the courier cannot see the pickup/delivery addresses of the order they were just assigned** — see Gap §D.5.1. |
| **Orders (merchant)** | `/api/v1/orders/merchant/pending`, `/orders/{id}/accept`, `/orders/{id}/reject` | `RequireMerchant` | Merchant inbox. | Not the courier. |
| **Orders (seam)** | `POST /api/v1/orders/{id}/mark-delivered` | `RequireAuthorization()` (any auth, no role) ([OrderSeamEndpoints.cs:17](src/Services/Orders/Rappix.Orders.Api/Endpoints/OrderSeamEndpoints.cs:17)) | TEMPORARY seam from Fase 5: marks an order delivered manually so the saga reaches `Completed`. The handler does **no ownership check** ([MarkDeliveredCommandHandler.cs:13-30](src/Services/Orders/Rappix.Orders.Application/Orders/MarkDelivered/MarkDeliveredCommandHandler.cs:13)) — any authenticated user with a valid orderId can fire it. | **Do not use from the courier portal.** It is labeled "TEMPORAL hacia Dispatch", slated for removal "Cuando llegue Dispatch real" — and Dispatch *is* real now (Fase 6 done). The fact that it survives is the only reason the saga can reach `Completed` today; see Gap §D.5.3 for the impact. |
| **Merchants (admin)** | `/api/v1/admin/merchants/{**}` | `RequireAdmin` | Approve / suspend merchants. | Not the courier. |
| **Merchants (merchant)** | `/api/v1/merchants/me/*` | `RequireMerchant` | Merchant self-service. | Not the courier. |
| **Merchants (public)** | `/api/v1/merchants/nearby`, `/api/v1/merchants/{id}`, `/api/v1/merchants/by-slug/{slug}`, `/api/v1/merchants/{id}/logo-url` | anon | Public discovery. | The courier *could* call these (they're anonymous) to render the merchant logo and name on the assignment screen — `PublicMerchantResponse` returns `{ id, name, slug, description, verticalType, averageRating, totalReviews }` ([PublicMerchantResponse.cs:6](src/Services/Merchants/Rappix.Merchants.Application/Responses/PublicMerchantResponse.cs:6)). **But the pickup address / coordinates are NOT exposed in this DTO** (they're only readable through the merchant's own `/me`). So the public endpoint is enough to label the assignment but not to navigate to it. See Gap §D.5.1. |
| **Catalog** | `/api/v1/catalog/me/*` (`RequireMerchant`), `/api/v1/catalog/items/*` (anon) | mix | Merchant catalog management + public item search. | Not the courier. The order line items embedded in the order are also not visible to the courier. |
| **Tracking REST** | `GET /api/v1/tracking/orders/{orderId}` | `RequireAuthorization()` (any auth) BUT ownership = `OrderTracking.CustomerUserId` | Live snapshot of the order tracking. | The handler at [GetOrderTrackingQuery.cs:25-28](src/Services/Tracking/Rappix.Tracking.Application/OrderTrackings/GetOrderTracking/GetOrderTrackingQuery.cs:25) collapses 404 and 403 into the same `Tracking.OrderTracking.NotFound` for the customer of the order. A courier calling it for an order they're delivering gets the same 404. **The courier cannot get the pickup/delivery coordinates this way either.** Documented backend gap §D.5.1. |
| **Tracking Hub** | `/hubs/tracking` (SignalR) | `[Authorize]` + ownership = `CustomerUserId` | Live push of `LocationUpdated` / `StatusChanged` to the customer. | The hub's `Subscribe(orderId)` validates `snapshot.CustomerUserId == JWT.sub` ([TrackingHub.cs:50](src/Services/Tracking/Rappix.Tracking.Api/Hubs/TrackingHub.cs:50)) and throws `HubException("no autorizado")` for anyone else (the same message used for "doesn't exist"). **The courier does not subscribe to this hub.** See §E for the actual location architecture. |
| **Payments** | `/api/v1/payments/{**}` | (no courier REST endpoints exist) | Only `/payments/webhooks/stripe` (anonymous, Stripe-signed). | Nothing the courier should call. |
| **Notifications** | (no REST endpoints) | — | Background consumer + projections only. | No client-facing API. |

---

## D. Courier flows (which endpoint chains drive which UI)

### D.1 Onboarding (register → confirm-email → set vehicle → go online)

```
1. POST /api/v1/auth/register   { ..., accountType: "Courier" }
                                → 201, UserResponse (no JWT yet)
                                → backend emits UserRegisteredIntegrationEvent
                                → Dispatch consumer creates the CourierProfile in Offline (idempotent)

2. POST /api/v1/auth/login      { identifier, password }
                                → 200, AuthResponse (JWT carries userType=Courier)

3. [user clicks email link]  GET /api/v1/auth/confirm-email?userId=&token=
                                → HTML "Email confirmado"
                                (Note: no backend endpoint forces email-confirmed for couriers today,
                                 unlike Merchants submit-for-approval. The portal may still gate.)
   POST /api/v1/auth/refresh   { refreshToken }   ← issues a JWT with email_confirmed=true

4. GET /api/v1/couriers/me      → CourierResponse, status="Offline", vehicle=null, lastLocation=null
                                  (the portal reads this to render the onboarding step)

5. PUT /api/v1/couriers/me/vehicle  { vehicleType: "Moto"|"Bici"|"Carro", plate?, capacityKg? }
                                → 200, CourierResponse with vehicle set
                                → 400 Dispatch.Courier.InvalidVehiclePlate     (plate > 20 chars)
                                → 400 Dispatch.Courier.InvalidVehicleCapacity  (capacityKg <= 0)

6. POST /api/v1/couriers/me/location  { latitude, longitude }       ← optional but recommended
                                → 200, CourierResponse with lastLocation set
                                → enables the GEOADD on step 7 (otherwise GoOnline saves Online
                                  in DB but does no GEOADD until the next location report).

7. POST /api/v1/couriers/me/online
                                → 200, CourierResponse with status="Online"
                                → 400 Dispatch.Courier.VehicleRequired  (must complete step 5 first)
                                → 409 Dispatch.Courier.InvalidTransition (already Busy)
                                → side effect: ZADD to Redis Geo if LastLocation is known
```

**Status-aware UI**:
- `Offline` + no vehicle → onboarding step (PUT vehicle).
- `Offline` + vehicle → "Go online" button.
- `Online` + no `currentAssignment` (204 from step §D.3.1) → waiting state with location loop.
- `Online` + has `currentAssignment` (200 from §D.3.1) → assignment screen + location loop.
- `Busy` → assignment screen, "go offline" disabled. (Status transitions to `Busy` server-side when the saga publishes `CourierAssigned`, but the courier observes the change by polling `/me/current-assignment` — there is no push.)

### D.2 Daily shift — go online / go offline

```
[on shift start]
  POST /api/v1/couriers/me/location  { latitude, longitude }    ← seed before going online
  POST /api/v1/couriers/me/online                                → 200, status="Online"
  → start the location loop (§D.4)
  → start the assignment poll  (§D.3)

[on shift end, if not Busy]
  POST /api/v1/couriers/me/offline                               → 200, status="Offline"
  → stop both loops

[on shift end, if Busy]
  → backend rejects offline with 409 Dispatch.Courier.BusyCannotGoOffline
  → the courier must complete (or backend must cancel) the active order first.
  → "complete" today = either the order's customer cancels, the seam mark-delivered fires,
     or the saga fails with a terminal. There is no courier-driven completion.
     See Gap §D.5.3.
```

### D.3 Receiving an assignment (polling, NOT push)

The backend does NOT push to the courier when a new assignment lands. The portal must **poll** `/me/current-assignment`. The assignment payload contains only `assignmentId`, `orderId`, `assignedAtUtc` — there is no embedded order info.

```
[loop while Online]
  GET /api/v1/couriers/me/current-assignment
       → 204 No Content        → still waiting, sleep ~5s, retry
       → 200 CurrentAssignmentResponse  { assignmentId, orderId, assignedAtUtc }
         → the moment the orderId changes (or appears for the first time),
           the courier has been assigned. Status in /me is now "Busy".
         → render the assignment screen using only what fits in this payload
           + whatever the portal can derive (see Gap §D.5.1 for what is missing).
```

> ⚠️ **No SignalR for the courier.** `/hubs/tracking` is customer-only by ownership. There is no separate courier hub. Polling is the only option today.

The 204-No-Content semantics is unusual — most "list" endpoints return `[]`. Here the handler ([GetCurrentAssignmentQuery.cs:24-32](src/Services/Dispatch/Rappix.Dispatch.Application/Couriers/GetCurrentAssignment/GetCurrentAssignmentQuery.cs:24)) returns `Result.Success<…>(null)`, and the endpoint maps null to `Results.NoContent()` ([CourierEndpoints.cs:89](src/Services/Dispatch/Rappix.Dispatch.Api/Endpoints/CourierEndpoints.cs:89)). The frontend must check `response.status === 204` separately from a `200`. **Only one assignment is ever active at a time** (enforced by a unique partial index on `(CourierId) WHERE ReleasedAtUtc IS NULL` in Dispatch; see [CourierAssignment.cs:7-10](src/Services/Dispatch/Rappix.Dispatch.Domain/Couriers/CourierAssignment.cs:7)).

### D.4 Reporting location (the location heartbeat)

```
[loop while Online or Busy]
  POST /api/v1/couriers/me/location  { latitude, longitude }
       → 200 CourierResponse
       → 400 Dispatch.Courier.InvalidLocation  (lat outside [-90,90] or lng outside [-180,180])
       → fire-and-forget retry on transient 5xx / network — the next tick will reconcile.
```

Cadence guidance:
- **Server-side throttle**: none specific. The gateway's authenticated rate limit is **300 req/min** sliding-window per `sub` claim ([gateway Program.cs:100-101, 130-139](src/ApiGateway/Rappix.Gateway.Api/Program.cs:100)) — call this once per 200 ms (300/min) and you exhaust the budget for everything else. Default ≥ 1 s between reports is safe. A practical sweet spot is **3–5 s while Busy, 10–15 s while Online-idle**.
- **Stale dedup at Tracking**: `OrderTracking.ApplyLocation` rejects any update whose `reportedAtUtc <= LastLocationAtUtc` ([OrderTracking.cs:149-168](src/Services/Tracking/Rappix.Tracking.Domain/OrderTrackings/OrderTracking.cs:149)). Out-of-order reports are silently dropped — no error to the courier, no error to the customer. So overlapping in-flight requests are harmless.
- **No Idempotency-Key needed**. The `IdempotencyMiddleware` only kicks in *if* the header is present ([IdempotencyMiddleware.cs:25](src/BuildingBlocks/Rappix.BuildingBlocks.WebApi/Middleware/IdempotencyMiddleware.cs:25)). Location reports are deliberately replayable, so don't send one.

### D.5 Gaps the portal will have to design around

These are **real limitations in the current backend** as of phase 13. The courier portal cannot:

1. **See the pickup address, delivery address, customer name, or items of the assigned order.** `CurrentAssignmentResponse` only has `{ assignmentId, orderId, assignedAtUtc }`. `GET /api/v1/orders/{id}` is `RequireCustomer` + ownership-bound. `GET /api/v1/tracking/orders/{id}` collapses 404/403 against the courier. `GET /api/v1/merchants/{id}` is anonymous but `PublicMerchantResponse` *omits* the pickup coordinate. **Today the courier knows an orderId and nothing else.** *Workaround*: render the assignment with a "contact dispatch" stub; there is no client-side workaround that recovers the addresses from the public surface. Backend gap to file: expose `GET /api/v1/couriers/me/current-assignment` with a richer payload (pickup address from Merchants, delivery address + customer first name + line totals from Orders), or add a Tracking ownership check for `LastCourierId`.

2. **Receive a push when a new assignment lands.** No courier hub, no SignalR group, no SSE. *Workaround*: poll `/me/current-assignment` every 5–10 s while Online; on `Online → Busy` transition (observed via `/me`), tighten the poll to 2 s for a brief window to catch the assignment immediately. Backend gap to file: a courier SignalR hub (or reuse `/hubs/tracking` with a second group keyed by `CourierId`).

3. **Mark "picked up at merchant" or "in transit".** The TrackingStatus enum has only `Placed | MerchantAccepted | CourierAssigned | Delivered | Cancelled | Failed` ([TrackingStatus.cs](src/Services/Tracking/Rappix.Tracking.Domain/OrderTrackings/TrackingStatus.cs)) — the intermediate states are **explicitly listed as a future ADR-0008 follow-up** ("Estados intermedios mas finos (Preparing/PickedUp/InTransit) son un follow-up para cuando la saga publique OrderStatusChangedIntegrationEvent"). There is no command, no event, no domain method for the courier to advance the order through pickup or in-transit. *Workaround*: the assignment screen treats the entire "I have the order, I'm driving" period as one undifferentiated state. The customer's tracking timeline jumps `MerchantAccepted → CourierAssigned → Delivered` with no detail in between.

4. **Mark the order delivered (courier-driven).** Dispatch has no `complete-assignment` endpoint. The saga's `Completed` state today is reached **only** when something publishes `OrderDeliveredIntegrationEvent`. The only producer of that event in the codebase is the **seam endpoint** `POST /api/v1/orders/{id}/mark-delivered`, marked TEMPORARY at [OrderSeamEndpoints.cs:8-10](src/Services/Orders/Rappix.Orders.Api/Endpoints/OrderSeamEndpoints.cs:8) and at [MarkDeliveredCommand.cs:7-9](src/Services/Orders/Rappix.Orders.Application/Orders/MarkDelivered/MarkDeliveredCommand.cs:7) ("Eliminar en Fase 6"). The endpoint has no ownership check whatsoever — any authenticated user (customer, merchant, courier, admin) can mark any order delivered if they know its id. **Two design choices for the portal**:
   - **Avoid the seam** (recommended) — render the "Entregar" button as disabled with a tooltip "esperando confirmacion del backend" and let the customer or an admin trigger completion out of band. This keeps the portal honest about the gap.
   - **Use the seam knowingly** — the courier portal calls `POST /api/v1/orders/{orderId}/mark-delivered` (no body, bearer JWT) and treats it as the courier-completion action. **Add an explicit FE-side ownership check** by comparing `orderId` against the assignment they currently hold. Document loudly in the portal's README that this will break the day the seam is removed (the call should be replaced with a proper Dispatch endpoint like `POST /api/v1/couriers/me/current-assignment/complete`). Backend gap to file: add a real courier-completion endpoint with ownership against `CourierAssignment.CourierId == JWT.sub`.

5. **Reject or hand back an assignment.** Dispatch's claim is atomic and one-shot — the courier is just "told" they have it (via the status change to `Busy`). The unique partial index `(CourierId) WHERE ReleasedAtUtc IS NULL` actively prevents a second assignment, so the courier cannot even "pass" it. The only release paths are server-side: `OrderTerminalEventsConsumer` releases on `OrderDelivered | OrderCancelled | OrderFailed`. *Workaround*: no client workaround. Backend gap to file: a `POST /api/v1/couriers/me/current-assignment/decline` that emits a compensation event re-queueing the order.

6. **See past deliveries / earnings / ratings.** No `/me/history`, no `/me/earnings`, no `/me/ratings`. Searched the entire `/src` for `earnings|payout|courierRating` — zero hits. Today, the entity `CourierAssignment` keeps a row per past assignment with `ReleasedAtUtc` and `ReleaseReason` (`delivered|cancelled|failed`), but **no read endpoint exposes it**. *Workaround*: keep a client-side append-only log of every `currentAssignment.orderId` the portal has seen this session. Backend gap to file: `GET /api/v1/couriers/me/assignments?status=&from=&to=&page=&pageSize=` projecting `CourierAssignment`.

7. **Receive real-time order status changes (e.g., customer cancelled while courier is driving).** No push. The courier discovers cancellation only when the assignment disappears from `/me/current-assignment` (because `OrderTerminalEventsConsumer` released it). *Workaround*: when the poll flips from 200 to 204 with status `Busy → Online`, the portal must assume an out-of-band release happened and show a "el pedido fue cancelado/fallido" toast — there is no payload to tell the courier *why* the assignment vanished. Backend gap to file: include the release reason in the next poll (e.g., return a "last release" attached to the courier `/me` for one tick after release).

8. **Chat with the customer or merchant.** No `/messages`, no presence channel, nothing. *Workaround*: out of scope for this phase.

9. **Edit personal profile beyond the Identity-level `PUT /users/me`.** The courier's Dispatch-side display name (`CourierProfile.FirstName`) is only filled once from the `UserRegistered` event. Changing `firstName` via `PUT /users/me` does NOT trigger an update to Dispatch — there is no `UserUpdatedIntegrationEvent` consumer in Dispatch (confirmed by listing Dispatch's `Messaging/` folder, only `UserRegisteredConsumer`, `CourierRequestedConsumer`, `OrderTerminalEventsConsumer`). This is unlikely to surface to customers (the customer-facing courier name comes from `LastCourierId`, which the customer never resolves to a name today), but the courier's own `/me` will show the original first name forever. Backend gap to file: a `UserUpdatedIntegrationEvent` consumer in Dispatch.

10. **Refresh the location-loop interval based on backend signal.** No `Retry-After`, no rate-limit hint specific to `/location`. The frontend must pick a static cadence (3–5 s while Busy, 10–15 s while Online-idle is a reasonable starting point — see §D.4).

---

## E. **Location flow — REST, not SignalR (architecture A)**

> **This is the most important section of this document.** The courier portal does **NOT** need `@microsoft/signalr` as a dependency.

The full flow, traced through the code:

```
[courier app, frequent]
   POST /api/v1/couriers/me/location  { latitude, longitude }    ← REST, bearer JWT
       │
       ▼
[Gateway, port 5000]
   Route "dispatch-couriers" matches /api/v1/couriers/{**} → cluster "dispatch" → http://localhost:5006/
   (default FallbackPolicy = RequireAuthenticatedUser; JWT validated at gateway AND at Dispatch)
       │
       ▼
[Dispatch.Api, port 5006 → port 8080 in Kestrel]
   CourierEndpoints.MapPost("/me/location", …) [RequireCourier]
       → ReportLocationCommand(userId=JWT.sub, lat, lng)
       → ReportLocationCommandHandler:
            • CourierProfile.ReportLocation(lat, lng, now)
                → raises CourierLocationReportedDomainEvent (in-memory)
            • SaveChangesAsync()        ← writes Dispatch DB + MassTransit outbox in ONE tx
            • if Status == Online: IRedisGeoIndex.AddOrUpdateAsync (best-effort, post-commit)
       → MediatR DomainEventNotification dispatches the domain event:
            CourierLocationReportedDomainEventHandler.Handle(...)
                → publishEndpoint.Publish(CourierLocationUpdatedIntegrationEvent {
                    CourierId, Latitude, Longitude, ReportedAtUtc
                  })   ← goes through the SAME outbox row written by SaveChanges above
       → Response 200 OK, body = CourierResponse
       │
       ▼ (MassTransit outbox publisher dispatches asynchronously)
[RabbitMQ]
   CourierLocationUpdatedIntegrationEvent fanned out to Tracking's queue
       │
       ▼
[Tracking.Api, port 5007]
   CourierLocationUpdatedConsumer.Consume(...)
       → ICourierActiveOrderRepository.GetByCourierIdAsync(courierId)  ← PK lookup
         (this row was created by CourierAssignedConsumer when the courier was matched)
            • if no mapping (courier reporting Online-idle): silent debug log, exit
       → IOrderTrackingRepository.GetByOrderIdAsync(orderId)
       → OrderTracking.ApplyLocation(courierId, lat, lng, reportedAtUtc)
            • rejects if courierId != LastCourierId, or if reportedAtUtc <= LastLocationAtUtc
       → SaveChangesAsync
       → IClientNotifier.PushLocation(orderId, lat, lng, reportedAtUtc, courierId)
       │
       ▼
[SignalR, on Tracking, hub /hubs/tracking]
   Clients.Group("order:" + orderId).LocationUpdated({ latitude, longitude, reportedAtUtc, courierId })
       │
       ▼
[The CUSTOMER's browser]
   Their @microsoft/signalr connection (subscribed via Subscribe(orderId), ownership checked
   against CustomerUserId) receives LocationUpdated and updates the map marker.
```

**The courier app emits via REST**. The customer app receives via SignalR. **The two clients never talk to each other directly, and they never share a hub.** All fan-out is server-side.

### What this means for the portal stack

- **No `@microsoft/signalr` dependency.** No `HubConnectionBuilder`, no `withUrl("/hubs/tracking")`, no `accessTokenFactory`. The courier portal is plain `fetch` against the gateway.
- **No `?access_token=` query string anywhere.** That pattern only exists because SignalR WebSocket upgrades can't carry the `Authorization` header — irrelevant here.
- **Idempotency-Key**: don't send one on `/location` (heartbeats are deliberately replay-safe; see §D.4).
- **Rate limit budget**: the gateway gives an authenticated `sub` 300 req/min (sliding). Reserve at least 60 of those for everything else (`/me`, `/me/current-assignment`, the seam call if used). A 3 s heartbeat = 20 req/min — comfortable.

### What WOULD be the SignalR (architecture B) shape, if it existed

For the record, **it doesn't exist** today. A courier-side hub would look like:

```
HubConnectionBuilder().withUrl(`${apiUrl}/hubs/courier?access_token=${jwt}`)
connection.invoke("UpdateLocation", { latitude, longitude })
connection.on("AssignmentReceived", payload => ...)   ← would close gap §D.5.2
connection.on("AssignmentReleased", reason => ...)    ← would close gap §D.5.7
```

There is no `/hubs/courier` route in the gateway (verified in [appsettings.json:74-86](src/ApiGateway/Rappix.Gateway.Api/appsettings.json:74) — only `/hubs/tracking` exists). There is no `CourierHub` class in `Rappix.Dispatch.Api/Hubs/` (the directory doesn't exist). Building the courier portal around SignalR is not just a different file — it would require backend work that hasn't happened.

---

## F. Flujo de estados del courier + del pedido (mapping endpoints → state)

Two finite-state machines run in parallel: the courier's `CourierStatus` (in Dispatch) and the order's `OrderStatus` (in Orders) — they are coupled by integration events but the courier portal only observes the courier side directly (and infers the order side from poll changes).

### Courier state machine

```
   ┌──── (POST /me/online if Vehicle)
   │
Offline ────────────────────────────► Online
   ▲                                   │  │
   │                                   │  │
   │   (POST /me/offline)              │  │  (server-side: claim atomico WHERE Status='Online'
   │                                   │  │   inside CourierRequestedConsumer)
   │                                   │  ▼
   │                                   │  Busy
   │                                   │  │
   │           (NOT allowed)           │  │ (server-side: OrderTerminalEventsConsumer
   │ ◄─────────────────────────────────│──┘  releases on OrderDelivered/Cancelled/Failed)
   │  Dispatch.Courier.BusyCannotGoOffline 409
   │                                   │
   │   (POST /me/offline)              │
   └───────────────────────────────────┘
```

| Transition | Driven by | What the portal does |
|---|---|---|
| `Offline → Online` | `POST /api/v1/couriers/me/online` | Courier taps "Conectarme"; portal calls the endpoint; on 200, starts the location loop and assignment poll. |
| `Online → Offline` | `POST /api/v1/couriers/me/offline` | Courier taps "Desconectarme"; portal calls; on 200, stops both loops. |
| `Online → Busy` | **Server-side** `CourierRequestedConsumer.TryClaimAsync` | Portal does not call anything. Observed by `/me/current-assignment` flipping from 204 to 200 and `/me.status` going from `Online` to `Busy` — the portal renders the assignment screen. |
| `Busy → Online` | **Server-side** `OrderTerminalEventsConsumer` on terminal event | Portal observes `/me/current-assignment` flipping from 200 to 204 (and `/me.status` going from `Busy` to `Online`). Show a "entrega completada / cancelada" toast (the portal cannot get the reason — gap §D.5.7) and return to the waiting state. |
| `Busy → Offline` | **Rejected by the backend** | Portal must disable the "Desconectarme" button while `status === "Busy"`. If the courier somehow hits it, expect `409 Dispatch.Courier.BusyCannotGoOffline`; surface the message. |

### Order state machine, from the courier's perspective

The courier only ever sees an order between `AwaitingCourier`-resolution and the terminal — and only by inference, since they can't read the order. The full `OrderStatus` enum ([OrderStatus.cs](src/Services/Orders/Rappix.Orders.Domain/Orders/OrderStatus.cs)) goes `Submitted → AwaitingMerchant → AwaitingPayment → AwaitingCourier → InProgress → Completed | Cancelled | Failed | NeedsReview`. The courier window is the `AwaitingCourier → InProgress → terminal` portion.

```
[Customer places order]
   Order: Submitted → AwaitingMerchant → AwaitingPayment → AwaitingCourier
                                                                      │
                                                                      │ Orders.Saga publishes CourierRequestedIntegrationEvent
                                                                      ▼
                                              [Dispatch.CourierRequestedConsumer]
                                              GEOSEARCH + TryClaimAsync (atomic update WHERE Status='Online')
                                                                      │
                                                          ┌───────────┴───────────┐
                                                          ▼                       ▼
                                              Claim wins for a courier   No courier won
                                                          │                       │
                                          Dispatch publishes              Dispatch publishes
                                          CourierAssignedIntegrationEvent CourierUnavailableIntegrationEvent
                                                          │                       │
                                                          ▼                       ▼
                                              Orders.Saga: → InProgress    Orders.Saga: → Cancelled
                                              Courier.Status: → Busy       Courier.Status: unchanged
                                                          │                       │
                                                          │                       ▼
                                                          │      Courier never sees this order (assignment never landed).
                                                          ▼
                                              Courier portal observes:
                                              GET /me/current-assignment → 200 { assignmentId, orderId, assignedAtUtc }
                                              GET /me                    → status="Busy"

                                              [courier drives, posting POST /me/location repeatedly]

                                              (intermediate states like "PickedUp" do NOT exist — gap §D.5.3)

                                              [the seam fires: POST /api/v1/orders/{id}/mark-delivered]
                                              (either by the courier knowingly using the seam, or by an out-of-band tool)
                                                          │
                                                          ▼
                                              Orders publishes OrderDeliveredIntegrationEvent
                                                          │
                                                          ▼
                                              Dispatch.OrderTerminalEventsConsumer:
                                              → CourierProfile.Release(now)  (Busy → Online)
                                              → CourierAssignment.Release(now, "delivered")
                                              → Redis GEOADD (back in candidate pool)
                                                          │
                                                          ▼
                                              Courier portal observes on next poll:
                                              GET /me/current-assignment → 204 No Content
                                              GET /me                    → status="Online"
                                              Order (in Orders DB) → Completed
```

| Order transition | Triggered by | Courier-visible signal |
|---|---|---|
| `AwaitingCourier → InProgress` | Saga consumes `CourierAssigned` | `/me/current-assignment` flips 204 → 200; `/me.status` flips `Online` → `Busy`. |
| `InProgress → Completed` | Saga consumes `OrderDelivered` (today only the seam emits it; in a future phase, a real courier-completion endpoint would) | `/me/current-assignment` flips 200 → 204; `/me.status` flips `Busy` → `Online`. **No reason payload.** |
| `* → Cancelled` (e.g., customer cancelled in-flight) | Saga consumes `OrderCancelled` | Same observable as above: 200 → 204, `Busy` → `Online`. The portal cannot distinguish a cancel from a delivery on its own — gap §D.5.7. |
| `* → Failed` | Saga's compensation pipeline | Same observable. Same indistinguishability. |

---

## G. Ownership & security (summary)

Three layers stack on top of each other:

1. **JWT bearer** — every protected endpoint requires a valid HS256 JWT (`iss=https://localhost:5001`, `aud=rappix`, alive `exp`, signed with the shared `Jwt:SigningKey`). `MapInboundClaims=false`, `NameClaimType=sub` — `ClaimsPrincipal.GetUserId()` reads `sub` as a `Guid`. The gateway validates the JWT *and* Dispatch re-validates it — defense in depth.

2. **Role policy `RequireCourier`** — `policy.RequireClaim("userType", "Courier")` ([Dispatch.Api/Program.cs:82](src/Services/Dispatch/Rappix.Dispatch.Api/Program.cs:82)). Enforced on every endpoint in `CourierEndpoints`. **A Customer or Merchant JWT cannot call any of them**, even with a valid signature.

3. **Per-resource ownership** — `JWT.sub == CourierId.Value`. Implicit: there's no URL parameter to attack, the handler always builds the id from `JWT.sub`. The only attack surface would be a forged JWT, which the signing-key validation already covers.

4. **State guards** — beyond ownership:
   - `GoOnline` requires `Vehicle != null` (`Dispatch.Courier.VehicleRequired` 400).
   - `GoOffline` rejects `Busy` (`Dispatch.Courier.BusyCannotGoOffline` 409).
   - `ReportLocation` validates ranges (`Dispatch.Courier.InvalidLocation` 400 via FluentValidation).
   - All status transitions are idempotent (already-target → no-op success).

5. **What the portal does NOT need to send**:
   - No `courierId` in URLs (resolved from JWT).
   - No `Idempotency-Key` on GETs, and explicitly NOT on `/location` (heartbeats are designed to dedup by timestamp at Tracking).
   - No CSRF token (no cookies — JWT in `Authorization: Bearer` header, refresh token in body).

---

## H. Summary

- **6 courier-exclusive REST endpoints**, all in Dispatch under `/api/v1/couriers/me/*`: `GET /me`, `PUT /me/vehicle`, `POST /me/online`, `POST /me/offline`, `POST /me/location`, `GET /me/current-assignment`. That is the **entire** courier-facing backend today.
- **Shared endpoints used by the courier portal**: Identity (`/auth/register|login|refresh|logout|confirm-email|resend-confirmation|me`, `/users/me`, `/users/me/change-password`). Optionally the anonymous `/api/v1/merchants/{id}` for the merchant's display name and logo on the assignment screen (the pickup *address* is not exposed there).
- **Pricing, Orders, Tracking, Catalog, Payments, Notifications, admin endpoints**: not callable from the courier portal. **The single most painful absence is order detail** (pickup address, delivery address, customer name, items) — see Gap §D.5.1. The Tracking hub is also off-limits to the courier.
- **Two operational loops** in the portal: (a) location heartbeat (`POST /me/location` every 3–5 s while Busy, 10–15 s while Online-idle), (b) assignment poll (`GET /me/current-assignment` every 5–10 s while Online; tighten on `Online → Busy`).
- **Location flow = REST, not SignalR** (§E). The courier portal does **not** need `@microsoft/signalr`. The customer's live map is fed by Tracking consuming the bus event Dispatch publishes from `POST /me/location`. **This is the answer to the obligatory section of this investigation.**
- **Ownership**: `JWT.sub == CourierId.Value`. No URL parameters to attack. `RequireCourier` policy is exclusive to Dispatch — no other service even defines it.
- **Biggest backend gaps to close before the courier portal is fully usable**: (1) richer `current-assignment` payload with addresses, (2) a real courier-completion endpoint replacing the temporary seam, (3) intermediate states (`PickedUp`, `InTransit`), (4) a courier-side push (SignalR or SSE) for new assignments and out-of-band releases, (5) `assignment-history` + `earnings` read endpoints.
