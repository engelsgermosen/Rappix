/**
 * Espejos TypeScript de los DTOs del backend que el portal courier consume.
 * Nombres en camelCase como llegan en el wire (System.Text.Json).
 *
 * Este archivo cubre las shapes compartidas con todos los portales (Identity +
 * RFC 7807). Las shapes courier-especificas (CourierResponse, AssignmentSnapshot,
 * etc.) se anaden en el commit 4 junto al servicio API courier.
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

// -------- Dispatch — Courier --------------------------------------------

export type CourierStatus = "Offline" | "Online" | "Busy";
export type VehicleType = "Moto" | "Bici" | "Carro";

export type VehicleResponse = {
  type: VehicleType;
  plate: string | null;
  capacityKg: number | null;
};

export type LocationResponse = {
  latitude: number;
  longitude: number;
  reportedAtUtc: string;
};

export type CourierResponse = {
  courierId: string;
  firstName: string;
  status: CourierStatus;
  vehicle: VehicleResponse | null;
  lastLocation: LocationResponse | null;
  createdAtUtc: string;
  updatedAtUtc: string | null;
};

export type UpdateVehicleRequest = {
  vehicleType: VehicleType;
  plate?: string | null;
  capacityKg?: number | null;
};

export type ReportLocationRequest = {
  latitude: number;
  longitude: number;
};

/**
 * Asignacion activa enriquecida (Fase 13.6). El endpoint devuelve 204 No
 * Content cuando no hay asignacion — `apiFetch` lo retorna como `undefined`,
 * asi que el caller debe tratar `null` y `undefined` como "no hay".
 */
export type CurrentAssignmentResponse = {
  assignmentId: string;
  orderId: string;
  assignedAtUtc: string;
  customerUserId: string;
  pickup: {
    merchantName: string;
    latitude: number;
    longitude: number;
  };
  delivery: {
    street: string;
    reference: string | null;
    latitude: number;
    longitude: number;
  };
  orderTotal: number;
  orderCurrency: string;
  lines: Array<{ itemName: string; quantity: number }>;
};
