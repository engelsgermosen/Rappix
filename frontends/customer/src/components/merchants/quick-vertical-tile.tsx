"use client";

import Link from "next/link";

import type { VerticalType } from "@/lib/api/types";

const ICON_CLASS = "bg-[--tile-bg] text-[--tile-fg]";

const TILES: {
  vertical: VerticalType;
  title: string;
  subtitle: string;
  icon: string;
  bg: string;
  fg: string;
}[] = [
  { vertical: "Food",     title: "Comida rápida", subtitle: "En 30 min",    icon: "🍔",  bg: "#FBE3DE", fg: "#C04A21" },
  { vertical: "Pharmacy", title: "Farmacia 24/7", subtitle: "Sin receta",   icon: "✚",   bg: "#D8F0EE", fg: "#2F8C87" },
  { vertical: "Grocery",  title: "Súper en casa", subtitle: "Frescos",      icon: "🛒",  bg: "#E1F0DB", fg: "#4D7E48" },
  { vertical: "Parcel",   title: "Envía paquetes", subtitle: "En la ciudad", icon: "📦", bg: "#DBE6F8", fg: "#4C7DC0" },
];

export function QuickVerticalTiles({
  onSelect,
}: {
  onSelect?: (v: VerticalType) => void;
}) {
  return (
    <div className="grid grid-cols-2 md:grid-cols-4 gap-3 md:gap-4">
      {TILES.map((t) => {
        const content = (
          <div
            className="group rounded-xl border border-border bg-white p-4 hover:border-brand/40 hover:shadow-sm transition-all cursor-pointer"
            style={{ ["--tile-bg" as string]: t.bg, ["--tile-fg" as string]: t.fg } as React.CSSProperties}
          >
            <div className={`inline-flex h-12 w-12 items-center justify-center rounded-lg ${ICON_CLASS} text-xl font-bold mb-3`}>
              <span aria-hidden>{t.icon}</span>
            </div>
            <div className="font-semibold text-sm md:text-base">{t.title}</div>
            <div className="text-xs md:text-sm text-muted-foreground">{t.subtitle}</div>
          </div>
        );
        if (onSelect) {
          return (
            <button key={t.vertical} type="button" onClick={() => onSelect(t.vertical)} className="text-left">
              {content}
            </button>
          );
        }
        return (
          <Link key={t.vertical} href={`/?vertical=${t.vertical}`}>
            {content}
          </Link>
        );
      })}
    </div>
  );
}
