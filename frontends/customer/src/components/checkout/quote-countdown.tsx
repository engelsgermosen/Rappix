"use client";

import { Clock } from "lucide-react";
import { useEffect, useState } from "react";

import { cn } from "@/lib/utils";

export function QuoteCountdown({ expiresAtUtc, onExpire }: { expiresAtUtc: string; onExpire?: () => void }) {
  const [remaining, setRemaining] = useState<number>(() => Math.max(0, new Date(expiresAtUtc).getTime() - Date.now()));

  useEffect(() => {
    const id = window.setInterval(() => {
      const r = Math.max(0, new Date(expiresAtUtc).getTime() - Date.now());
      setRemaining(r);
      if (r === 0) {
        window.clearInterval(id);
        onExpire?.();
      }
    }, 1000);
    return () => window.clearInterval(id);
  }, [expiresAtUtc, onExpire]);

  const minutes = Math.floor(remaining / 60_000);
  const seconds = Math.floor((remaining % 60_000) / 1000);
  const isWarning = remaining < 60_000;

  return (
    <div
      className={cn(
        "inline-flex items-center gap-1.5 rounded-md px-2.5 py-1 text-xs font-medium",
        isWarning ? "bg-red-50 text-red-700" : "bg-amber-50 text-amber-700",
      )}
    >
      <Clock className="h-3.5 w-3.5" />
      {remaining === 0 ? (
        <span>Cotización expirada</span>
      ) : (
        <span>
          Cotización válida por {String(minutes).padStart(2, "0")}:{String(seconds).padStart(2, "0")}
        </span>
      )}
    </div>
  );
}
