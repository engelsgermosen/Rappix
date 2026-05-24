"use client";

import { useEffect, useState } from "react";
import { RefreshCw } from "lucide-react";

import { Button } from "@/components/ui/button";

/**
 * Tiny banner showing "Auto-actualización cada 10 s · Próxima en X s · Refrescar ahora".
 * `dataUpdatedAt` comes from TanStack Query's `query.dataUpdatedAt` (epoch ms);
 * the countdown is derived locally.
 */
export function RefreshBanner({
  intervalMs,
  dataUpdatedAt,
  onRefresh,
  refreshing,
}: {
  intervalMs: number;
  dataUpdatedAt: number;
  onRefresh: () => void;
  refreshing: boolean;
}) {
  const [now, setNow] = useState(() => Date.now());
  useEffect(() => {
    const id = window.setInterval(() => setNow(Date.now()), 1000);
    return () => window.clearInterval(id);
  }, []);

  const elapsed = Math.max(0, now - dataUpdatedAt);
  const remaining = Math.max(0, Math.ceil((intervalMs - elapsed) / 1000));
  const intervalSec = Math.round(intervalMs / 1000);

  return (
    <div className="flex flex-wrap items-center justify-between gap-3 rounded-lg border border-brand-100 bg-brand-50/40 px-4 py-2.5">
      <div className="flex items-center gap-2 text-sm text-foreground/80">
        <span className="h-1.5 w-1.5 animate-pulse rounded-full bg-brand" />
        <span className="font-semibold text-brand">Auto-actualización cada {intervalSec} s</span>
        <span className="text-muted-foreground">· Próxima en {remaining}s</span>
      </div>
      <Button type="button" variant="ghost" size="sm" onClick={onRefresh} disabled={refreshing}>
        <RefreshCw className={refreshing ? "h-3.5 w-3.5 animate-spin" : "h-3.5 w-3.5"} />
        {refreshing ? "Actualizando..." : "Refrescar ahora"}
      </Button>
    </div>
  );
}
