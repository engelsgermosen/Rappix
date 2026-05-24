"use client";

import { MessageCircle, Phone } from "lucide-react";

import { Button } from "@/components/ui/button";
import type { OrderTrackingResponse } from "@/lib/api/types";
import { shortId } from "@/lib/utils";

/**
 * The customer cannot fetch the courier's name/vehicle/rating today — Dispatch
 * is courier-only. We render a friendly placeholder built from the courierId.
 * Listed as backend follow-up in API_MAP.md.
 */
export function CourierCard({ tracking }: { tracking: OrderTrackingResponse }) {
  if (!tracking.courierId) {
    return (
      <div className="rounded-xl border border-dashed border-border bg-white p-4 text-sm text-muted-foreground">
        Cuando se asigne un repartidor verás aquí sus datos.
      </div>
    );
  }
  const courierLabel = `Repartidor ${shortId(tracking.courierId, "#")}`;
  return (
    <div className="rounded-xl border border-border bg-white p-4 flex items-center gap-3">
      <span className="inline-flex h-11 w-11 items-center justify-center rounded-full bg-brand text-white font-bold">
        🛵
      </span>
      <div className="flex-1 min-w-0">
        <div className="font-semibold text-sm">{courierLabel}</div>
        <div className="text-xs text-muted-foreground">Moto · Llega pronto</div>
      </div>
      <Button size="sm" variant="outline" className="gap-1.5">
        <Phone className="h-4 w-4" /> Llamar
      </Button>
      <Button size="sm" variant="outline" className="gap-1.5 hidden sm:inline-flex">
        <MessageCircle className="h-4 w-4" />
      </Button>
    </div>
  );
}
