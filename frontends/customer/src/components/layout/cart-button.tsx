"use client";

import { ShoppingBag } from "lucide-react";
import Link from "next/link";

import { useCartStore } from "@/lib/cart/store";

export function CartButton() {
  const totalQuantity = useCartStore((s) => s.totalQuantity());

  return (
    <Link
      href="/carrito"
      className="relative inline-flex h-10 w-10 items-center justify-center rounded-full bg-white/10 text-white hover:bg-white/20 transition-colors"
      aria-label="Carrito"
    >
      <ShoppingBag className="h-5 w-5" />
      {totalQuantity > 0 && (
        <span className="absolute -top-1 -right-1 inline-flex h-5 min-w-5 items-center justify-center rounded-full bg-accent px-1 text-[10px] font-bold text-white">
          {totalQuantity}
        </span>
      )}
    </Link>
  );
}
