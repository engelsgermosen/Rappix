/**
 * TypeScript mirrors of the backend DTOs the merchant portal consumes.
 * Names match the C# records on the wire (camelCase keys via System.Text.Json).
 *
 * Only the slice used by /frontends/merchant is included — the customer portal
 * has the full surface (orders place, pricing quotes, tracking, etc.).
 */

// -------- RFC 7807 ------------------------------------------------------

export type ProblemDetails = {
  type?: string;
  title?: string;
  status: number;
  detail?: string;
  code?: string;
  [key: string]: unknown;
};

// -------- Pagination ----------------------------------------------------

/**
 * Espejo de `Rappix.BuildingBlocks.Core.Pagination.PagedResult<T>` — el backend
 * SIEMPRE envuelve listas paginadas en este shape (NO devuelve arrays desnudos).
 * Histórico: clientes que tipaban `Promise<T[]>` (mintiendo) provocaban runtime
 * crashes al hacer `.filter/.map/.length` sobre el wrapper. Se corrigió y ahora
 * cada caller desempaca explicitamente `result.items`.
 */
export type PagedResult<T> = {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  hasPrevious: boolean;
  hasNext: boolean;
};

// -------- Identity ------------------------------------------------------

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

export type LoginRequest = { identifier: string; password: string };
export type RefreshRequest = { refreshToken: string };
export type LogoutRequest = { refreshToken: string };

export type RegisterRequest = {
  email: string;
  phoneNumber?: string | null;
  password: string;
  firstName: string;
  lastName: string;
  accountType?: AccountType;
};

export type UpdateProfileRequest = {
  firstName: string;
  lastName: string;
  phoneNumber?: string | null;
};

export type ChangePasswordRequest = {
  currentPassword: string;
  newPassword: string;
};

// -------- Merchants ----------------------------------------------------

export type VerticalType = "Food" | "Pharmacy" | "Grocery" | "Parcel";

export type MerchantStatus =
  | "Draft"
  | "Pending"
  | "Active"
  | "Paused"
  | "Suspended"
  | "Rejected";

export type DayOfWeek =
  | "Sunday"
  | "Monday"
  | "Tuesday"
  | "Wednesday"
  | "Thursday"
  | "Friday"
  | "Saturday";

export type ServiceAreaType = "Polygon" | "Circle";

export type ServiceAreaResponse = {
  id: string;
  type: ServiceAreaType;
  polygon: number[][] | null; // [[lng, lat], ...] (GeoJSON order)
  centerLatitude: number | null;
  centerLongitude: number | null;
  radiusMeters: number | null;
};

export type OperatingHoursResponse = {
  dayOfWeek: DayOfWeek;
  opensAt: string;  // "HH:mm"
  closesAt: string; // "HH:mm"
};

export type MerchantResponse = {
  id: string;
  ownerUserId: string;
  name: string;
  slug: string;
  rnc: string | null;
  description: string | null;
  verticalType: VerticalType;
  status: MerchantStatus;
  commissionPercentage: number;
  averageRating: number | null;
  totalReviews: number;
  logoObjectKey: string | null;
  createdAtUtc: string;
  serviceAreas: ServiceAreaResponse[];
  operatingHours: OperatingHoursResponse[];
};

export type UpdateMerchantProfileRequest = {
  name: string;
  slug: string;
  rnc?: string | null;
  description?: string | null;
  verticalType: VerticalType;
};

export type OperatingHoursRangeRequest = {
  dayOfWeek: DayOfWeek;
  opensAt: string;
  closesAt: string;
};

export type UpdateOperatingHoursRequest = {
  hours: OperatingHoursRangeRequest[];
};

export type AddServiceAreaRequest =
  | { type: "Polygon"; polygon: number[][]; centerLatitude?: null; centerLongitude?: null; radiusMeters?: null }
  | { type: "Circle"; polygon?: null; centerLatitude: number; centerLongitude: number; radiusMeters: number };

export type SetPickupLocationRequest = {
  latitude: number;
  longitude: number;
};

// -------- Catalog ------------------------------------------------------

export type CategoryResponse = {
  id: string;
  name: string;
  sortOrder: number;
};

export type CatalogResponse = {
  id: string;
  merchantId: string;
  verticalType: VerticalType;
  isEnabled: boolean;
  categories: CategoryResponse[];
};

export type ModifierOptionResponse = {
  id: string;
  name: string;
  priceDelta: number;
};

export type ModifierResponse = {
  id: string;
  name: string;
  isRequired: boolean;
  minSelections: number;
  maxSelections: number;
  options: ModifierOptionResponse[];
};

export type ItemResponse = {
  id: string;
  merchantId: string;
  categoryId: string | null;
  name: string;
  description: string | null;
  priceAmount: number;
  currency: string;
  isAvailable: boolean;
  tracksInventory: boolean;
  photoObjectKey: string | null;
  attributes: Record<string, string>;
  modifiers: ModifierResponse[];
  createdAtUtc: string;
};

export type StockResponse = {
  itemId: string;
  quantity: number;
  isDepleted: boolean;
};

export type CreateCategoryRequest = { name: string; sortOrder: number };

export type CreateItemRequest = {
  categoryId?: string | null;
  name: string;
  description?: string | null;
  priceAmount: number;
  currency?: string | null;
  tracksInventory: boolean;
  initialStock: number;
  attributes?: Record<string, string> | null;
};

export type UpdateItemRequest = {
  categoryId?: string | null;
  name: string;
  description?: string | null;
  priceAmount: number;
  currency?: string | null;
};

export type SetAvailabilityRequest = { available: boolean };
export type SetItemAttributesRequest = { attributes: Record<string, string> };
export type AddModifierRequest = {
  name: string;
  isRequired: boolean;
  minSelections: number;
  maxSelections: number;
};
export type AddModifierOptionRequest = { name: string; priceDelta: number };
export type AdjustStockRequest = { mode: "Set" | "Restock"; quantity: number };

// -------- Orders (merchant view) ----------------------------------------

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

export type OrderLineResponse = {
  itemId: string;
  itemName: string;
  unitPrice: number;
  modifierTotal: number;
  quantity: number;
  lineSubtotal: number;
};

export type DeliveryAddressResponse = {
  street: string;
  reference: string | null;
  latitude: number;
  longitude: number;
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
  lines: OrderLineResponse[];
  subtotal: number;
  deliveryFee: number;
  serviceFee: number;
  tax: number;
  tip: number;
  discountAmount: number;
  totalAmount: number;
  deliveryAddress: DeliveryAddressResponse;
  cancellationReason: string | null;
  createdAtUtc: string;
  acceptedAtUtc: string | null;
  inProgressAtUtc: string | null;
  completedAtUtc: string | null;
  closedAtUtc: string | null;
};

export type RejectOrderRequest = { reason?: string | null };
