import { apiFetch, withQuery } from "./client";
import type { NearbyMerchant, PublicMerchant, VerticalType } from "./types";

export const merchantsApi = {
  nearby: (params: { lat: number; lng: number; vertical?: VerticalType; page?: number; pageSize?: number }) =>
    apiFetch<NearbyMerchant[]>(
      withQuery("/api/v1/merchants/nearby", {
        lat: params.lat,
        lng: params.lng,
        vertical: params.vertical,
        page: params.page,
        pageSize: params.pageSize,
      }),
      { anonymous: true },
    ),

  byId: (id: string) =>
    apiFetch<PublicMerchant>(`/api/v1/merchants/${encodeURIComponent(id)}`, { anonymous: true }),

  bySlug: (slug: string) =>
    apiFetch<PublicMerchant>(`/api/v1/merchants/by-slug/${encodeURIComponent(slug)}`, { anonymous: true }),

  logoUrl: (id: string) =>
    apiFetch<string>(`/api/v1/merchants/${encodeURIComponent(id)}/logo-url`, { anonymous: true }),
};
