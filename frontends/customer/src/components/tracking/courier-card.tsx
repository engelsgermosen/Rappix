"use client";

import { Bike, MessageCircle, Phone, Star } from "lucide-react";

import { Button } from "@/components/ui/button";
import type { OrderTrackingResponse } from "@/lib/api/types";
import { shortId } from "@/lib/utils";

/**
 * The customer cannot fetch the courier's name/vehicle/rating today — Dispatch
 * is courier-only. We render a friendly placeholder built from the courierId.
 * Listed as backend follow-up in API_MAP.md / FINDINGS.md.
 */
export function CourierCard({ tracking }: { tracking: OrderTrackingResponse }) {
  if (!tracking.courierId) {
    return (
      <div className="rounded-xl border border-dashed border-border bg-white p-5 flex items-center gap-3">
        <span className="inline-flex h-10 w-10 items-center justify-center rounded-full bg-muted text-muted-foreground">
          <Bike className="h-5 w-5" />
        </span>
        <div className="min-w-0">
          <div className="font-semibold text-sm">Buscando repartidor</div>
          <div className="text-xs text-muted-foreground">Cuando asignemos uno, lo verás aquí.</div>
        </div>
      </div>
    );
  }

  const courierLabel = `Repartidor ${shortId(tracking.courierId, "#")}`;
  return (
    <div className="rounded-xl border border-border bg-white p-4 md:p-5 flex items-center gap-3 md:gap-4">
      <span className="inline-flex h-12 w-12 items-center justify-center rounded-full bg-brand text-white flex-shrink-0">
        <Bike className="h-5 w-5" />
      </span>
      <div className="flex-1 min-w-0">
        <div className="font-semibold text-sm md:text-base flex items-center gap-2 flex-wrap">
          {courierLabel}
          <span className="inline-flex items-center gap-0.5 text-xs font-medium text-muted-foreground">
            <Star className="h-3 w-3 fill-accent text-accent" />
            5.0
          </span>
        </div>
        <div className="text-xs text-muted-foreground mt-0.5">Moto · Llega pronto</div>
      </div>
      <Button size="sm" variant="outline" className="gap-1.5">
        <Phone className="h-4 w-4" />
        <span className="hidden sm:inline">Llamar</span>
      </Button>
      <Button size="icon" variant="outline" className="hidden sm:inline-flex" aria-label="Chat">
        <MessageCircle className="h-4 w-4" />
      </Button>
    </div>
  );
}
