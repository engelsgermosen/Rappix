"use client";

import { Plus } from "lucide-react";
import { useState } from "react";
import { toast } from "sonner";

import { Badge } from "@/components/ui/badge";
import type { PublicItem, VerticalType } from "@/lib/api/types";
import { useCartStore } from "@/lib/cart/store";
import { formatMoney } from "@/lib/utils";

type Merchant = { id: string; name: string; slug: string; vertical: VerticalType };

function attrBadges(item: PublicItem, vertical: VerticalType): React.ReactNode {
  const a = item.attributes;
  const bits: { key: string; label: string; variant?: "muted" | "info" | "warn" }[] = [];
  if (vertical === "Food") {
    if (a.isVegetarian === "true") bits.push({ key: "veg", label: "Vegetariano", variant: "info" });
    if (a.spicyLevel && Number(a.spicyLevel) >= 2) bits.push({ key: "spice", label: "Picante", variant: "warn" });
  }
  if (vertical === "Pharmacy") {
    if (a.requiresPrescription === "true") bits.push({ key: "rx", label: "Receta", variant: "warn" });
  }
  if (vertical === "Grocery") {
    if (a.brand) bits.push({ key: "brand", label: a.brand });
    if (a.weightGrams) bits.push({ key: "weight", label: `${a.weightGrams} g` });
  }
  if (vertical === "Parcel") {
    if (a.fragile === "true") bits.push({ key: "fragile", label: "Frágil", variant: "warn" });
  }
  if (bits.length === 0) return null;
  return (
    <div className="flex flex-wrap gap-1.5 mt-2">
      {bits.map((b) => (
        <Badge key={b.key} variant={b.variant ?? "muted"}>
          {b.label}
        </Badge>
      ))}
    </div>
  );
}

export function ItemCard({
  item,
  merchant,
  popular,
}: {
  item: PublicItem;
  merchant: Merchant;
  popular?: boolean;
}) {
  const add = useCartStore((s) => s.add);
  const replaceMerchantAndAdd = useCartStore((s) => s.replaceMerchantAndAdd);
  const [adding, setAdding] = useState(false);

  async function onAdd() {
    setAdding(true);
    try {
      const result = add(merchant, item, 1, 0);
      if (!result.ok && result.reason === "different-merchant") {
        const ok = window.confirm(
          `Tu carrito tiene productos de ${result.currentMerchantName}. ¿Vaciarlo para empezar uno nuevo de ${merchant.name}?`,
        );
        if (!ok) return;
        replaceMerchantAndAdd(merchant, item, 1, 0);
        toast.success(`Carrito reiniciado con ${item.name}`);
      } else {
        toast.success(`Añadido: ${item.name}`);
      }
    } finally {
      setAdding(false);
    }
  }

  return (
    <article className="relative grid grid-cols-[1fr_88px] sm:grid-cols-[1fr_120px] gap-3 sm:gap-5 rounded-xl border border-border bg-white p-3 sm:p-4 hover:border-brand/30 hover:shadow-sm transition-all">
      <div>
        {popular && (
          <Badge variant="accent" className="mb-1.5">
            POPULAR
          </Badge>
        )}
        <div className="font-semibold text-foreground">{item.name}</div>
        {item.description && (
          <p className="text-sm text-muted-foreground mt-0.5 line-clamp-2">{item.description}</p>
        )}
        {attrBadges(item, merchant.vertical)}
        <div className="mt-3 font-bold text-foreground">{formatMoney(item.priceAmount, item.currency)}</div>
      </div>
      <div className="relative">
        <div className="aspect-square w-full rounded-lg bg-brand-100/60 flex items-center justify-center text-brand-700/70 text-xs font-semibold uppercase tracking-widest">
          Foto
        </div>
        <button
          type="button"
          onClick={onAdd}
          disabled={adding}
          aria-label={`Agregar ${item.name}`}
          className="absolute -bottom-2 -right-2 inline-flex h-9 w-9 items-center justify-center rounded-full bg-accent text-white shadow-md hover:scale-105 hover:bg-accent-500 transition-all disabled:opacity-60"
        >
          <Plus className="h-5 w-5" />
        </button>
      </div>
    </article>
  );
}
