"use client";

import { ChevronRight } from "lucide-react";
import Link from "next/link";

import { useCartStore } from "@/lib/cart/store";
import { formatMoney } from "@/lib/utils";

/**
 * Floating "Ver carrito" bar shown while the user has any items in their
 * cart — matches the Cocina Criolla mockup ("3 — Ver carrito — RD$ 1,125").
 */
export function CartFab() {
  const quantity = useCartStore((s) => s.totalQuantity());
  const subtotal = useCartStore((s) => s.subtotal());
  const lines = useCartStore((s) => s.lines);
  const currency = lines[0]?.currency ?? "DOP";

  if (quantity === 0) return null;

  return (
    <div className="fixed inset-x-0 bottom-4 z-30 px-4 md:px-6 pointer-events-none">
      <Link
        href="/carrito"
        className="pointer-events-auto mx-auto flex max-w-2xl items-center justify-between rounded-xl bg-accent text-white px-5 py-3.5 shadow-xl hover:bg-accent-500 transition-colors animate-slide-up"
      >
        <div className="flex items-center gap-3">
          <span className="inline-flex h-7 min-w-7 items-center justify-center rounded-full bg-white text-accent text-xs font-bold px-2">
            {quantity}
          </span>
          <span className="font-semibold">Ver carrito</span>
        </div>
        <div className="flex items-center gap-2 font-bold">
          {formatMoney(subtotal, currency)}
          <ChevronRight className="h-5 w-5" />
        </div>
      </Link>
    </div>
  );
}
