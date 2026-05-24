"use client";

import { useRouter, useSearchParams } from "next/navigation";
import { Suspense, useMemo } from "react";

import { NearbyGrid } from "@/components/merchants/nearby-grid";
import { PromoBanner } from "@/components/merchants/promo-banner";
import { QuickVerticalTiles } from "@/components/merchants/quick-vertical-tile";
import { VerticalChips, type VerticalFilter } from "@/components/merchants/vertical-chips";
import type { VerticalType } from "@/lib/api/types";

function parseVertical(v: string | null): VerticalFilter {
  if (v === "Food" || v === "Pharmacy" || v === "Grocery" || v === "Parcel") return v;
  return "all";
}

export default function HomePage() {
  return (
    <Suspense fallback={<div className="container py-16 text-center text-muted-foreground">Cargando…</div>}>
      <Home />
    </Suspense>
  );
}

function Home() {
  const router = useRouter();
  const params = useSearchParams();
  const vertical = parseVertical(params.get("vertical"));
  const q = params.get("q") ?? undefined;

  const apiVertical = useMemo<VerticalType | undefined>(
    () => (vertical === "all" ? undefined : vertical),
    [vertical],
  );

  function setVertical(next: VerticalFilter) {
    const search = new URLSearchParams(params);
    if (next === "all") search.delete("vertical");
    else search.set("vertical", next);
    router.push(`/?${search.toString()}`, { scroll: false });
  }

  return (
    <div className="container py-6 md:py-8 space-y-6 md:space-y-8 animate-fade-in">
      <PromoBanner />
      <VerticalChips active={vertical} onChange={setVertical} />
      <QuickVerticalTiles onSelect={(v) => setVertical(v)} />

      <section>
        <div className="flex items-end justify-between mb-4 md:mb-5">
          <div>
            <h2 className="text-xl md:text-2xl font-bold tracking-tight">Cerca de ti</h2>
            <p className="text-sm text-muted-foreground">Comercios disponibles en tu zona</p>
          </div>
          <button className="text-sm text-brand font-medium hover:underline">Ver todo →</button>
        </div>
        <NearbyGrid vertical={apiVertical} q={q} />
      </section>
    </div>
  );
}
