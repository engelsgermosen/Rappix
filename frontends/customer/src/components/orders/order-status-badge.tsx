import { Badge } from "@/components/ui/badge";
import type { OrderStatus } from "@/lib/api/types";
import { ORDER_STATUS_LABEL, ORDER_STATUS_TONE } from "@/lib/orders/status";

const VARIANT_BY_TONE: Record<ReturnType<() => string>, "default" | "info" | "success" | "warn" | "danger" | "muted"> = {
  default: "default",
  info: "info",
  success: "success",
  warn: "warn",
  danger: "danger",
  muted: "muted",
};

export function OrderStatusBadge({ status }: { status: OrderStatus }) {
  const tone = ORDER_STATUS_TONE[status];
  return <Badge variant={VARIANT_BY_TONE[tone]}>{ORDER_STATUS_LABEL[status]}</Badge>;
}
