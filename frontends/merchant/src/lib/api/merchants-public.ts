/**
 * Public merchants endpoints. We only need the logo presigned URL — the rest
 * (nearby, by-slug, by-id) is customer-facing.
 */
import { apiFetch } from "./client";

/**
 * GET /api/v1/merchants/{id}/logo-url — returns a bare JSON string with a
 * 1-hour MinIO presigned URL.
 */
export function getLogoUrl(merchantId: string): Promise<string> {
  return apiFetch<string>(`/api/v1/merchants/${merchantId}/logo-url`, { anonymous: true });
}
