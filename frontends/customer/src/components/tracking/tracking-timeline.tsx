import { Check } from "lucide-react";

import { cn } from "@/lib/utils";
import type { OrderResponse, TrackingStatus } from "@/lib/api/types";

type Step = {
  key: string;
  label: string;
  description: string;
  done: boolean;
  active: boolean;
};

function fmt(dt: string | null | undefined, fallback: string) {
  if (!dt) return fallback;
  try {
    return new Date(dt).toLocaleString("es-DO", {
      day: "2-digit",
      month: "short",
      hour: "2-digit",
      minute: "2-digit",
    });
  } catch {
    return fallback;
  }
}

export function TrackingTimeline({
  order,
  liveStatus,
}: {
  order: OrderResponse;
  liveStatus: TrackingStatus | null;
}) {
  const merged = liveStatus ?? order.status;
  const isDelivered = merged === "Delivered" || order.status === "Completed";
  const isCancelled = merged === "Cancelled" || order.status === "Cancelled" || order.status === "Failed";
  const courierAssigned = liveStatus === "CourierAssigned" || ["AwaitingCourier", "InProgress"].includes(order.status) || isDelivered;
  const inTransit = liveStatus === "CourierAssigned" || order.status === "InProgress" || isDelivered;

  const steps: Step[] = [
    {
      key: "confirmed",
      label: "Pedido confirmado",
      description: `Recibimos tu pedido a las ${fmt(order.createdAtUtc, "—")}`,
      done: true,
      active: !courierAssigned && !inTransit && !isDelivered,
    },
    {
      key: "assigned",
      label: "Repartidor asignado",
      description: courierAssigned ? "El repartidor va camino al comercio" : "Buscando un repartidor cerca",
      done: courierAssigned,
      active: courierAssigned && !inTransit,
    },
    {
      key: "in-transit",
      label: "En camino contigo",
      description: inTransit ? "Tu pedido salió hacia tu dirección" : "Cuando el repartidor recoja el pedido te avisamos",
      done: inTransit && !isDelivered,
      active: inTransit && !isDelivered,
    },
    {
      key: "delivered",
      label: "Entregado",
      description: isDelivered ? fmt(order.completedAtUtc ?? order.closedAtUtc, "Llegará pronto") : "Llega en pocos minutos",
      done: isDelivered,
      active: isDelivered,
    },
  ];

  return (
    <ol className="space-y-4">
      {steps.map((s) => (
        <li key={s.key} className="flex gap-3">
          <span
            className={cn(
              "mt-0.5 inline-flex h-7 w-7 flex-shrink-0 items-center justify-center rounded-full text-xs font-bold",
              s.done && !isCancelled && "bg-emerald-100 text-emerald-700",
              s.active && !s.done && "bg-brand-100 text-brand-700 ring-2 ring-brand-300 animate-pulse",
              !s.done && !s.active && "bg-muted text-muted-foreground",
              isCancelled && "bg-red-100 text-red-700",
            )}
          >
            {s.done ? <Check className="h-4 w-4" /> : "•"}
          </span>
          <div>
            <div className={cn("font-semibold text-sm", s.active && "text-brand")}>{s.label}</div>
            <div className="text-xs text-muted-foreground mt-0.5">{s.description}</div>
          </div>
        </li>
      ))}
      {isCancelled && order.cancellationReason && (
        <li className="rounded-lg bg-red-50 p-3 text-sm text-red-700">
          <div className="font-semibold">Pedido cancelado</div>
          <div>{order.cancellationReason}</div>
        </li>
      )}
    </ol>
  );
}
