"use client";

import { Banknote, CreditCard, Wallet } from "lucide-react";

import { cn } from "@/lib/utils";

export type PaymentMethodId = "card-visa" | "card-mastercard" | "cash" | "wallet";

const METHODS: { id: PaymentMethodId; label: string; sub: string; Icon: React.ComponentType<{ className?: string }> }[] = [
  { id: "card-visa",       label: "Visa •••• 4242",       sub: "Pago seguro por Stripe", Icon: CreditCard },
  { id: "card-mastercard", label: "Mastercard •••• 5454", sub: "Pago seguro por Stripe", Icon: CreditCard },
  { id: "cash",            label: "Efectivo al recibir",   sub: "Entrega en pesos dominicanos", Icon: Banknote },
  { id: "wallet",          label: "Wallet Rappix",         sub: "Saldo y promociones",          Icon: Wallet },
];

export function PaymentPicker({
  value,
  onChange,
}: {
  value: PaymentMethodId;
  onChange: (id: PaymentMethodId) => void;
}) {
  return (
    <div className="grid sm:grid-cols-2 gap-3">
      {METHODS.map((m) => {
        const active = m.id === value;
        return (
          <button
            key={m.id}
            type="button"
            onClick={() => onChange(m.id)}
            className={cn(
              "flex items-center gap-3 rounded-xl border p-3 text-left transition-colors",
              active ? "border-brand bg-brand-50/50 ring-2 ring-brand/20" : "border-border bg-white hover:border-brand/40",
            )}
          >
            <span
              className={cn(
                "inline-flex h-9 w-9 items-center justify-center rounded-lg",
                active ? "bg-brand text-white" : "bg-brand-100/70 text-brand-700",
              )}
            >
              <m.Icon className="h-4 w-4" />
            </span>
            <div className="min-w-0 flex-1">
              <div className="font-semibold text-sm truncate">{m.label}</div>
              <div className="text-xs text-muted-foreground truncate">{m.sub}</div>
            </div>
            <span
              className={cn(
                "h-4 w-4 rounded-full border-2 flex-shrink-0",
                active ? "border-brand bg-brand" : "border-muted-foreground/30",
              )}
            />
          </button>
        );
      })}
    </div>
  );
}
