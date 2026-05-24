"use client";

import { create } from "zustand";

/**
 * In-memory cache of the last known stock per item. The backend has no
 * GET /items/{id}/stock — the only read is the response of PUT /stock. We
 * remember what came back so the merchant can see "current value" in the
 * Adjust dialog without paying for a full inventory read.
 *
 * Lost on reload — that's intentional. Documented in FINDINGS as gap §6.
 */

export type StockEntry = {
  quantity: number;
  isDepleted: boolean;
  readAtIso: string;
};

type StockState = {
  cache: Record<string, StockEntry>;
  set: (itemId: string, entry: StockEntry) => void;
  get: (itemId: string) => StockEntry | undefined;
  clear: () => void;
};

export const useStockCache = create<StockState>((set, get) => ({
  cache: {},
  set: (itemId, entry) => set((s) => ({ cache: { ...s.cache, [itemId]: entry } })),
  get: (itemId) => get().cache[itemId],
  clear: () => set({ cache: {} }),
}));
