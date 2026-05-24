import type { OrderStatus, TrackingStatus } from "@/lib/api/types";

export const ORDER_STATUS_LABEL: Record<OrderStatus, string> = {
  Submitted: "Pedido confirmado",
  AwaitingMerchant: "Esperando al comercio",
  AwaitingPayment: "Procesando pago",
  AwaitingCourier: "Buscando repartidor",
  InProgress: "En camino",
  Completed: "Entregado",
  Cancelled: "Cancelado",
  Failed: "Fallido",
  NeedsReview: "Requiere revisión",
};

export const ORDER_STATUS_TONE: Record<OrderStatus, "default" | "info" | "success" | "warn" | "danger" | "muted"> = {
  Submitted: "default",
  AwaitingMerchant: "info",
  AwaitingPayment: "info",
  AwaitingCourier: "warn",
  InProgress: "warn",
  Completed: "success",
  Cancelled: "danger",
  Failed: "danger",
  NeedsReview: "muted",
};

export const TRACKING_STATUS_LABEL: Record<TrackingStatus, string> = {
  Placed: "Pedido confirmado",
  MerchantAccepted: "Comercio aceptó",
  CourierAssigned: "Repartidor asignado",
  Delivered: "Entregado",
  Cancelled: "Cancelado",
  Failed: "Fallido",
};

export function isOrderTerminal(s: OrderStatus): boolean {
  return s === "Completed" || s === "Cancelled" || s === "Failed" || s === "NeedsReview";
}

export function isOrderCancellable(s: OrderStatus): boolean {
  return s === "Submitted" || s === "AwaitingMerchant" || s === "AwaitingPayment" || s === "AwaitingCourier";
}
