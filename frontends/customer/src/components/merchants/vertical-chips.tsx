"use client";

import { LayoutGrid, Package, Pill, ShoppingCart, Utensils } from "lucide-react";

import { cn } from "@/lib/utils";
import type { VerticalType } from "@/lib/api/types";

export type VerticalFilter = VerticalType | "all";

const CHIPS: { key: VerticalFilter; label: string; icon: React.ComponentType<{ className?: string }> }[] = [
  { key: "all",      label: "Todo",     icon: LayoutGrid },
  { key: "Food",     label: "Comida",   icon: Utensils },
  { key: "Pharmacy", label: "Farmacia", icon: Pill },
  { key: "Grocery",  label: "Súper",    icon: ShoppingCart },
  { key: "Parcel",   label: "Envíos",   icon: Package },
];

export function VerticalChips({
  active,
  onChange,
}: {
  active: VerticalFilter;
  onChange: (next: VerticalFilter) => void;
}) {
  return (
    <div className="flex gap-2 md:gap-3 overflow-x-auto no-scrollbar -mx-4 px-4 md:mx-0 md:px-0">
      {CHIPS.map(({ key, label, icon: Icon }) => {
        const isActive = active === key;
        return (
          <button
            key={key}
            onClick={() => onChange(key)}
            className={cn(
              "inline-flex items-center gap-2 h-11 px-4 rounded-full border transition-colors text-sm font-medium flex-shrink-0",
              isActive
                ? "bg-brand text-white border-brand shadow-sm"
                : "bg-white text-foreground/70 border-border hover:border-brand/40 hover:text-foreground",
            )}
          >
            <Icon className="h-4 w-4" />
            {label}
          </button>
        );
      })}
    </div>
  );
}
