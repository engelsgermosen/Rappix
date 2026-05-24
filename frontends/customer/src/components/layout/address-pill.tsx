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
import { useAuthStore } from "@/lib/auth/store";

/**
 * Header pill — appears ONLY when a Customer is logged in. Reads from
 * the per-user address store (each Customer has their own address book
 * keyed by userId).
 *
 * Render contract:
 *   - Not hydrated yet OR no user        → renders nothing (null)
 *   - Logged in, no saved addresses      → "Elige tu dirección"
 *   - Logged in, has default address     → shows it + dropdown
 */
export function AddressPill() {
  const user = useAuthStore((s) => s.user);
  const authHydrated = useAuthStore((s) => s.hydrated);
  const addressesHydrated = useAddressStore((s) => s.hydrated);
  const addresses = useAddressStore((s) => s.addresses);
  const defaultAddress = useAddressStore((s) => s.defaultAddress);
  const setDefault = useAddressStore((s) => s.setDefault);
  const [dialogOpen, setDialogOpen] = useState(false);

  // SSR + first paint stability: don't render anything until both stores
  // have hydrated, so we never flash a logged-in pill on the public
  // routes that someone might bookmark.
  if (!authHydrated || !addressesHydrated) return null;
  // Anonymous browsing → no address pill at all.
  if (!user) return null;

  const label = defaultAddress
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
