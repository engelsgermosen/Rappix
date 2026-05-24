# Rappix API Map — Merchant portal

> Built from reading `/src` directly (no guessing). All paths below are reached **only** through the API Gateway at `NEXT_PUBLIC_API_URL` (default `http://localhost:5000`). YARP preserves paths — the gateway URL is the only base the frontend uses.
>
> JSON conventions across services:
> - Money is `decimal` rounded to 2 places, sent as a plain JSON number (not cents). The currency field accompanies it (default `"DOP"`).
> - Enums are serialized as PascalCase strings (`"Food"`, `"Active"`, `"AwaitingMerchant"`).
> - Strongly-typed Ids unwrap to bare GUID strings on the wire — never `{ value: "..." }`.
> - Errors are RFC 7807 ProblemDetails: `{ status, title, detail, type: "https://rappix.dev/errors/<Code>", code }`. Switch on `code`, not `detail`.

---

## 0. Who is a "Merchant" and how the backend recognizes one

- **User type**: a user is created as a Merchant by registering with `accountType: "Merchant"` (see [AuthRequests.cs](src/Services/Identity/Rappix.Identity.Api/Contracts/AuthRequests.cs)). `UserType` is persisted in Identity and **embedded in every JWT** as the `userType` claim ([JwtTokenService.cs:57](src/Services/Identity/Rappix.Identity.Infrastructure/Authentication/JwtTokenService.cs:57)).
- **Authorization policy**: services define `RequireMerchant` as `policy.RequireClaim("userType", "Merchant")` (see [Merchants.Api/Program.cs:77](src/Services/Merchants/Rappix.Merchants.Api/Program.cs:77), [Catalog.Api/Program.cs:77](src/Services/Catalog/Rappix.Catalog.Api/Program.cs:77), [Orders.Api/Program.cs:77](src/Services/Orders/Rappix.Orders.Api/Program.cs:77)). Endpoints attach it with `.RequireAuthorization("RequireMerchant")`. **Three services enforce it: Merchants, Catalog, Orders.** No other service does.
- **Merchant aggregate**: each Merchant user has exactly one `Merchant` row in the Merchants service, owned by `OwnerUserId` = the user's id. A `UserRegisteredConsumer` in Merchants ([UserRegisteredConsumer.cs](src/Services/Merchants/Rappix.Merchants.Infrastructure/Messaging/UserRegisteredConsumer.cs)) creates the row in `Draft` state automatically when a user registers with `userType=Merchant` (idempotent).
- **Cross-service resolution**: when Catalog or Orders need to act on "this merchant's" data, the merchant is resolved from the JWT subject in one of two ways:
  - **Catalog** calls a `ResolveMerchantIdQuery(ownerUserId)` against its local `Catalog` projection (the row Catalog stores when it consumes `MerchantApprovedDomainEvent`) — returns `404 Catalog.NotFound` if the merchant isn't approved yet ([ResolveMerchantIdQueryHandler.cs](src/Services/Catalog/Rappix.Catalog.Application/Catalogs/ResolveMerchant/ResolveMerchantIdQueryHandler.cs)).
  - **Orders** stores `MerchantOwnerUserId` on every `Order` aggregate (filled at order creation via the gRPC `MerchantValidationService`). Authorization is `order.MerchantOwnerUserId == JWT.sub` ([AcceptOrderCommandHandler.cs:26](src/Services/Orders/Rappix.Orders.Application/Orders/Accept/AcceptOrderCommandHandler.cs:26), [RejectOrderCommandHandler.cs:23](src/Services/Orders/Rappix.Orders.Application/Orders/Reject/RejectOrderCommandHandler.cs:23)).
- **Lifecycle states** ([MerchantStatus.cs](src/Services/Merchants/Rappix.Merchants.Domain/Merchants/MerchantStatus.cs)): `Draft` → `Pending` → `Active` (the happy path). Also `Paused`, `Suspended`, `Rejected`. Only `Active` is operational; `Suspended` / `Rejected` are dead-ends ([Merchant.cs:375](src/Services/Merchants/Rappix.Merchants.Domain/Merchants/Merchant.cs:375)). A merchant in `Draft` can edit and submit; `Pending` is awaiting admin approval; `Active` can be discovered, list a catalog, and receive orders.

---

## A. Merchant-exclusive routes

Endpoints that **only** a Merchant can call. All require a JWT with `userType=Merchant` (`RequireMerchant` policy), and most also enforce ownership of the resource against the JWT subject.

### A.1 Identity — there are none

Identity has **no merchant-only routes**. Login/register/me/profile are shared with all roles. See section B below.

### A.2 Merchants service — own profile, areas, hours, pickup, logo, submit

Root group: `/api/v1/merchants` with `RequireMerchant`. Defined in [MerchantEndpoints.cs](src/Services/Merchants/Rappix.Merchants.Api/Endpoints/MerchantEndpoints.cs).

| Method | Path | Auth | Body / Query | Response | Notes |
|---|---|---|---|---|---|
| GET | `/api/v1/merchants/me` | bearer + Merchant | — | `MerchantResponse` | Returns the merchant owned by `JWT.sub`. Includes `status` (`Draft|Pending|Active|Paused|Suspended|Rejected`), `serviceAreas`, `operatingHours`, `logoObjectKey`, `commissionPercentage`. **The portal reads `status` to decide what UI to show** (onboarding wizard vs operational dashboard). `404 Merchants.Merchant.NotFound` if the Draft row hasn't been projected yet (race against the consumer; very brief). |
| PUT | `/api/v1/merchants/me` | bearer + Merchant | `UpdateMerchantProfileRequest { name, slug, rnc?, description?, verticalType }` | `MerchantResponse` | `verticalType ∈ {Food, Pharmacy, Grocery, Parcel}`. `slug` is the public URL token; must be kebab-case and unique (`Merchants.Slug.Taken` if not). Editable only in `Draft` (review the domain — outside Draft most fields are locked). |
| PUT | `/api/v1/merchants/me/operating-hours` | bearer + Merchant | `UpdateOperatingHoursRequest { hours: [{ dayOfWeek, opensAt, closesAt }] }` | `MerchantResponse` | Bulk replace. `dayOfWeek` is the .NET `DayOfWeek` enum name (`"Monday"`...). `opensAt`/`closesAt` are `"HH:mm"` strings. |
| POST | `/api/v1/merchants/me/service-areas` | bearer + Merchant | `AddServiceAreaRequest { type: "Polygon"\|"Circle", polygon?: [[lng,lat],...], centerLatitude?, centerLongitude?, radiusMeters? }` | `MerchantResponse` (includes the new area id) | `Polygon` ring is `[lng, lat]` pairs (GeoJSON axis order) — opposite of what most maps emit; the portal must swap. `Circle` uses lat/lng/radius. |
| DELETE | `/api/v1/merchants/me/service-areas/{areaId:guid}` | bearer + Merchant | — | `MerchantResponse` | Removes the area. `404 Merchants.ServiceArea.NotFound` if it's not this merchant's. |
| PUT | `/api/v1/merchants/me/pickup-location` | bearer + Merchant | `SetPickupLocationRequest { latitude, longitude }` | `MerchantResponse` | Physical location of the store. **Required to submit for approval**, and Orders propagates it to Dispatch in `CourierRequested`. |
| POST | `/api/v1/merchants/me/logo` | bearer + Merchant | `multipart/form-data` with `file` (≤ 2 MB) | `MerchantResponse` (with `logoObjectKey`) | `DisableAntiforgery`. The portal must `await res.json()` to read back the updated `logoObjectKey`. The presigned URL is fetched via the public `/{id}/logo-url`. |
| POST | `/api/v1/merchants/me/submit-for-approval` | bearer + Merchant + **confirmed email** | — | 204 | Transitions `Draft → Pending`. Requires `email_confirmed=true` in the JWT (the `RequireConfirmedEmail` policy) **and** the merchant must be complete: `Rnc`, ≥ 1 service area, ≥ 1 operating-hours row, and `PickupLocation` set ([Merchant.cs:264](src/Services/Merchants/Rappix.Merchants.Domain/Merchants/Merchant.cs:264)) — `Merchants.Merchant.IncompleteForSubmission` otherwise. |

```ts
type MerchantStatus = "Draft" | "Pending" | "Active" | "Paused" | "Suspended" | "Rejected";
type VerticalType = "Food" | "Pharmacy" | "Grocery" | "Parcel";

type ServiceAreaResponse = {
  id: string;
  type: "Polygon" | "Circle";
  polygon: number[][] | null;          // [[lng, lat], ...] (GeoJSON ring), or null for circles
  centerLatitude: number | null;
  centerLongitude: number | null;
  radiusMeters: number | null;
};

type OperatingHoursResponse = {
  dayOfWeek: "Sunday" | "Monday" | "Tuesday" | "Wednesday" | "Thursday" | "Friday" | "Saturday";
  opensAt: string;                     // "HH:mm"
  closesAt: string;                    // "HH:mm"
};

type MerchantResponse = {
  id: string;
  ownerUserId: string;
  name: string;
  slug: string;
  rnc: string | null;
  description: string | null;
  verticalType: VerticalType;
  status: MerchantStatus;
  commissionPercentage: number;        // e.g. 15.0
  averageRating: number | null;
  totalReviews: number;
  logoObjectKey: string | null;        // MinIO key — fetch presigned URL via /merchants/{id}/logo-url
  createdAtUtc: string;
  serviceAreas: ServiceAreaResponse[];
  operatingHours: OperatingHoursResponse[];
};
```

### A.3 Catalog service — own catalog: categories, items, modifiers, stock, photos

Root group: `/api/v1/catalog/me` with `RequireMerchant`. Every handler resolves the merchant id from `JWT.sub` via `ResolveMerchantIdQuery` before operating ([CatalogOwnerEndpoints.cs](src/Services/Catalog/Rappix.Catalog.Api/Endpoints/CatalogOwnerEndpoints.cs)). **The catalog row only exists after the merchant is `Active`** (created by Catalog's consumer of `MerchantApprovedDomainEvent`) — until then every endpoint here returns `404 Catalog.Catalog.NotFound`.

| Method | Path | Auth | Body / Query | Response | Notes |
|---|---|---|---|---|---|
| GET | `/api/v1/catalog/me/` | bearer + Merchant | — | `CatalogResponse` | The catalog header + ordered categories. **Note the trailing slash** — the route is `/catalog/me/` (with `/`), not `/catalog/me`. |
| POST | `/api/v1/catalog/me/categories` | bearer + Merchant | `CreateCategoryRequest { name, sortOrder }` | `CatalogResponse` (updated) | |
| DELETE | `/api/v1/catalog/me/categories/{categoryId:guid}` | bearer + Merchant | — | `CatalogResponse` (updated) | Fails with `Catalog.Category.HasItems` if the category still has items. |
| POST | `/api/v1/catalog/me/items` | bearer + Merchant | `CreateItemRequest { categoryId?, name, description?, priceAmount, currency?, tracksInventory, initialStock, attributes? }` | `ItemResponse` | `attributes` is a free `{ string: string }` map for vertical-specific fields (mapped to JSONB per-vertical on the server). `tracksInventory=true` + `initialStock>0` seeds the stock level. |
| GET | `/api/v1/catalog/me/items?page=&pageSize=&categoryId=` | bearer + Merchant | query | `ItemResponse[]` | Lists this merchant's items. `pageSize` clamped to 1..100. |
| GET | `/api/v1/catalog/me/items/{itemId:guid}` | bearer + Merchant | — | `ItemResponse` | `404 Catalog.Item.NotFound` if not owned by the caller's merchant. |
| PUT | `/api/v1/catalog/me/items/{itemId:guid}` | bearer + Merchant | `UpdateItemRequest { categoryId?, name, description?, priceAmount, currency? }` | `ItemResponse` | Optimistic concurrency via `xmin` — the handler retries internally; the client doesn't need to send a version. |
| DELETE | `/api/v1/catalog/me/items/{itemId:guid}` | bearer + Merchant | — | 204 | Soft delete in the aggregate; not visible to public search afterwards. |
| PUT | `/api/v1/catalog/me/items/{itemId:guid}/availability` | bearer + Merchant | `SetAvailabilityRequest { available: boolean }` | `ItemResponse` | Toggle "publish/hide" without changing the row. |
| PUT | `/api/v1/catalog/me/items/{itemId:guid}/attributes` | bearer + Merchant | `SetItemAttributesRequest { attributes: { [k]: string } }` | `ItemResponse` | Replaces the JSONB attributes map. Useful to fix typos / add missing keys without touching name/price. |
| POST | `/api/v1/catalog/me/items/{itemId:guid}/modifiers` | bearer + Merchant | `AddModifierRequest { name, isRequired, minSelections, maxSelections }` | `ItemResponse` (with new modifier group) | |
| POST | `/api/v1/catalog/me/items/{itemId:guid}/modifiers/{modifierId:guid}/options` | bearer + Merchant | `AddModifierOptionRequest { name, priceDelta }` | `ItemResponse` (with new option) | `priceDelta` is added at checkout (can be 0, negative, positive). |
| PUT | `/api/v1/catalog/me/items/{itemId:guid}/stock` | bearer + Merchant | `AdjustStockRequest { mode: "Set"\|"Restock", quantity }` | `StockResponse { itemId, quantity, isDepleted }` | `Set` is absolute, `Restock` adds. Returns `400` if `mode` not parseable. |
| POST | `/api/v1/catalog/me/items/{itemId:guid}/photo` | bearer + Merchant | `multipart/form-data` with `file` (≤ 4 MB) | `ItemResponse` (with `photoObjectKey`) | `DisableAntiforgery`. Presigned URL is fetched via the public `/items/{itemId}/photo-url`. |

```ts
type CategoryResponse = { id: string; name: string; sortOrder: number };

type CatalogResponse = {
  id: string;
  merchantId: string;
  verticalType: VerticalType;
  isEnabled: boolean;
  categories: CategoryResponse[];      // sorted by sortOrder, then name
};

type ModifierOptionResponse = { id: string; name: string; priceDelta: number };
type ModifierResponse = {
  id: string; name: string;
  isRequired: boolean;
  minSelections: number; maxSelections: number;
  options: ModifierOptionResponse[];
};

type ItemResponse = {
  id: string;
  merchantId: string;
  categoryId: string | null;
  name: string;
  description: string | null;
  priceAmount: number;
  currency: string;                    // "DOP"
  isAvailable: boolean;
  tracksInventory: boolean;
  photoObjectKey: string | null;
  attributes: { [key: string]: string };
  modifiers: ModifierResponse[];
  createdAtUtc: string;
};

type StockResponse = { itemId: string; quantity: number; isDepleted: boolean };
```

### A.4 Orders service — incoming orders, accept/reject

Root group: `/api/v1/orders` with `RequireMerchant`. Defined in [MerchantOrderEndpoints.cs](src/Services/Orders/Rappix.Orders.Api/Endpoints/MerchantOrderEndpoints.cs). The merchant identity is `JWT.sub` — Orders stores `MerchantOwnerUserId` on every `Order` and compares directly.

| Method | Path | Auth | Body / Query | Response | Notes |
|---|---|---|---|---|---|
| GET | `/api/v1/orders/merchant/pending?page=&pageSize=` | bearer + Merchant | query | `OrderResponse[]` | Only orders in status `AwaitingMerchant` for this merchant. `pageSize` clamped to 1..100. Internally calls `ListByMerchantOwnerAndStatusAsync(JWT.sub, AwaitingMerchant, ...)` ([ListMerchantPendingQueryHandler.cs:18](src/Services/Orders/Rappix.Orders.Application/Orders/MerchantPending/ListMerchantPendingQueryHandler.cs:18)). |
| POST | `/api/v1/orders/{orderId:guid}/accept` | bearer + Merchant | — | 204 | Publishes `MerchantAccepted` on the bus. The saga unschedules the `MerchantTimeout` and moves on to charge the payment. Returns `403 Orders.Order.NotForMerchant` if the order doesn't belong to this merchant; `409 Orders.Order.InvalidState` if it's already terminal ([AcceptOrderCommandHandler.cs:26](src/Services/Orders/Rappix.Orders.Application/Orders/Accept/AcceptOrderCommandHandler.cs:26)). |
| POST | `/api/v1/orders/{orderId:guid}/reject` | bearer + Merchant | `RejectOrderRequest? { reason? }` (optional body) | 204 | Publishes `MerchantRejected` with the reason (defaults to `"Rechazado por el merchant"`). The saga compensates (returns the stock reservation, voids the quote consumption) and cancels the order. Same ownership and terminal checks as `accept`. |

> ⚠️ **There is no `mark-prepared`, no `mark-ready-for-pickup`, no `get-order-by-id` for merchants, and no list-by-other-status endpoint.** Once the merchant accepts, the saga drives the rest (payment → courier → delivery). See section D for the resulting UI consequences.

```ts
type OrderStatus =
  | "Submitted"          // bus saga validating quote + reserving stock
  | "AwaitingMerchant"   // ← the only state the merchant sees in /merchant/pending
  | "AwaitingPayment"
  | "AwaitingCourier"
  | "InProgress"
  | "Completed"          // terminal
  | "Cancelled"          // terminal
  | "Failed"             // terminal
  | "NeedsReview";       // terminal (manual intervention)

type OrderLineResponse = {
  itemId: string; itemName: string;
  unitPrice: number; modifierTotal: number;
  quantity: number; lineSubtotal: number;
};

type DeliveryAddressResponse = {
  street: string;
  reference: string | null;
  latitude: number; longitude: number;
};

type OrderResponse = {
  orderId: string;
  customerUserId: string;
  merchantId: string;
  merchantOwnerUserId: string;
  quoteId: string;
  vertical: VerticalType;
  currency: string;
  status: OrderStatus;
  lines: OrderLineResponse[];
  subtotal: number; deliveryFee: number; serviceFee: number;
  tax: number; tip: number; discountAmount: number; totalAmount: number;
  deliveryAddress: DeliveryAddressResponse;
  cancellationReason: string | null;
  createdAtUtc: string;
  acceptedAtUtc: string | null;
  inProgressAtUtc: string | null;
  completedAtUtc: string | null;
  closedAtUtc: string | null;
};
```

---

## B. Public / shared routes the merchant uses

### B.1 Identity (port 5001) — login, register, refresh, profile

All shared with Customers, Couriers, Admins. The merchant portal uses these as-is.

| Method | Path | Auth | Notes |
|---|---|---|---|
| POST | `/api/v1/auth/register` | anon | `{ email, phoneNumber?, password, firstName, lastName, accountType }` → `UserResponse`. **For the merchant portal pass `accountType: "Merchant"`** — this triggers Merchants to create the `Draft` row (idempotent, via the `UserRegisteredIntegrationEvent` consumer). |
| POST | `/api/v1/auth/login` | anon | `{ identifier, password }` → `AuthResponse`. The merchant portal can hard-fail if `user.userType !== "Merchant"` to prevent customers/couriers from logging in here. |
| POST | `/api/v1/auth/refresh` | anon | `{ refreshToken }` → new `AuthResponse`. Rotates both tokens. On `Identity.Token.ReuseDetected` all sessions are revoked → force re-login. |
| POST | `/api/v1/auth/logout` | anon | `{ refreshToken }` → 204. |
| GET | `/api/v1/auth/confirm-email?userId=&token=` | anon | HTML response. Used by the email-confirmation link. **The merchant must confirm email before `submit-for-approval` works.** |
| POST | `/api/v1/auth/resend-confirmation` | anon | `{ email }` → 204. Always succeeds (no enumeration). |
| GET | `/api/v1/auth/google` | anon | Browser challenge → 302 to Google. After callback, redirects with the Rappix JWT cookies cleared and the body returned via the callback handler. For the merchant portal this is **only useful if you let merchants sign up with Google** — but the resulting user would be `Customer` (the default `accountType`). For merchants, prefer email/password and pass `accountType: "Merchant"` explicitly. |
| GET | `/api/v1/auth/me` | bearer | → `UserResponse`. Same shape across roles. |
| PUT | `/api/v1/users/me` | bearer | `{ firstName, lastName, phoneNumber? }` → `UserResponse`. The merchant's *personal* profile (not the business). |
| POST | `/api/v1/users/me/change-password` | bearer | `{ currentPassword, newPassword }` → 204. Revokes all active refresh tokens. |

### B.2 Merchants service — public discovery (read-only)

Useful for the merchant portal to **preview** how its own business looks to customers (e.g. "Preview public page", or to verify a logo URL).

| Method | Path | Auth | Notes |
|---|---|---|---|
| GET | `/api/v1/merchants/nearby?lat=&lng=&vertical=&page=&pageSize=` | anon | Active merchants whose service area contains the point. |
| GET | `/api/v1/merchants/{id:guid}` | anon | `PublicMerchantResponse` (no `commissionPercentage`, no `rnc`, no `ownerUserId`). Active merchants only. |
| GET | `/api/v1/merchants/by-slug/{slug}` | anon | Same. |
| GET | `/api/v1/merchants/{id:guid}/logo-url` | anon | Bare JSON string — a 1-hour MinIO presigned URL. The merchant portal **must** call this to render its own logo too (the `logoObjectKey` from `/me` is not a URL). |

### B.3 Catalog service — public search (read-only)

The merchant portal might use these to preview its own listings (the public price page).

| Method | Path | Auth | Notes |
|---|---|---|---|
| GET | `/api/v1/catalog/items/search?q=&merchantId=&categoryId=&page=&pageSize=` | anon | Full-text (Spanish) over items. Pass `merchantId=<this merchant's id>` to preview only your own items. |
| GET | `/api/v1/catalog/items/{itemId:guid}` | anon | `PublicItemResponse` (no stock counters). |
| GET | `/api/v1/catalog/items/{itemId:guid}/photo-url` | anon | Bare JSON string presigned URL (1 h). |

### B.4 Health

| Method | Path | Notes |
|---|---|---|
| GET | `/health` on each service host | Simple JSON. Not exposed through the gateway by default — use only in operator tooling. |

---

## C. Services the merchant does NOT use (verified, do not call)

These services are protected with policies that explicitly exclude `userType=Merchant`:

| Service | Path prefix at gateway | Policy | What it's for | Why the merchant doesn't use it |
|---|---|---|---|---|
| **Pricing** | `/api/v1/pricing/{**}` | `RequireCustomer` ([PricingEndpoints.cs:20](src/Services/Pricing/Rappix.Pricing.Api/Endpoints/PricingEndpoints.cs:20)) | `POST /quotes`, `GET /quotes/{id}` — customer quotes a cart before placing the order. | Merchants don't quote; quotes are part of the *customer* checkout. A merchant calling this gets `403`. |
| **Pricing (admin)** | `/api/v1/admin/pricing/{**}` | `RequireAdmin` | Surge rules + coupon CRUD. | Not the merchant. |
| **Orders (customer)** | `POST /api/v1/orders`, `GET /api/v1/orders/{id}`, `GET /api/v1/orders`, `POST /api/v1/orders/{id}/cancel` | `RequireCustomer` ([OrderEndpoints.cs:22](src/Services/Orders/Rappix.Orders.Api/Endpoints/OrderEndpoints.cs:22)) | Place / list-own / get-own / cancel. | A merchant gets `403`. The customer view also enforces `order.CustomerUserId == JWT.sub` ownership ([GetOrderQueryHandler.cs:22](src/Services/Orders/Rappix.Orders.Application/Orders/Get/GetOrderQueryHandler.cs:22)) — even if the policy were lifted, the merchant's `JWT.sub` would still 404. |
| **Orders (seam)** | `POST /api/v1/orders/{id}/mark-delivered` | `RequireAuthorization()` (any auth) ([OrderSeamEndpoints.cs:17](src/Services/Orders/Rappix.Orders.Api/Endpoints/OrderSeamEndpoints.cs:17)) | TEMPORARY seam for development before Dispatch was real. Marks an order delivered manually so the saga reaches `Completed`. | **Do not use from the merchant portal**, even though technically any authenticated user can call it. The comment in the file labels it "TEMPORAL" and slated for removal. |
| **Dispatch** | `/api/v1/couriers/me/{**}` | `RequireCourier` ([CourierEndpoints.cs:24](src/Services/Dispatch/Rappix.Dispatch.Api/Endpoints/CourierEndpoints.cs:24)) | Courier self-service: online/offline, vehicle, location, current assignment. | Not the merchant. |
| **Tracking** | `/api/v1/tracking/orders/{id}` + `/hubs/tracking` (SignalR) | `RequireAuthorization()` (any auth), **but ownership = `OrderTracking.CustomerUserId`** | Live tracking of an order: REST snapshot + SignalR push. | The ownership check **only** matches the **customer** of the order ([OrderTracking.cs:55](src/Services/Tracking/Rappix.Tracking.Domain/OrderTrackings/OrderTracking.cs:55), [TrackingHub.cs:52](src/Services/Tracking/Rappix.Tracking.Api/Hubs/TrackingHub.cs:52)). A merchant calling `GET /tracking/orders/{id}` for one of their own orders gets a `404` (intentionally indistinguishable from 403). **This is a documented backend gap** — see section D. |
| **Payments** | `/api/v1/payments/{**}` | (no merchant REST endpoints exist) | Only `/payments/webhooks/stripe` (anonymous, Stripe-signed). | Nothing the merchant should call. |
| **Notifications** | (no REST endpoints) | — | Background consumer + projections only. | No client-facing API. |

---

## D. Merchant flows (which endpoint chains drive which UI)

### D.1 Onboarding & approval (Draft → Pending → Active)

```
1. POST /api/v1/auth/register   { ..., accountType: "Merchant" }
                                → 201, UserResponse (no JWT yet)
                                → backend emits UserRegisteredIntegrationEvent
                                → Merchants consumer creates the Draft row (idempotent)

2. POST /api/v1/auth/login      { identifier, password }
                                → 200, AuthResponse (JWT carries userType=Merchant)

3. [user clicks email link] GET /api/v1/auth/confirm-email?userId=&token=
                                → HTML "Email confirmado"
                                → after the next /refresh, JWT carries email_confirmed=true
   POST /api/v1/auth/refresh   { refreshToken }   ← needed so the new JWT has email_confirmed=true

4. GET /api/v1/merchants/me     → MerchantResponse, status="Draft"
                                  (the portal reads `status` to decide what to render)

5. Fill in the wizard:
   PUT  /api/v1/merchants/me                          { name, slug, rnc, description?, verticalType }
   PUT  /api/v1/merchants/me/operating-hours          { hours: [...] }
   POST /api/v1/merchants/me/service-areas            { type, polygon|center+radius }
   PUT  /api/v1/merchants/me/pickup-location          { latitude, longitude }
   POST /api/v1/merchants/me/logo                     (multipart file)
   (any subset of these — but ALL are required to submit)

6. POST /api/v1/merchants/me/submit-for-approval
                                → 204 on success (Draft → Pending)
                                → 422 Merchants.Merchant.IncompleteForSubmission if any required field missing
                                → 403 if email_confirmed=false

7. (Admin approves out-of-band) POST /api/v1/admin/merchants/{id}/approve
                                → backend emits MerchantApprovedDomainEvent
                                → Catalog consumer creates the local catalog row
                                → Notifications sends "merchant approved" to the user

8. The portal re-fetches GET /api/v1/merchants/me, sees status="Active", switches to the operational dashboard.
```

**Status awareness in the UI**:
- `Draft` → onboarding wizard.
- `Pending` → "Esperando aprobación" placeholder, polling `/me` (or wait for a notification).
- `Active` → full dashboard (catalog management + incoming orders).
- `Paused` → catalog read-only, no new orders (the public listing also hides the merchant — backend already enforces this in `/merchants/{id}` returning 404 for non-Active).
- `Suspended` / `Rejected` → dead-end screens. No retry path in the current backend.

### D.2 Managing the catalog (only when `status === "Active"`)

The catalog row only exists in Catalog *after* approval, so the portal must short-circuit and disable the menu screens until `merchant.status === "Active"`. After that:

```
GET    /api/v1/catalog/me/                                    → CatalogResponse (categories + flags)
POST   /api/v1/catalog/me/categories                          → +1 category
DELETE /api/v1/catalog/me/categories/{categoryId}             → -1 category (fails if it still has items)

GET    /api/v1/catalog/me/items?page=&pageSize=&categoryId=   → list
POST   /api/v1/catalog/me/items                               → +1 item
GET    /api/v1/catalog/me/items/{itemId}                      → detail
PUT    /api/v1/catalog/me/items/{itemId}                      → edit
DELETE /api/v1/catalog/me/items/{itemId}                      → soft-delete

PUT    /api/v1/catalog/me/items/{itemId}/availability         → publish/hide toggle
PUT    /api/v1/catalog/me/items/{itemId}/attributes           → replace JSONB attrs
PUT    /api/v1/catalog/me/items/{itemId}/stock                → set or restock

POST   /api/v1/catalog/me/items/{itemId}/modifiers            → +1 modifier group
POST   /api/v1/catalog/me/items/{itemId}/modifiers/{mod}/options → +1 option

POST   /api/v1/catalog/me/items/{itemId}/photo                → multipart, sets photoObjectKey
GET    /api/v1/catalog/items/{itemId}/photo-url               → presigned URL to render
```

### D.3 Attending incoming orders (the core operational loop)

The backend exposes only **three** endpoints for this loop. The shape of the merchant dashboard is **pulled from `/merchant/pending` on a poll cadence** (no SignalR push exists for merchants — see gap below).

```
[poll every ~10s]
  GET  /api/v1/orders/merchant/pending?page=1&pageSize=20
                                → OrderResponse[] in status="AwaitingMerchant"

[merchant taps "Aceptar"]
  POST /api/v1/orders/{orderId}/accept
                                → 204; saga moves on to payment → courier → delivery

[merchant taps "Rechazar"]
  POST /api/v1/orders/{orderId}/reject  { reason? }
                                → 204; saga compensates (stock + quote) and cancels
```

After accept/reject, **the order disappears from `/merchant/pending` on the next refresh.** The portal has **no way** to follow that order through the rest of its lifecycle (see D.5).

### D.4 Account settings (any role)

```
GET    /api/v1/auth/me
PUT    /api/v1/users/me                                 { firstName, lastName, phoneNumber? }
POST   /api/v1/users/me/change-password                 { currentPassword, newPassword }
POST   /api/v1/auth/logout                              { refreshToken }
```

### D.5 Gaps the portal will have to design around

These are **real limitations in the current backend** as of phase 13. The merchant portal cannot:

1. **See past orders.** No endpoint lists orders in any state other than `AwaitingMerchant`. Once accepted/rejected, an order is invisible to the merchant. *Workaround*: keep a client-side cache of recently seen orders for the current session; persist nothing across reloads.
2. **See a single order's detail.** `GET /api/v1/orders/{id}` is `RequireCustomer` and the handler's ownership check (`CustomerUserId == JWT.sub`) would 404 a merchant even if the policy were relaxed. *Workaround*: pull all needed fields from the `/merchant/pending` list response (`OrderResponse` already includes lines, totals, address — no detail trip needed).
3. **Track a live order in progress.** Tracking only authorizes the `CustomerUserId`. A merchant cannot subscribe to `/hubs/tracking` for one of their orders. *Workaround*: there is no client-side workaround — the merchant portal simply cannot show live courier movement for now. (Backend gap to file: add merchant ownership to `OrderTracking`.)
4. **Mark an order as "prepared / ready for pickup".** That domain event does not exist; the saga progression is automatic (Accept → AwaitingPayment, then the payment-captured event flips it to AwaitingCourier, then InProgress on courier-claimed, then delivery completes it). *Workaround*: the dashboard treats "Aceptar" as "I will prepare this and let the system handle the rest". UX message accordingly.
5. **Receive real-time push of new pending orders.** No merchant SignalR hub exists; only `/hubs/tracking` (per-order, customer-only). *Workaround*: poll `/merchant/pending` every 5–10 s while the merchant is online; show a small badge with the count.
6. **Bulk publish/hide items, or reorder categories.** Each item is toggled individually (`/availability`), and category `sortOrder` can only be set at creation. *Workaround*: do it client-side as a sequence of PUTs and show progress.

---

## E. Ownership & security

Three layers stack on top of each other; the portal can rely on all three.

1. **JWT bearer** — every protected endpoint requires a valid HS256 JWT with `iss=https://localhost:5001`, `aud=rappix`, alive `exp`. `MapInboundClaims=false` and `NameClaimType=sub`, so `ClaimsPrincipal.GetUserId()` reads the `sub` claim verbatim as a `Guid`.

2. **Role policy `RequireMerchant`** — `policy.RequireClaim("userType", "Merchant")`. Enforced on every endpoint in `MerchantEndpoints`, `CatalogOwnerEndpoints`, `MerchantOrderEndpoints`. **A Customer JWT cannot call any of them**, even with a valid signature.

3. **Per-resource ownership** — even within `RequireMerchant`, every handler verifies the resource belongs to the caller. The mechanism differs per service:

   | Service | Mechanism | What is checked |
   |---|---|---|
   | Merchants | `Merchant.OwnerUserId == JWT.sub` | The merchant repo lookup is by owner (`GetByOwnerAsync`). There is no `merchantId` in the URL; everything is `/me`, scoped by the JWT. |
   | Catalog | `ResolveMerchantIdQuery(JWT.sub) → Result<merchantId>`, then every command/query carries `merchantId` | The Catalog projection stores `(merchantId, ownerUserId)`; resolution happens at the start of each handler. If the user has no catalog yet (merchant not yet `Active`), all endpoints 404. |
   | Orders | `order.MerchantOwnerUserId == JWT.sub` | Persisted on the `Order` aggregate at creation time (via gRPC `MerchantValidationService` against Merchants). Both `Accept` and `Reject` compare directly and return `Orders.Order.NotForMerchant` (403) on mismatch. The list endpoint filters by the same column at the SQL level. |

4. **State guards** — beyond ownership:
   - **Merchant must be `Active`** for Catalog endpoints to find a catalog (the Catalog row is only created after `MerchantApprovedDomainEvent`). The 404 is the gate.
   - **`submit-for-approval` requires `email_confirmed=true`** via the `RequireConfirmedEmail` policy (asserts the `email_confirmed` claim is the literal `"True"` and parseable as `true`) — see [Merchants.Api/Program.cs:79-82](src/Services/Merchants/Rappix.Merchants.Api/Program.cs:79).
   - **`accept` / `reject` reject terminal orders** (`OrderErrors.InvalidState`, 409) — the merchant can't act on an order that has already been cancelled / completed.

5. **What the portal does NOT need to send**:
   - No `merchantId` in URLs (resolved from JWT).
   - No idempotency key on GETs (only on POST/PUT/DELETE — the gateway already enforces `IdempotencyMiddleware` on writes).
   - No CSRF token (no cookies — JWT in `Authorization: Bearer` header, refresh token in body).

---

## F. Summary

- **17 merchant-exclusive REST endpoints** across 3 services:
  - **Merchants (8)**: `/merchants/me` (GET, PUT), `/me/operating-hours` (PUT), `/me/service-areas` (POST, DELETE/{id}), `/me/pickup-location` (PUT), `/me/logo` (POST), `/me/submit-for-approval` (POST).
  - **Catalog (13)**: `/catalog/me/` (GET), `/me/categories` (POST, DELETE/{id}), `/me/items` (GET, POST), `/me/items/{id}` (GET, PUT, DELETE), `/me/items/{id}/availability` (PUT), `/me/items/{id}/attributes` (PUT), `/me/items/{id}/modifiers` (POST), `/me/items/{id}/modifiers/{m}/options` (POST), `/me/items/{id}/stock` (PUT), `/me/items/{id}/photo` (POST).
  - **Orders (3)**: `/orders/merchant/pending` (GET), `/orders/{id}/accept` (POST), `/orders/{id}/reject` (POST).
- **Public / shared endpoints used by the merchant portal**: Identity (`/auth/register|login|refresh|logout|confirm-email|resend-confirmation|me`, `/users/me`, `/users/me/change-password`), Merchants public (`/merchants/nearby|/{id}|/by-slug/{slug}|/{id}/logo-url`) and Catalog public (`/catalog/items/search|/{id}|/{id}/photo-url`) — useful for previewing the public storefront.
- **Pricing, Dispatch, Tracking, Payments, Notifications**: not callable from the merchant portal. Tracking is the most painful absence (no live-order view) and is a documented backend gap.
- **Three operational flows**: (1) onboarding (`register → login → confirm-email → fill profile → submit-for-approval → admin approves`), (2) catalog management (only after `status="Active"`), (3) incoming-orders loop (`poll /merchant/pending → accept | reject`). After accept/reject the saga runs unattended; the merchant has no further actions.
- **Ownership**: three stacked layers — bearer JWT, `RequireMerchant` policy, per-resource check (`OwnerUserId`/resolved `merchantId`/`MerchantOwnerUserId`). The portal never sends a `merchantId` in URLs — everything is `/me`.
