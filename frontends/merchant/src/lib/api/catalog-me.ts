/**
 * Catalog-owner endpoints under /api/v1/catalog/me. Every endpoint resolves
 * the merchant from the JWT subject. The catalog row only exists after the
 * merchant is `Active` — GETs return 404 (`Catalog.Catalog.NotFound`) until then.
 */
import { newIdempotencyKey } from "@/lib/utils";

import { apiFetch, withQuery } from "./client";
import type {
  AddModifierOptionRequest,
  AddModifierRequest,
  AdjustStockRequest,
  CatalogResponse,
  CreateCategoryRequest,
  CreateItemRequest,
  ItemResponse,
  SetAvailabilityRequest,
  SetItemAttributesRequest,
  StockResponse,
  UpdateItemRequest,
} from "./types";

/** GET /api/v1/catalog/me/ — note the trailing slash; the backend route is literal. */
export function getMyCatalog(): Promise<CatalogResponse> {
  return apiFetch<CatalogResponse>("/api/v1/catalog/me/");
}

/** POST /api/v1/catalog/me/categories. */
export function addCategory(req: CreateCategoryRequest): Promise<CatalogResponse> {
  return apiFetch<CatalogResponse>("/api/v1/catalog/me/categories", {
    method: "POST",
    json: req,
    idempotencyKey: newIdempotencyKey(),
  });
}

/** DELETE /api/v1/catalog/me/categories/{id}. */
export function removeCategory(categoryId: string): Promise<CatalogResponse> {
  return apiFetch<CatalogResponse>(`/api/v1/catalog/me/categories/${categoryId}`, {
    method: "DELETE",
    idempotencyKey: newIdempotencyKey(),
  });
}

/** POST /api/v1/catalog/me/items. */
export function createItem(req: CreateItemRequest): Promise<ItemResponse> {
  return apiFetch<ItemResponse>("/api/v1/catalog/me/items", {
    method: "POST",
    json: req,
    idempotencyKey: newIdempotencyKey(),
  });
}

/** GET /api/v1/catalog/me/items?... */
export function listMyItems(params: {
  categoryId?: string | null;
  page?: number;
  pageSize?: number;
}): Promise<ItemResponse[]> {
  return apiFetch<ItemResponse[]>(
    withQuery("/api/v1/catalog/me/items", {
      categoryId: params.categoryId ?? null,
      page: params.page ?? 1,
      pageSize: params.pageSize ?? 20,
    }),
  );
}

/** GET /api/v1/catalog/me/items/{id}. */
export function getMyItem(itemId: string): Promise<ItemResponse> {
  return apiFetch<ItemResponse>(`/api/v1/catalog/me/items/${itemId}`);
}

/** PUT /api/v1/catalog/me/items/{id}. */
export function updateItem(itemId: string, req: UpdateItemRequest): Promise<ItemResponse> {
  return apiFetch<ItemResponse>(`/api/v1/catalog/me/items/${itemId}`, {
    method: "PUT",
    json: req,
    idempotencyKey: newIdempotencyKey(),
  });
}

/** DELETE /api/v1/catalog/me/items/{id} — soft delete. */
export function deleteItem(itemId: string): Promise<void> {
  return apiFetch<void>(`/api/v1/catalog/me/items/${itemId}`, {
    method: "DELETE",
    idempotencyKey: newIdempotencyKey(),
  });
}

/** PUT /api/v1/catalog/me/items/{id}/availability — publish/hide. */
export function setItemAvailability(itemId: string, req: SetAvailabilityRequest): Promise<ItemResponse> {
  return apiFetch<ItemResponse>(`/api/v1/catalog/me/items/${itemId}/availability`, {
    method: "PUT",
    json: req,
    idempotencyKey: newIdempotencyKey(),
  });
}

/** PUT /api/v1/catalog/me/items/{id}/attributes — replace JSONB attrs map. */
export function setItemAttributes(itemId: string, req: SetItemAttributesRequest): Promise<ItemResponse> {
  return apiFetch<ItemResponse>(`/api/v1/catalog/me/items/${itemId}/attributes`, {
    method: "PUT",
    json: req,
    idempotencyKey: newIdempotencyKey(),
  });
}

/** POST /api/v1/catalog/me/items/{id}/modifiers — add a modifier group. */
export function addModifier(itemId: string, req: AddModifierRequest): Promise<ItemResponse> {
  return apiFetch<ItemResponse>(`/api/v1/catalog/me/items/${itemId}/modifiers`, {
    method: "POST",
    json: req,
    idempotencyKey: newIdempotencyKey(),
  });
}

/** POST /api/v1/catalog/me/items/{id}/modifiers/{m}/options — add option to a modifier group. */
export function addModifierOption(
  itemId: string,
  modifierId: string,
  req: AddModifierOptionRequest,
): Promise<ItemResponse> {
  return apiFetch<ItemResponse>(`/api/v1/catalog/me/items/${itemId}/modifiers/${modifierId}/options`, {
    method: "POST",
    json: req,
    idempotencyKey: newIdempotencyKey(),
  });
}

/** PUT /api/v1/catalog/me/items/{id}/stock — Set (absolute) or Restock (add). */
export function adjustStock(itemId: string, req: AdjustStockRequest): Promise<StockResponse> {
  return apiFetch<StockResponse>(`/api/v1/catalog/me/items/${itemId}/stock`, {
    method: "PUT",
    json: req,
    idempotencyKey: newIdempotencyKey(),
  });
}

/** POST /api/v1/catalog/me/items/{id}/photo — multipart upload (≤4 MB). */
export function uploadItemPhoto(itemId: string, file: File): Promise<ItemResponse> {
  const form = new FormData();
  form.append("file", file);
  return apiFetch<ItemResponse>(`/api/v1/catalog/me/items/${itemId}/photo`, {
    method: "POST",
    body: form,
    idempotencyKey: newIdempotencyKey(),
  });
}
