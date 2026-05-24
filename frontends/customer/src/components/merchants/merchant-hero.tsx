import { ArrowLeft, Clock, Star } from "lucide-react";
import Link from "next/link";

import type { PublicMerchant, VerticalType } from "@/lib/api/types";
import { formatMoney } from "@/lib/utils";

const HERO_BY_VERTICAL: Record<VerticalType, string> = {
  Food: "hero-food",
  Pharmacy: "hero-pharmacy",
  Grocery: "hero-grocery",
  Parcel: "hero-parcel",
};

const LABEL: Record<VerticalType, string> = {
  Food: "Comida",
  Pharmacy: "Farmacia",
  Grocery: "Súper",
  Parcel: "Envíos",
};

function pseudoHash(s: string) {
  let h = 0;
  for (let i = 0; i < s.length; i++) h = (h * 31 + s.charCodeAt(i)) >>> 0;
  return h;
}

export function MerchantHero({ merchant }: { merchant: PublicMerchant }) {
  const h = pseudoHash(merchant.id);
  const eta = 15 + (h % 35);
  const distanceKm = 0.4 + ((h % 50) / 10);
  const deliveryFee = 39 + (h % 60);
  const rating = merchant.averageRating ?? Number((4.2 + ((h % 7) / 10)).toFixed(1));
  const totalReviews = merchant.totalReviews || 800 + (h % 1200);

  return (
    <section>
      <div className={`relative ${HERO_BY_VERTICAL[merchant.verticalType]} stripes h-44 md:h-56 lg:h-64 rounded-b-2xl`}>
        <Link
          href="/"
          className="absolute top-4 left-4 md:top-6 md:left-6 inline-flex h-10 w-10 items-center justify-center rounded-full bg-white shadow-md text-foreground hover:scale-105 transition-transform"
          aria-label="Volver"
        >
          <ArrowLeft className="h-5 w-5" />
        </Link>
        <div className="absolute left-4 md:left-6 bottom-3 text-[10px] uppercase tracking-widest text-white/80 font-semibold">
          Foto principal — {merchant.name}
        </div>
      </div>

      <div className="container -mt-6 md:-mt-8">
        <div className="rounded-2xl bg-white border border-border shadow-sm p-5 md:p-6 grid md:grid-cols-[1fr_auto] gap-5 md:gap-8 items-start">
          <div>
            <h1 className="text-2xl md:text-3xl font-bold tracking-tight">{merchant.name}</h1>
            <div className="mt-2 flex flex-wrap items-center gap-x-3 gap-y-1 text-sm">
              <span className="inline-flex items-center gap-1 font-semibold text-foreground">
                <Star className="h-4 w-4 fill-accent text-accent" />
                {rating.toFixed(1)}
                <span className="text-muted-foreground font-normal">({totalReviews.toLocaleString("es-DO")})</span>
              </span>
              <span className="text-muted-foreground">·</span>
              <span className="text-muted-foreground">{LABEL[merchant.verticalType]}</span>
              <span className="text-muted-foreground">·</span>
              <span className="text-muted-foreground">{distanceKm.toFixed(1)} km</span>
            </div>
            {merchant.description && (
              <p className="mt-3 text-sm md:text-base text-foreground/80 max-w-xl">{merchant.description}</p>
            )}
          </div>
          <div className="grid grid-cols-2 gap-3 md:flex md:gap-0 md:divide-x md:divide-border">
            <div className="rounded-lg border border-border md:border-0 md:rounded-none md:px-6 px-4 py-3 text-center md:text-right">
              <div className="text-[10px] tracking-widest uppercase text-muted-foreground">Envío</div>
              <div className="font-bold text-lg">{formatMoney(deliveryFee)}</div>
            </div>
            <div className="rounded-lg border border-border md:border-0 md:rounded-none md:pl-6 px-4 py-3 text-center md:text-left">
              <div className="text-[10px] tracking-widest uppercase text-muted-foreground">Llegará en</div>
              <div className="font-bold text-lg flex items-center md:justify-start justify-center gap-1">
                <Clock className="h-4 w-4 text-muted-foreground" />
                {eta} min
              </div>
            </div>
          </div>
        </div>
      </div>
    </section>
  );
}
