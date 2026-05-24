import { apiFetch, withQuery } from "./client";
import type { Paged, PublicItem } from "./types";

export const catalogApi = {
  search: (params: { q?: string; merchantId?: string; categoryId?: string; page?: number; pageSize?: number }) =>
    apiFetch<Paged<PublicItem>>(
      withQuery("/api/v1/catalog/items/search", {
        q: params.q,
        merchantId: params.merchantId,
        categoryId: params.categoryId,
        page: params.page,
        pageSize: params.pageSize,
      }),
      { anonymous: true },
    ),

  byId: (id: string) =>
    apiFetch<PublicItem>(`/api/v1/catalog/items/${encodeURIComponent(id)}`, { anonymous: true }),

  photoUrl: (id: string) =>
    apiFetch<string>(`/api/v1/catalog/items/${encodeURIComponent(id)}/photo-url`, { anonymous: true }),
};
