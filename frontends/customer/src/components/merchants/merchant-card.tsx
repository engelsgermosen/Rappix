"use client";

import { Clock, Star } from "lucide-react";
import Link from "next/link";

import type { NearbyMerchant, VerticalType } from "@/lib/api/types";
import { Badge } from "@/components/ui/badge";
import { cn, formatMoney } from "@/lib/utils";

/**
 * Visual props the backend doesn't expose today (distanceKm, etaMinutes,
 * deliveryFeeCents, isOpen, badge). They're derived deterministically from the
 * merchant id so the UI looks consistent across reloads. Listed as backend
 * follow-ups in API_MAP.md.
 */
type CardProps = {
  merchant: NearbyMerchant;
};

const VERTICAL_LABEL: Record<VerticalType, string> = {
  Food: "Comida",
  Pharmacy: "Farmacia",
  Grocery: "Súper",
  Parcel: "Envíos",
};

const HERO_BY_VERTICAL: Record<VerticalType, string> = {
  Food: "hero-food",
  Pharmacy: "hero-pharmacy",
  Grocery: "hero-grocery",
  Parcel: "hero-parcel",
};

function pseudoHash(s: string) {
  let h = 0;
  for (let i = 0; i < s.length; i++) h = (h * 31 + s.charCodeAt(i)) >>> 0;
  return h;
}

function deriveDisplay(m: NearbyMerchant) {
  const h = pseudoHash(m.id);
  const eta = 15 + (h % 35); // 15–49 min
  const distanceKm = 0.4 + ((h % 50) / 10); // 0.4 – 5.4 km
  const deliveryFee = 39 + (h % 60); // RD$ 39 – 98
  // Tags: random offer + occasional "Cerrado"
  const offer = h % 8 === 0 ? "-20% hoy" : h % 13 === 0 ? "Envío gratis" : h % 17 === 0 ? "Cotiza al instante" : null;
  const closed = h % 11 === 0;
  // Rating fallback when backend has none yet.
  const rating = m.averageRating ?? Number((4.2 + ((h % 7) / 10)).toFixed(1));
  return { eta, distanceKm, deliveryFee, offer, closed, rating };
}

export function MerchantCard({ merchant }: CardProps) {
  const { eta, distanceKm, deliveryFee, offer, closed, rating } = deriveDisplay(merchant);
  const hero = closed ? "hero-closed" : HERO_BY_VERTICAL[merchant.verticalType];

  return (
    <Link
      href={`/comercios/${merchant.slug}`}
      className={cn(
        "group block rounded-xl border border-border bg-white overflow-hidden hover:shadow-md hover:-translate-y-0.5 transition-all",
        closed && "opacity-90",
      )}
      prefetch={false}
    >
      <div className={cn("relative aspect-[16/9] stripes", hero)}>
        {/* Top-left tag (offer) */}
        {offer && !closed && (
          <div className="absolute top-3 left-3">
            <Badge variant="accent" className="text-xs px-2.5 py-1 rounded-md">
              {offer}
            </Badge>
          </div>
        )}
        {closed && (
          <div className="absolute top-3 right-3">
            <Badge variant="dark" className="text-xs px-2.5 py-1 rounded-md">
              Cerrado
            </Badge>
          </div>
        )}
        {/* Bottom watermark: "VERTICAL · NAME" */}
        <div className="absolute left-3 bottom-3 text-[10px] font-semibold text-white/80 tracking-widest uppercase">
          {VERTICAL_LABEL[merchant.verticalType]} · {merchant.name}
        </div>
        {/* Bottom-right time pill */}
        <div className="absolute right-3 bottom-3">
          <span className="inline-flex items-center gap-1 rounded-md bg-amber-100 px-2 py-1 text-[11px] font-semibold text-amber-900">
            <Clock className="h-3 w-3" />
            {eta} min
          </span>
        </div>
      </div>
      <div className="p-4">
        <div className="flex items-start justify-between gap-3">
          <div className="min-w-0">
            <div className="font-semibold text-foreground truncate">{merchant.name}</div>
            <div className="text-sm text-muted-foreground truncate">
              {VERTICAL_LABEL[merchant.verticalType]} · {distanceKm.toFixed(1)} km · Envío {formatMoney(deliveryFee)}
            </div>
          </div>
          <div className="flex items-center gap-1 text-sm font-semibold text-foreground/90 flex-shrink-0">
            <Star className="h-4 w-4 fill-accent text-accent" />
            {rating.toFixed(1)}
          </div>
        </div>
      </div>
    </Link>
  );
}

export function MerchantCardSkeleton() {
  return (
    <div className="rounded-xl border border-border bg-white overflow-hidden">
      <div className="aspect-[16/9] bg-muted/80 animate-pulse" />
      <div className="p-4 space-y-2">
        <div className="h-4 w-2/3 bg-muted animate-pulse rounded" />
        <div className="h-3 w-1/2 bg-muted animate-pulse rounded" />
      </div>
    </div>
  );
}
