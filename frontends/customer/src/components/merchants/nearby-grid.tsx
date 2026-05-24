"use client";

import { useQuery } from "@tanstack/react-query";
import { AlertCircle } from "lucide-react";

import { merchantsApi } from "@/lib/api/merchants";
import type { NearbyMerchant, VerticalType } from "@/lib/api/types";
import { env } from "@/lib/env";

import { MerchantCard, MerchantCardSkeleton } from "./merchant-card";

export function NearbyGrid({
  vertical,
  q,
}: {
  vertical: VerticalType | undefined;
  q?: string;
}) {
  const query = useQuery({
    queryKey: ["merchants", "nearby", env.defaultLat, env.defaultLng, vertical ?? "all"],
    queryFn: () => merchantsApi.nearby({ lat: env.defaultLat, lng: env.defaultLng, vertical, pageSize: 24 }),
    staleTime: 60_000,
  });

  const merchants: NearbyMerchant[] = query.data ?? [];

  // Client-side substring search by name (Catalog full-text is per-item, not
  // per-merchant). When q is empty we show everything as returned by nearby.
  const filtered = q
    ? merchants.filter((m) => m.name.toLowerCase().includes(q.toLowerCase()))
    : merchants;

  if (query.isLoading) {
    return (
      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4 gap-4 md:gap-5">
        {Array.from({ length: 6 }).map((_, i) => (
          <MerchantCardSkeleton key={i} />
        ))}
      </div>
    );
  }

  if (query.isError) {
    return (
      <div className="flex flex-col items-center justify-center gap-3 rounded-xl border border-dashed border-border bg-white py-16 text-center text-muted-foreground">
        <AlertCircle className="h-8 w-8 text-amber-500" />
        <div className="font-semibold text-foreground">No pudimos cargar los comercios</div>
        <div className="text-sm max-w-md">
          Asegúrate de que el API Gateway esté corriendo en <code>{env.apiUrl}</code> y vuelve a intentar.
        </div>
      </div>
    );
  }

  if (filtered.length === 0) {
    return (
      <div className="rounded-xl border border-dashed border-border bg-white py-16 text-center">
        <div className="text-3xl mb-3">🔍</div>
        <div className="font-semibold text-foreground">Nada en tu zona todavía</div>
        <div className="text-sm text-muted-foreground mt-1">
          {q ? "Prueba con otro término de búsqueda" : "Estamos agregando nuevos comercios cada semana"}
        </div>
      </div>
    );
  }

  return (
    <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4 gap-4 md:gap-5">
      {filtered.map((m) => (
        <MerchantCard key={m.id} merchant={m} />
      ))}
    </div>
  );
}
