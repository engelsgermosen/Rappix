"use client";

import { create } from "zustand";

import type { QuoteResponse } from "@/lib/api/types";

/**
 * Ephemeral store for the checkout flow — holds the active quote + selected
 * tip + the SavedAddress id the user chose for THIS checkout. Not persisted
 * (quote TTL is 10 min, re-quote on reload is cheap).
 *
 * NOTE: the address itself is NOT stored here anymore — it lives in the
 * dedicated address store. We only remember which one the user picked for
 * this checkout (defaults to the user's default address).
 */

type CheckoutState = {
  quote: QuoteResponse | null;
  tip: number;
  couponCode: string | null;
  /** Id of the SavedAddress chosen for this checkout. `null` = use default. */
  selectedAddressId: string | null;
  setQuote: (q: QuoteResponse | null) => void;
  setTip: (n: number) => void;
  setCoupon: (c: string | null) => void;
  setSelectedAddressId: (id: string | null) => void;
  reset: () => void;
};

export const useCheckoutStore = create<CheckoutState>((set) => ({
  quote: null,
  tip: 50,
  couponCode: null,
  selectedAddressId: null,
  setQuote: (q) => set({ quote: q }),
  setTip: (n) => set({ tip: Math.max(0, Math.round(n)) }),
  setCoupon: (c) => set({ couponCode: c?.trim() || null }),
  setSelectedAddressId: (id) => set({ selectedAddressId: id }),
  reset: () => set({ quote: null, tip: 50, couponCode: null, selectedAddressId: null }),
}));
