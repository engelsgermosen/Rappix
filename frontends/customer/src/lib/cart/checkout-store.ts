"use client";

import { create } from "zustand";

import type { QuoteResponse } from "@/lib/api/types";

/**
 * Ephemeral store for the checkout flow — holds the active quote + selected
 * address/tip so /cart → /checkout share state without re-quoting on navigation.
 * Not persisted (quote TTL is 10 min, re-quote on reload is cheap).
 */

export type CheckoutAddress = {
  street: string;
  reference: string | null;
  latitude: number;
  longitude: number;
  label: string;
};

type CheckoutState = {
  quote: QuoteResponse | null;
  tip: number;
  couponCode: string | null;
  address: CheckoutAddress;
  setQuote: (q: QuoteResponse | null) => void;
  setTip: (n: number) => void;
  setCoupon: (c: string | null) => void;
  setAddress: (a: CheckoutAddress) => void;
  reset: () => void;
};

const defaultAddress: CheckoutAddress = {
  street: "C/ José Reyes 14, Apto 3B",
  reference: "Zona Colonial, Santo Domingo",
  latitude: 18.4719,
  longitude: -69.9,
  label: "Casa",
};

export const useCheckoutStore = create<CheckoutState>((set) => ({
  quote: null,
  tip: 50,
  couponCode: null,
  address: defaultAddress,
  setQuote: (q) => set({ quote: q }),
  setTip: (n) => set({ tip: Math.max(0, Math.round(n)) }),
  setCoupon: (c) => set({ couponCode: c?.trim() || null }),
  setAddress: (a) => set({ address: a }),
  reset: () => set({ quote: null, tip: 50, couponCode: null }),
}));
