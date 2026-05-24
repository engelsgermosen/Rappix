"use client";

import { Check, ChevronDown, MapPin, Plus, Settings2 } from "lucide-react";
import Link from "next/link";
import { useState } from "react";

import { AddressFormDialog } from "@/components/addresses/address-form-dialog";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import { useAddressStore } from "@/lib/addresses";

/**
 * Header pill — reads the default address from the local store. Shows
 * "Elige tu dirección" when there are none (NO hardcoded fallback).
 *
 * Clicking opens a dropdown with the saved list + actions to switch
 * default, add a new one (inline dialog), or manage all (→ /direcciones).
 */
export function AddressPill() {
  const addresses = useAddressStore((s) => s.addresses);
  const defaultAddress = useAddressStore((s) => s.defaultAddress);
  const setDefault = useAddressStore((s) => s.setDefault);
  const hydrated = useAddressStore((s) => s.hydrated);
  const [dialogOpen, setDialogOpen] = useState(false);

  // Until hydration completes (first client tick) show a stable placeholder
  // so SSR + first paint don't diverge.
  const label = !hydrated
    ? "Cargando…"
    : defaultAddress
    ? defaultAddress.label || defaultAddress.street
    : "Elige tu dirección";

  return (
    <>
      <DropdownMenu>
        <DropdownMenuTrigger asChild>
          <button
            className="inline-flex h-10 max-w-[260px] md:max-w-[320px] items-center gap-2 rounded-full bg-white/10 px-3 text-sm text-white hover:bg-white/20 transition-colors focus:outline-none focus:ring-2 focus:ring-white/40"
            type="button"
          >
            <MapPin className="h-4 w-4 flex-shrink-0" />
            <span className="truncate font-medium">{label}</span>
            <ChevronDown className="h-4 w-4 flex-shrink-0 opacity-70" />
          </button>
        </DropdownMenuTrigger>
        <DropdownMenuContent align="start" className="w-[320px]">
          <DropdownMenuLabel>Direcciones de entrega</DropdownMenuLabel>
          {addresses.length === 0 ? (
            <div className="px-3 py-4 text-sm text-muted-foreground">
              Aún no tienes direcciones guardadas. Añade una para empezar a pedir.
            </div>
          ) : (
            addresses.map((a) => (
              <DropdownMenuItem
                key={a.id}
                className="flex-col items-start gap-0.5 cursor-pointer"
                onClick={() => setDefault(a.id)}
              >
                <div className="flex w-full items-center justify-between">
                  <span className="font-semibold">{a.label || "Sin etiqueta"}</span>
                  {a.isDefault && <Check className="h-4 w-4 text-brand" />}
                </div>
                <span className="text-xs text-muted-foreground truncate w-full">{a.street}</span>
              </DropdownMenuItem>
            ))
          )}
          <DropdownMenuSeparator />
          <DropdownMenuItem onClick={() => setDialogOpen(true)} className="cursor-pointer">
            <Plus className="h-4 w-4" />
            Añadir dirección
          </DropdownMenuItem>
          <DropdownMenuItem asChild>
            <Link href="/direcciones" className="cursor-pointer">
              <Settings2 className="h-4 w-4" />
              Gestionar todas
            </Link>
          </DropdownMenuItem>
        </DropdownMenuContent>
      </DropdownMenu>

      <AddressFormDialog open={dialogOpen} onOpenChange={setDialogOpen} mode={{ kind: "create" }} />
    </>
  );
}
