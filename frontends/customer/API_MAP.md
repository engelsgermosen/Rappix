# Rappix API Map — Customer portal

> Built from reading `/src` directly (no guessing). All paths below are reached **only** through the API Gateway at `NEXT_PUBLIC_API_URL` (default `http://localhost:5000`). YARP preserves paths — the gateway URL is the only base the frontend uses.
>
> JSON conventions across services:
> - Money is `decimal` rounded to 2 places, sent as a plain JSON number (not cents). The currency field accompanies it (default `"DOP"`).
> - Enums are serialized as PascalCase strings (`"Food"`, `"Submitted"`, `"Active"`).
> - Strongly-typed Ids unwrap to bare GUID strings on the wire — never `{ value: "..." }`.
> - Errors are RFC 7807 ProblemDetails: `{ status, title, detail, type: "https://rappix.dev/errors/<Code>", code }`. Switch on `code`, not `detail`.

---

## Identity (port 5001)

- Anonymous: `/api/v1/auth/{register,login,refresh,logout,resend-confirmation,confirm-email,google,google/callback}`
- Authenticated: `/api/v1/auth/me`, `/api/v1/users/me`, `/api/v1/users/me/change-password`

| Method | Path | Auth | Notes |
|---|---|---|---|
| POST | `/api/v1/auth/register` | anon | `{ email, phoneNumber?, password, firstName, lastName, accountType? }` → `UserResponse` (no JWT, register-only). Conflicts: `Identity.User.EmailInUse`, `Identity.User.PhoneInUse`. |
| POST | `/api/v1/auth/login` | anon | `{ identifier, password }` (identifier = email or E.164 phone) → `AuthResponse { accessToken, accessTokenExpiresAtUtc, refreshToken, user }`. `401 Identity.User.InvalidCredentials`, `403 Identity.User.Inactive`. |
| POST | `/api/v1/auth/refresh` | anon | `{ refreshToken }` → new `AuthResponse` (rotates both). On `Identity.Token.ReuseDetected` all sessions are revoked. |
| POST | `/api/v1/auth/logout` | anon | `{ refreshToken }` → 204. Always succeeds. |
| GET  | `/api/v1/auth/me` | bearer | → `UserResponse`. |
| PUT  | `/api/v1/users/me` | bearer | `{ firstName, lastName, phoneNumber? }` → `UserResponse`. |
| POST | `/api/v1/users/me/change-password` | bearer | `{ currentPassword, newPassword }` → 204. Revokes all active refresh tokens. |

**JWT**: HS256, `iss=https://localhost:5001`, `aud=rappix`, claims `sub` (Guid string), `email`, `userType` (`Customer|Merchant|Courier|Admin`), `email_confirmed`, `jti`, `iat/nbf/exp`. **Access token: 15 min.** **Refresh token: 90 days, opaque base64url.** No cookies — body-only transport.

```ts
type UserResponse = {
  id: string; email: string; phoneNumber: string | null;
  firstName: string; lastName: string;
  userType: "Customer" | "Merchant" | "Courier" | "Admin";
  emailConfirmed: boolean; phoneConfirmed: boolean;
  createdAtUtc: string;
};
type AuthResponse = {
  accessToken: string; accessTokenExpiresAtUtc: string;
  refreshToken: string; user: UserResponse;
};
```

---

## Merchants (port 5002)

All public. There is **no** `/featured`, `/search`, `/categories`, `/{id}/menu`, `/{id}/products` here — catalog lives in the Catalog service.

| Method | Path | Auth | Notes |
|---|---|---|---|
| GET | `/api/v1/merchants/nearby?lat=&lng=&vertical=&page=&pageSize=` | anon | Returns merchants whose service area contains the point. **No `radiusKm`** param — each merchant owns its area. `vertical ∈ {Food, Pharmacy, Grocery, Parcel}` (case-insensitive). |
| GET | `/api/v1/merchants/{id:guid}` | anon | Active merchants only; 404 otherwise. |
| GET | `/api/v1/merchants/by-slug/{slug}` | anon | Same shape as `/{id}`. Bad slug returns 404 (not 400). |
| GET | `/api/v1/merchants/{id:guid}/logo-url` | anon | Bare JSON string (a 1-hour MinIO presigned URL). Body looks like `"http://minio:9000/..."` — `await res.text()` and strip quotes, or `await res.json()`. |

```ts
type NearbyMerchant = {
  id: string; name: string; slug: string;
  verticalType: "Food" | "Pharmacy" | "Grocery" | "Parcel";
  averageRating: number | null; totalReviews: number;
};
type PublicMerchant = NearbyMerchant & { description: string | null };
```

**Gaps** (the public response does NOT expose): pickup `lat/lng`, distance, delivery fee, ETA, `isOpen`, operating hours, cover image. Cover image does not exist anywhere in this service — only logo.

---

## Catalog (port 5003)

| Method | Path | Auth | Notes |
|---|---|---|---|
| GET | `/api/v1/catalog/items/search?q=&merchantId=&categoryId=&page=&pageSize=` | anon | Full-text Spanish over `search_vector` (GIN). Visibility = `IsAvailable && MerchantCatalog.IsEnabled`. Sort = createdAt DESC. |
| GET | `/api/v1/catalog/items/{itemId:guid}` | anon | `PublicItemResponse`. |
| GET | `/api/v1/catalog/items/{itemId:guid}/photo-url` | anon | Bare JSON string (1-hour MinIO presigned URL). |

Owner-only endpoints under `/api/v1/catalog/me/**` are out of scope (merchant portal).

```ts
type PublicItem = {
  id: string; merchantId: string; categoryId: string | null;
  name: string; description: string | null;
  priceAmount: number; currency: string;     // decimal, "DOP"
  photoObjectKey: string | null;             // NOT a URL — resolve via /photo-url
  attributes: Record<string, string>;        // vertical-specific (see below)
  modifiers: { id: string; name: string; isRequired: boolean; minSelections: number; maxSelections: number;
               options: { id: string; name: string; priceDelta: number }[] }[];
};
type Paged<T> = { items: T[]; page: number; pageSize: number; totalCount: number; totalPages: number; hasPrevious: boolean; hasNext: boolean };
```

**Vertical attributes** (validated server-side, echoed back as strings):
- `Food`: `spicyLevel`, `calories`, `isVegetarian`, `allergens`, `portionSize`
- `Pharmacy`: `requiresPrescription` (required), `activeIngredient`, `dosage`, `laboratory`
- `Grocery`: `brand`, `unit`, `barcode`, `weightGrams`
- `Parcel`: `maxWeightKg`, `dimensions`, `fragile`

**Categories**: no public endpoint exposes category names. The customer FE groups items by `categoryId` client-side; section titles fall back to `"Sección N"` when names are unknown.

---

## Pricing (port 5004)

Customer-only. Two endpoints. **No coupon validation endpoint** — apply the code as part of `POST /quotes`; bad codes return `409 Pricing.Coupon.*`.

| Method | Path | Auth | Notes |
|---|---|---|---|
| POST | `/api/v1/pricing/quotes` | bearer (`userType=Customer`) | Optional `Idempotency-Key`. `201 QuoteResponse` + `Location` header. |
| GET  | `/api/v1/pricing/quotes/{quoteId:guid}` | bearer | Lazy expiration — past TTL flips status to Expired and returns `409 Pricing.Quote.Expired`. |

Request body (frontend-supplied):

```ts
type CreateQuote = {
  merchantId: string;
  vertical: "Food" | "Pharmacy" | "Grocery" | "Parcel";
  distanceKm: number;          // FE-computed; merchants does NOT expose pickup coords
  zoneId?: string | null;      // optional surge bucket
  tip: number;                 // decimal >= 0
  couponCode?: string | null;  // validated server-side
  isFirstOrder: boolean;
  lines: { itemId: string; quantity: number; modifierTotal: number /* per UNIT */ }[];
};
type QuoteResponse = {
  quoteId: string; customerUserId: string; merchantId: string; vertical: string; currency: string;
  lines: { itemId: string; itemName: string; unitPrice: number; modifierTotal: number; quantity: number; lineSubtotal: number }[];
  breakdown: { subtotal: number; surgeMultiplier: number; surgeAmount: number; discountAmount: number;
               deliveryFee: number; serviceFee: number; tax: number; tip: number; total: number };
  couponCode: string | null; status: "Active" | "Consumed" | "Expired";
  createdAtUtc: string; expiresAtUtc: string; consumedAtUtc: string | null;
};
```

**TTL**: `Pricing:QuoteExpiryMinutes = 10` by default. Show a countdown using `expiresAtUtc` from the response.

Conflict codes to surface: `Pricing.Quote.MerchantInactive`, `Pricing.QuoteLine.ItemNotPurchasable`, `Pricing.QuoteLine.PriceUnavailable`, `Pricing.Coupon.{NotFound,Inactive,Expired,NotYetValid,MaxUsesReached,MinOrderNotMet,PerUserLimitReached,MerchantMismatch}`.

---

## Orders (port 5005)

Customer-only.

| Method | Path | Auth | Notes |
|---|---|---|---|
| POST | `/api/v1/orders` | bearer (`Customer`) | **`Idempotency-Key` recommended.** Body: `{ quoteId, street, reference?, latitude, longitude }`. No `paymentMethodId`. Returns `201 OrderResponse` + `Location`. |
| GET  | `/api/v1/orders/{orderId:guid}` | bearer | `OrderResponse`. 403 = `Orders.Order.NotOwnedByCustomer`. |
| GET  | `/api/v1/orders?page=&pageSize=` | bearer | **Plain array** (no pagination envelope). `pageSize` clamps to `[1, 100]`. |
| POST | `/api/v1/orders/{orderId:guid}/cancel` | bearer | Optional `{ reason }`. 204. Conflict `Orders.Order.NotCancellable`. Status change is eventual via the saga. |

```ts
type OrderStatus = "Submitted" | "AwaitingMerchant" | "AwaitingPayment" | "AwaitingCourier"
                 | "InProgress" | "Completed" | "Cancelled" | "Failed" | "NeedsReview";

type OrderResponse = {
  orderId: string; customerUserId: string; merchantId: string; merchantOwnerUserId: string;
  quoteId: string; vertical: string; currency: string; status: OrderStatus;
  lines: { itemId: string; itemName: string; unitPrice: number; modifierTotal: number; quantity: number; lineSubtotal: number }[];
  subtotal: number; deliveryFee: number; serviceFee: number; tax: number; tip: number;
  discountAmount: number; totalAmount: number;
  deliveryAddress: { street: string; reference: string | null; latitude: number; longitude: number };
  cancellationReason: string | null;
  createdAtUtc: string; acceptedAtUtc: string | null; inProgressAtUtc: string | null;
  completedAtUtc: string | null; closedAtUtc: string | null;
};
```

UI gating:
- Show **Cancelar** when status ∈ `{Submitted, AwaitingMerchant, AwaitingPayment, AwaitingCourier}`.
- Stop tracking (close hub, stop polling) when status ∈ `{Completed, Cancelled, Failed, NeedsReview}`.
- Timeline = `createdAtUtc → acceptedAtUtc → inProgressAtUtc → completedAtUtc/closedAtUtc`.

`OrderResponse` does **NOT** include any courier info (no `courierId`, no name, no vehicle). That comes from Tracking.

---

## Payments (port 5008) — **Phase 8: event-driven, NO REST for customers**

The only HTTP endpoints are `POST /payments/webhooks/stripe` (Stripe → backend, informational only) and `GET /health`. There is **no** `POST /api/v1/payments/intents`, **no** `client_secret` exposure, **no** `GET /payments/{id}`.

Flow Phase 8:
1. Frontend `POST /api/v1/orders` with `quoteId` + `Idempotency-Key`.
2. Orders saga emits `PaymentRequestedIntegrationEvent` on the bus.
3. `PaymentRequestedConsumer` calls `IPaymentGateway.AuthorizeAsync` server-side. Default is the **Fake** gateway (always succeeds in dev); `Payments:Gateway=Stripe` uses Stripe with hardcoded `pm_card_visa`.
4. Saga moves to `AwaitingCourier` / etc.; the customer observes via Tracking SignalR + `GET /api/v1/orders/{id}`.

The customer FE renders a **payment-method picker** for UX continuity but does NOT call any Payments endpoint. Stripe Elements + `client_secret` is a Phase 9+ follow-up.

---

## Tracking (port 5007)

### REST

| Method | Path | Auth | Notes |
|---|---|---|---|
| GET | `/api/v1/tracking/orders/{orderId:guid}` | bearer | `OrderTrackingResponse`. **404 and 403 are indistinguishable** by design (`Tracking.OrderTracking.NotFound`). |

```ts
type TrackingStatus = "Placed" | "MerchantAccepted" | "CourierAssigned" | "Delivered" | "Cancelled" | "Failed";
type OrderTrackingResponse = {
  orderId: string; status: TrackingStatus; statusReason: string | null;
  lastLocation: { lat: number; lng: number; reportedAtUtc: string } | null;
  courierId: string | null;
  pickup:   { lat: number; lng: number };
  delivery: { lat: number; lng: number };
  createdAtUtc: string; updatedAtUtc: string;
};
```

### SignalR hub — `/hubs/tracking`

**JWT goes in `?access_token=`** (browser WS can't send Authorization). The backend extracts it only when the path starts with `/hubs/tracking`. Anonymous policy at the gateway lets the hub itself validate.

Client → Server methods:
- `Subscribe(orderId: Guid)` → validates ownership (`sub == snapshot.CustomerUserId`) and joins group `order:{orderId:N}`. On success, immediately pushes `StatusChanged` (current state) and `LocationUpdated` (if last known) to the caller. Throws `HubException("no autorizado")` on auth failure (same message for missing/unauthorized — no enumeration). Retries the read 3×500ms to cover the projection race.
- `Unsubscribe(orderId: Guid)`.

Server → Client events:
- `LocationUpdated(payload)` — `{ latitude, longitude, reportedAtUtc, courierId }`.
- `StatusChanged(payload)` — `{ status, changedAtUtc, reason | null }`.

Frontend should let SignalR negotiate transports (WebSockets first, fallback to SSE/LongPolling). In Phase 7 there is no Redis backplane (single instance).

---

## Dispatch (port 5006)

Every `/api/v1/couriers/**` route is gated by `RequireCourier` — **the customer cannot call any of them**. Tracking's response carries only `courierId` (Guid). The "Marco P. ⭐4.9 • Moto • Placa K42-189" string from the mockup cannot be sourced today; the FE renders a courier id placeholder and notes this as a follow-up.

---

## Follow-ups for the backend (do NOT touch in Phase 11)

1. **Pricing needs lat/lng or merchant pickup coords** — today the FE sends a hardcoded `distanceKm` (env). Add either `deliveryAddress` to `CreateQuote` or expose pickup coords on `/api/v1/merchants/{id}`.
2. **Public courier snapshot** — add `GET /api/v1/couriers/{id}/public` returning `{ firstName, vehicle:{type,plate} }`, OR denormalize courier into `OrderTrackingResponse`.
3. **Public catalog categories** — expose `GET /api/v1/catalog/merchants/{id}/categories` so the store-detail page can label sections.
4. **Payments REST** — `POST /api/v1/payments/intents` returning `clientSecret` + remove hardcoded `pm_card_visa` so the FE can ship real Stripe Elements + 3DS.
5. **CORS** — gateway allows `http://localhost:3000` already (`appsettings.json:Cors.AllowedOrigins`). If the FE moves ports, update there.
6. **Orders list pagination envelope** — `GET /api/v1/orders` returns a bare array with no totals; consider `PagedResult<T>` for consistency.
7. **Merchant operating hours / cover image** — none of the public merchant responses expose them; the "Cerrado" badge in the mockup is rendered randomly until a backend signal lands.
