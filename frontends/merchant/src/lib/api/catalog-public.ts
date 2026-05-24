/**
 * Public catalog endpoints. We only need the item photo presigned URL — the
 * search/byId endpoints are customer-facing.
 */
import { apiFetch } from "./client";

/**
 * GET /api/v1/catalog/items/{id}/photo-url — bare JSON string, 1-hour MinIO presigned URL.
 */
export function getItemPhotoUrl(itemId: string): Promise<string> {
  return apiFetch<string>(`/api/v1/catalog/items/${itemId}/photo-url`, { anonymous: true });
}
