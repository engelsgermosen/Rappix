/**
 * Merchant-owner endpoints under /api/v1/merchants/me. Every endpoint resolves
 * the merchant by the JWT subject; the FE never sends a merchantId.
 */
import { newIdempotencyKey } from "@/lib/utils";

import { apiFetch } from "./client";
import type {
  AddServiceAreaRequest,
  MerchantResponse,
  SetPickupLocationRequest,
  UpdateMerchantProfileRequest,
  UpdateOperatingHoursRequest,
} from "./types";

/** GET /api/v1/merchants/me — owner view of the merchant aggregate. */
export function getMyMerchant(): Promise<MerchantResponse> {
  return apiFetch<MerchantResponse>("/api/v1/merchants/me");
}

/** PUT /api/v1/merchants/me — update basic profile fields. */
export function updateMyMerchant(req: UpdateMerchantProfileRequest): Promise<MerchantResponse> {
  return apiFetch<MerchantResponse>("/api/v1/merchants/me", {
    method: "PUT",
    json: req,
    idempotencyKey: newIdempotencyKey(),
  });
}

/** PUT /api/v1/merchants/me/operating-hours — bulk replace open ranges. */
export function updateOperatingHours(req: UpdateOperatingHoursRequest): Promise<MerchantResponse> {
  return apiFetch<MerchantResponse>("/api/v1/merchants/me/operating-hours", {
    method: "PUT",
    json: req,
    idempotencyKey: newIdempotencyKey(),
  });
}

/** POST /api/v1/merchants/me/service-areas — add one area (circle MVP). */
export function addServiceArea(req: AddServiceAreaRequest): Promise<MerchantResponse> {
  return apiFetch<MerchantResponse>("/api/v1/merchants/me/service-areas", {
    method: "POST",
    json: req,
    idempotencyKey: newIdempotencyKey(),
  });
}

/** DELETE /api/v1/merchants/me/service-areas/{id}. */
export function removeServiceArea(areaId: string): Promise<MerchantResponse> {
  return apiFetch<MerchantResponse>(`/api/v1/merchants/me/service-areas/${areaId}`, {
    method: "DELETE",
    idempotencyKey: newIdempotencyKey(),
  });
}

/** PUT /api/v1/merchants/me/pickup-location — lat/lng only (backend ignores everything else). */
export function setPickupLocation(req: SetPickupLocationRequest): Promise<MerchantResponse> {
  return apiFetch<MerchantResponse>("/api/v1/merchants/me/pickup-location", {
    method: "PUT",
    json: req,
    idempotencyKey: newIdempotencyKey(),
  });
}

/** POST /api/v1/merchants/me/logo — multipart upload (≤2 MB). */
export function uploadLogo(file: File): Promise<MerchantResponse> {
  const form = new FormData();
  form.append("file", file);
  return apiFetch<MerchantResponse>("/api/v1/merchants/me/logo", {
    method: "POST",
    body: form,
    idempotencyKey: newIdempotencyKey(),
  });
}

/** POST /api/v1/merchants/me/submit-for-approval — Draft → Pending. Requires email_confirmed=true. */
export function submitForApproval(): Promise<void> {
  return apiFetch<void>("/api/v1/merchants/me/submit-for-approval", {
    method: "POST",
    idempotencyKey: newIdempotencyKey(),
  });
}
