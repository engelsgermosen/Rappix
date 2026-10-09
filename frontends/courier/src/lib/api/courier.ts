import { newIdempotencyKey } from "@/lib/utils";

import { apiFetch } from "./client";
import type {
  CourierResponse,
  CurrentAssignmentResponse,
  ReportLocationRequest,
  UpdateVehicleRequest,
} from "./types";

/**
 * Los 7 endpoints courier-exclusivos del backend (Dispatch). Verificados contra
 * `src/Services/Dispatch/Rappix.Dispatch.Api/Endpoints/CourierEndpoints.cs` y
 * `frontends/COURIER_API_MAP.md` (seccion A.2 + el cambio de Fase 13.6 que
 * enriquecio current-assignment y agrego /delivered).
 *
 * Ownership: ninguno acepta id en URL — todo es /me, resuelto del JWT.sub.
 * Idempotency-Key: en POST/PUT/DELETE que tienen side effects de bus o BD;
 * NO en /me/location (heartbeats deben poder repetirse, y el backend hace
 * dedup por timestamp en Tracking).
 */
export const courierApi = {
  /** GET /api/v1/couriers/me → CourierResponse (status, vehicle?, lastLocation?). */
  me: () => apiFetch<CourierResponse>("/api/v1/couriers/me"),

  /** PUT /api/v1/couriers/me/vehicle → CourierResponse. */
  updateVehicle: (req: UpdateVehicleRequest) =>
    apiFetch<CourierResponse>("/api/v1/couriers/me/vehicle", {
      method: "PUT",
      json: req,
      idempotencyKey: newIdempotencyKey(),
    }),

  /** POST /api/v1/couriers/me/online → CourierResponse. Errores: 400 VehicleRequired, 409 InvalidTransition. */
  goOnline: () =>
    apiFetch<CourierResponse>("/api/v1/couriers/me/online", {
      method: "POST",
      idempotencyKey: newIdempotencyKey(),
    }),

  /** POST /api/v1/couriers/me/offline → CourierResponse. Error: 409 BusyCannotGoOffline. */
  goOffline: () =>
    apiFetch<CourierResponse>("/api/v1/couriers/me/offline", {
      method: "POST",
      idempotencyKey: newIdempotencyKey(),
    }),

  /**
   * POST /api/v1/couriers/me/location → CourierResponse. Heartbeat de
   * ubicacion. SIN Idempotency-Key (replay-safe por timestamp en Tracking,
   * y agregarlo desperdicia espacio en Redis del middleware sin valor).
   */
  reportLocation: (req: ReportLocationRequest) =>
    apiFetch<CourierResponse>("/api/v1/couriers/me/location", {
      method: "POST",
      json: req,
    }),

  /**
   * GET /api/v1/couriers/me/current-assignment.
   *  - 200 → CurrentAssignmentResponse (con snapshot enriquecido de Fase 13.6).
   *  - 204 → apiFetch retorna undefined; lo normalizamos a null para el caller.
   */
  currentAssignment: async (): Promise<CurrentAssignmentResponse | null> => {
    const res = await apiFetch<CurrentAssignmentResponse | undefined>(
      "/api/v1/couriers/me/current-assignment",
    );
    return res ?? null;
  },

  /**
   * POST /api/v1/couriers/me/current-assignment/delivered → 204.
   * Errores: 404 NoActiveAssignment, 403 si JWT no es Courier.
   */
  markDelivered: () =>
    apiFetch<void>("/api/v1/couriers/me/current-assignment/delivered", {
      method: "POST",
      idempotencyKey: newIdempotencyKey(),
    }),
};
