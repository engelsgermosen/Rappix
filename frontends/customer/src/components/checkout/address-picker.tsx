"use client";

import { Check, MapPin, Plus } from "lucide-react";
import { useState } from "react";

import { AddressFormDialog } from "@/components/addresses/address-form-dialog";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { useAddressStore } from "@/lib/addresses";
import { cn } from "@/lib/utils";

/**
 * Inline picker for /checkout — lists saved addresses with single-select
 * radios + an "Añadir nueva" CTA that opens the same dialog used by the
 * header/dropdown and /direcciones. On save, auto-selects the new address.
 */
export function AddressPicker({
  selectedId,
  onSelect,
}: {
  selectedId: string | null;
  onSelect: (id: string) => void;
}) {
  const addresses = useAddressStore((s) => s.addresses);
  const [dialogOpen, setDialogOpen] = useState(false);

  if (addresses.length === 0) {
    return (
      <div className="rounded-lg border border-dashed border-border bg-white p-5 text-center">
        <div className="font-semibold">Aún no tienes direcciones</div>
        <p className="text-sm text-muted-foreground mt-1">
          Añade una para indicar dónde recibir tu pedido.
        </p>
        <Button className="mt-3 gap-2" onClick={() => setDialogOpen(true)}>
          <Plus className="h-4 w-4" /> Añadir dirección
        </Button>
        <AddressFormDialog
          open={dialogOpen}
          onOpenChange={setDialogOpen}
          mode={{ kind: "create" }}
          onSaved={(saved) => onSelect(saved.id)}
        />
      </div>
    );
  }

  return (
    <div className="space-y-2">
      {addresses.map((a) => {
        const active = selectedId === a.id;
        return (
          <button
            key={a.id}
            type="button"
            onClick={() => onSelect(a.id)}
            className={cn(
              "w-full text-left rounded-lg border p-3 transition-colors flex items-start gap-3",
              active
                ? "border-brand bg-brand-50/40 ring-2 ring-brand/20"
                : "border-border bg-white hover:border-brand/40",
            )}
          >
            <span
              className={cn(
                "mt-0.5 inline-flex h-5 w-5 items-center justify-center rounded-full border-2 flex-shrink-0",
                active ? "border-brand bg-brand text-white" : "border-muted-foreground/30",
              )}
            >
              {active && <Check className="h-3 w-3" />}
            </span>
            <span className="inline-flex h-9 w-9 items-center justify-center rounded-lg bg-brand-100/70 text-brand-700 flex-shrink-0">
              <MapPin className="h-4 w-4" />
            </span>
            <div className="min-w-0 flex-1">
              <div className="flex items-center gap-2 flex-wrap">
                <span className="font-semibold text-sm">{a.label || "Sin etiqueta"}</span>
                {a.isDefault && <Badge variant="default">Predeterminada</Badge>}
              </div>
              <div className="text-xs text-foreground/70 truncate">{a.street}</div>
              {a.reference && <div className="text-xs text-muted-foreground truncate">{a.reference}</div>}
            </div>
          </button>
        );
      })}
      <button
        type="button"
        className="inline-flex items-center gap-2 text-sm font-medium text-brand hover:underline mt-1"
        onClick={() => setDialogOpen(true)}
      >
        <Plus className="h-4 w-4" /> Añadir nueva dirección
      </button>
      <AddressFormDialog
        open={dialogOpen}
        onOpenChange={setDialogOpen}
        mode={{ kind: "create" }}
        onSaved={(saved) => onSelect(saved.id)}
      />
    </div>
  );
}
