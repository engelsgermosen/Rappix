import { apiFetch, withQuery } from "./client";
import type { OrderResponse, PlaceOrderRequest } from "./types";

export const ordersApi = {
  place: (req: PlaceOrderRequest, idempotencyKey: string) =>
    apiFetch<OrderResponse>("/api/v1/orders", {
      method: "POST",
      json: req,
      idempotencyKey,
    }),

  byId: (orderId: string) =>
    apiFetch<OrderResponse>(`/api/v1/orders/${encodeURIComponent(orderId)}`),

  list: (params: { page?: number; pageSize?: number } = {}) =>
    apiFetch<OrderResponse[]>(
      withQuery("/api/v1/orders", { page: params.page, pageSize: params.pageSize }),
    ),

  cancel: (orderId: string, reason?: string) =>
    apiFetch<void>(`/api/v1/orders/${encodeURIComponent(orderId)}/cancel`, {
      method: "POST",
      json: { reason: reason ?? null },
    }),
};
