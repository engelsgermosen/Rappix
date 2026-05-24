/**
 * Merchant-facing order endpoints under /api/v1/orders. Only three:
 *  - GET /merchant/pending — orders awaiting accept/reject
 *  - POST /{id}/accept
 *  - POST /{id}/reject (optional reason)
 *
 * The backend filters by `merchantOwnerUserId == JWT.sub`.
 */
import { newIdempotencyKey } from "@/lib/utils";

import { apiFetch, withQuery } from "./client";
import type { OrderResponse, RejectOrderRequest } from "./types";

/** GET /api/v1/orders/merchant/pending — only status=AwaitingMerchant. */
export function listPending(params: { page?: number; pageSize?: number } = {}): Promise<OrderResponse[]> {
  return apiFetch<OrderResponse[]>(
    withQuery("/api/v1/orders/merchant/pending", {
      page: params.page ?? 1,
      pageSize: params.pageSize ?? 20,
    }),
  );
}

/** POST /api/v1/orders/{id}/accept — publishes MerchantAccepted on the bus. */
export function acceptOrder(orderId: string): Promise<void> {
  return apiFetch<void>(`/api/v1/orders/${orderId}/accept`, {
    method: "POST",
    idempotencyKey: newIdempotencyKey(),
  });
}

/** POST /api/v1/orders/{id}/reject — publishes MerchantRejected; the saga compensates. */
export function rejectOrder(orderId: string, req: RejectOrderRequest = {}): Promise<void> {
  return apiFetch<void>(`/api/v1/orders/${orderId}/reject`, {
    method: "POST",
    json: req,
    idempotencyKey: newIdempotencyKey(),
  });
}
