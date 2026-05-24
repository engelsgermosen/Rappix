/**
 * Wire types — mirror C# DTOs from the backend, NOT internal client models.
 * Each type's source-of-truth is documented in /API_MAP.md.
 */

// ────────────────────────────────────────────────────────────────────────────
// Identity
// ────────────────────────────────────────────────────────────────────────────

export type AccountType = "Customer" | "Merchant" | "Courier" | "Admin";

export type UserResponse = {
  id: string;
  email: string;
  phoneNumber: string | null;
  firstName: string;
  lastName: string;
  userType: AccountType;
  emailConfirmed: boolean;
  phoneConfirmed: boolean;
  createdAtUtc: string;
};

export type AuthResponse = {
  accessToken: string;
  accessTokenExpiresAtUtc: string;
  refreshToken: string;
  user: UserResponse;
};

export type RegisterRequest = {
  email: string;
  phoneNumber?: string | null;
  password: string;
  firstName: string;
  lastName: string;
  accountType?: AccountType | null;
};

export type LoginRequest = { identifier: string; password: string };
export type RefreshRequest = { refreshToken: string };
export type UpdateProfileRequest = {
  firstName: string;
  lastName: string;
  phoneNumber?: string | null;
};

// ────────────────────────────────────────────────────────────────────────────
// Merchants
// ────────────────────────────────────────────────────────────────────────────

export type VerticalType = "Food" | "Pharmacy" | "Grocery" | "Parcel";

export type NearbyMerchant = {
  id: string;
  name: string;
  slug: string;
  verticalType: VerticalType;
  averageRating: number | null;
  totalReviews: number;
};

export type PublicMerchant = NearbyMerchant & {
  description: string | null;
};

// ────────────────────────────────────────────────────────────────────────────
// Catalog
// ────────────────────────────────────────────────────────────────────────────

export type ItemModifierOption = {
  id: string;
  name: string;
  priceDelta: number;
};

export type ItemModifier = {
  id: string;
  name: string;
  isRequired: boolean;
  minSelections: number;
  maxSelections: number;
  options: ItemModifierOption[];
};

export type PublicItem = {
  id: string;
  merchantId: string;
  categoryId: string | null;
  name: string;
  description: string | null;
  priceAmount: number;
  currency: string;
  photoObjectKey: string | null;
  attributes: Record<string, string>;
  modifiers: ItemModifier[];
};

export type Paged<T> = {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  hasPrevious: boolean;
  hasNext: boolean;
};

// ────────────────────────────────────────────────────────────────────────────
// Pricing
// ────────────────────────────────────────────────────────────────────────────

export type CreateQuoteRequest = {
  merchantId: string;
  vertical: VerticalType;
  distanceKm: number;
  zoneId?: string | null;
  tip: number;
  couponCode?: string | null;
  isFirstOrder: boolean;
  lines: { itemId: string; quantity: number; modifierTotal: number }[];
};

export type QuoteLine = {
  itemId: string;
  itemName: string;
  unitPrice: number;
  modifierTotal: number;
  quantity: number;
  lineSubtotal: number;
};

export type QuoteBreakdown = {
  subtotal: number;
  surgeMultiplier: number;
  surgeAmount: number;
  discountAmount: number;
  deliveryFee: number;
  serviceFee: number;
  tax: number;
  tip: number;
  total: number;
};

export type QuoteResponse = {
  quoteId: string;
  customerUserId: string;
  merchantId: string;
  vertical: VerticalType;
  currency: string;
  lines: QuoteLine[];
  breakdown: QuoteBreakdown;
  couponCode: string | null;
  status: "Active" | "Consumed" | "Expired";
  createdAtUtc: string;
  expiresAtUtc: string;
  consumedAtUtc: string | null;
};

// ────────────────────────────────────────────────────────────────────────────
// Orders
// ────────────────────────────────────────────────────────────────────────────

export type OrderStatus =
  | "Submitted"
  | "AwaitingMerchant"
  | "AwaitingPayment"
  | "AwaitingCourier"
  | "InProgress"
  | "Completed"
  | "Cancelled"
  | "Failed"
  | "NeedsReview";

export type PlaceOrderRequest = {
  quoteId: string;
  street: string;
  reference?: string | null;
  latitude: number;
  longitude: number;
};

export type OrderLine = {
  itemId: string;
  itemName: string;
  unitPrice: number;
  modifierTotal: number;
  quantity: number;
  lineSubtotal: number;
};

export type OrderResponse = {
  orderId: string;
  customerUserId: string;
  merchantId: string;
  merchantOwnerUserId: string;
  quoteId: string;
  vertical: VerticalType;
  currency: string;
  status: OrderStatus;
  lines: OrderLine[];
  subtotal: number;
  deliveryFee: number;
  serviceFee: number;
  tax: number;
  tip: number;
  discountAmount: number;
  totalAmount: number;
  deliveryAddress: {
    street: string;
    reference: string | null;
    latitude: number;
    longitude: number;
  };
  cancellationReason: string | null;
  createdAtUtc: string;
  acceptedAtUtc: string | null;
  inProgressAtUtc: string | null;
  completedAtUtc: string | null;
  closedAtUtc: string | null;
};

// ────────────────────────────────────────────────────────────────────────────
// Tracking
// ────────────────────────────────────────────────────────────────────────────

export type TrackingStatus =
  | "Placed"
  | "MerchantAccepted"
  | "CourierAssigned"
  | "Delivered"
  | "Cancelled"
  | "Failed";

export type OrderTrackingResponse = {
  orderId: string;
  status: TrackingStatus;
  statusReason: string | null;
  lastLocation: { lat: number; lng: number; reportedAtUtc: string } | null;
  courierId: string | null;
  pickup: { lat: number; lng: number };
  delivery: { lat: number; lng: number };
  createdAtUtc: string;
  updatedAtUtc: string;
};

export type LocationUpdatedPayload = {
  latitude: number;
  longitude: number;
  reportedAtUtc: string;
  courierId: string;
};

export type StatusChangedPayload = {
  status: TrackingStatus;
  changedAtUtc: string;
  reason: string | null;
};

// ────────────────────────────────────────────────────────────────────────────
// RFC 7807 ProblemDetails
// ────────────────────────────────────────────────────────────────────────────

export type ProblemDetails = {
  type?: string;
  title?: string;
  status: number;
  detail?: string;
  code?: string;
  [key: string]: unknown;
};
