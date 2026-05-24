import { apiFetch } from "./client";
import type { OrderTrackingResponse } from "./types";

export const trackingApi = {
  snapshot: (orderId: string) =>
    apiFetch<OrderTrackingResponse>(`/api/v1/tracking/orders/${encodeURIComponent(orderId)}`),
};
