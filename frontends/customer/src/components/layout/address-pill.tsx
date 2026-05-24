"use client";

import { ChevronRight, MapPin } from "lucide-react";

import { useCheckoutStore } from "@/lib/cart/checkout-store";

export function AddressPill() {
  const address = useCheckoutStore((s) => s.address);

  return (
    <button
      className="inline-flex h-10 max-w-[280px] items-center gap-2 rounded-full bg-white/10 px-3 text-sm text-white hover:bg-white/20 transition-colors"
      type="button"
      onClick={() => {
        // Address editor lives in the checkout — keep the header pill informative-only.
      }}
    >
      <MapPin className="h-4 w-4 flex-shrink-0" />
      <span className="truncate font-medium">{address.street}</span>
      <ChevronRight className="h-4 w-4 flex-shrink-0 opacity-60" />
    </button>
  );
}
