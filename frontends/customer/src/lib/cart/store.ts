"use client";

import { create } from "zustand";
import { persist } from "zustand/middleware";

import type { PublicItem, VerticalType } from "@/lib/api/types";

/**
 * Cart store — one merchant at a time (switching merchants asks to clear).
 * Items snapshot the price at add-to-cart time; the real charge comes from
 * the server quote at checkout (FE numbers are illustrative).
 */

export type CartLine = {
  itemId: string;
  itemName: string;
  unitPrice: number;
  currency: string;
  /** Per-unit total of selected modifier options (or 0 if none chosen). */
  modifierTotal: number;
  quantity: number;
  photoObjectKey: string | null;
};

type CartState = {
  merchantId: string | null;
  merchantName: string | null;
  merchantSlug: string | null;
  merchantVertical: VerticalType | null;
  lines: CartLine[];
  /** TanStack mutates server quote separately; this is the local subtotal preview. */
  subtotal: () => number;
  totalQuantity: () => number;
  /** Add 1 unit (merges with existing line by itemId). */
  add: (
    merchant: { id: string; name: string; slug: string; vertical: VerticalType },
    item: PublicItem,
    quantity?: number,
    modifierTotal?: number,
  ) => { ok: true } | { ok: false; reason: "different-merchant"; currentMerchantName: string };
  /** Force-replace cart to start fresh for a new merchant. */
  replaceMerchantAndAdd: (
    merchant: { id: string; name: string; slug: string; vertical: VerticalType },
    item: PublicItem,
    quantity?: number,
    modifierTotal?: number,
  ) => void;
  setQuantity: (itemId: string, quantity: number) => void;
  remove: (itemId: string) => void;
  clear: () => void;
};

const initialLines = (): CartLine[] => [];

export const useCartStore = create<CartState>()(
  persist(
    (set, get) => ({
      merchantId: null,
      merchantName: null,
      merchantSlug: null,
      merchantVertical: null,
      lines: initialLines(),

      subtotal: () =>
        get().lines.reduce((sum, line) => sum + (line.unitPrice + line.modifierTotal) * line.quantity, 0),

      totalQuantity: () => get().lines.reduce((sum, line) => sum + line.quantity, 0),

      add: (merchant, item, quantity = 1, modifierTotal = 0) => {
        const state = get();
        if (state.merchantId && state.merchantId !== merchant.id && state.lines.length > 0) {
          return {
            ok: false as const,
            reason: "different-merchant" as const,
            currentMerchantName: state.merchantName ?? "",
          };
        }
        const lines = [...state.lines];
        const idx = lines.findIndex((l) => l.itemId === item.id);
        if (idx >= 0) {
          lines[idx] = { ...lines[idx]!, quantity: lines[idx]!.quantity + quantity };
        } else {
          lines.push({
            itemId: item.id,
            itemName: item.name,
            unitPrice: item.priceAmount,
            currency: item.currency,
            modifierTotal,
            quantity,
            photoObjectKey: item.photoObjectKey,
          });
        }
        set({
          merchantId: merchant.id,
          merchantName: merchant.name,
          merchantSlug: merchant.slug,
          merchantVertical: merchant.vertical,
          lines,
        });
        return { ok: true as const };
      },

      replaceMerchantAndAdd: (merchant, item, quantity = 1, modifierTotal = 0) => {
        set({
          merchantId: merchant.id,
          merchantName: merchant.name,
          merchantSlug: merchant.slug,
          merchantVertical: merchant.vertical,
          lines: [
            {
              itemId: item.id,
              itemName: item.name,
              unitPrice: item.priceAmount,
              currency: item.currency,
              modifierTotal,
              quantity,
              photoObjectKey: item.photoObjectKey,
            },
          ],
        });
      },

      setQuantity: (itemId, quantity) => {
        const lines = get().lines.map((l) => (l.itemId === itemId ? { ...l, quantity: Math.max(0, quantity) } : l));
        const filtered = lines.filter((l) => l.quantity > 0);
        set({ lines: filtered });
        if (filtered.length === 0) {
          set({ merchantId: null, merchantName: null, merchantSlug: null, merchantVertical: null });
        }
      },

      remove: (itemId) => get().setQuantity(itemId, 0),

      clear: () =>
        set({
          merchantId: null,
          merchantName: null,
          merchantSlug: null,
          merchantVertical: null,
          lines: initialLines(),
        }),
    }),
    {
      name: "rappix.customer.cart",
      // Only persist the data, not the getters.
      partialize: (state) => ({
        merchantId: state.merchantId,
        merchantName: state.merchantName,
        merchantSlug: state.merchantSlug,
        merchantVertical: state.merchantVertical,
        lines: state.lines,
      }),
    },
  ),
);
