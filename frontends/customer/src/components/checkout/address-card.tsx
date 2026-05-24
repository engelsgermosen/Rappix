"use client";

import { MapPin } from "lucide-react";

import { Badge } from "@/components/ui/badge";
import type { CheckoutAddress } from "@/lib/cart/checkout-store";

export function AddressCard({ address }: { address: CheckoutAddress }) {
  return (
    <div className="flex items-start gap-4">
      <div className="h-11 w-11 rounded-xl bg-brand-100/70 text-brand-700 inline-flex items-center justify-center flex-shrink-0">
        <MapPin className="h-5 w-5" />
      </div>
      <div className="min-w-0">
        <div className="flex items-center gap-2">
          <div className="font-semibold">{address.label}</div>
          <Badge variant="default">Predeterminada</Badge>
        </div>
        <div className="text-sm text-foreground/80 mt-0.5">{address.street}</div>
        {address.reference && (
          <div className="text-sm text-muted-foreground">{address.reference}</div>
        )}
      </div>
    </div>
  );
}

export function AddressMapPlaceholder() {
  return (
    <div className="mt-3 h-32 rounded-lg overflow-hidden bg-muted/70 grid grid-cols-6 gap-px relative">
      {Array.from({ length: 18 }).map((_, i) => (
        <div key={i} className="bg-white/80" />
      ))}
      <div className="absolute inset-0 flex items-center justify-center">
        <div className="h-7 w-7 rounded-full bg-brand grid place-items-center text-white">
          <MapPin className="h-4 w-4" />
        </div>
      </div>
    </div>
  );
}
