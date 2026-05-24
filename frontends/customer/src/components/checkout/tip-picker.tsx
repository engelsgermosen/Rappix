"use client";

import { cn } from "@/lib/utils";

const PRESETS = [0, 30, 50, 80, 120];

export function TipPicker({ value, onChange }: { value: number; onChange: (n: number) => void }) {
  return (
    <div className="flex flex-wrap gap-2">
      {PRESETS.map((n) => {
        const active = value === n;
        return (
          <button
            key={n}
            type="button"
            onClick={() => onChange(n)}
            className={cn(
              "h-10 rounded-full border px-4 text-sm font-medium transition-colors",
              active ? "border-brand bg-brand text-white" : "border-border bg-white hover:border-brand/40",
            )}
          >
            {n === 0 ? "Sin propina" : `RD$ ${n}`}
          </button>
        );
      })}
    </div>
  );
}
