"use client";

import { ArrowLeft, MapPin, Pencil, Plus, Star, Trash2 } from "lucide-react";
import Link from "next/link";
import { useState } from "react";
import { toast } from "sonner";

import { AddressFormDialog } from "@/components/addresses/address-form-dialog";
import { RequireAuth } from "@/components/auth/require-auth";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { useAddressStore } from "@/lib/addresses";
import type { SavedAddress } from "@/lib/addresses";

export default function AddressesPage() {
  return (
    <RequireAuth fallback={<div className="container py-16 text-center text-muted-foreground">Cargando…</div>}>
      <AddressesContent />
    </RequireAuth>
  );
}

type DialogState = { kind: "closed" } | { kind: "create" } | { kind: "edit"; address: SavedAddress };

function AddressesContent() {
  const addresses = useAddressStore((s) => s.addresses);
  const remove = useAddressStore((s) => s.remove);
  const setDefault = useAddressStore((s) => s.setDefault);
  const [dialog, setDialog] = useState<DialogState>({ kind: "closed" });

  function onRemove(a: SavedAddress) {
    const confirmMsg = `¿Eliminar la dirección "${a.label || a.street}"?`;
    if (!window.confirm(confirmMsg)) return;
    remove(a.id);
    toast.success("Dirección eliminada");
  }

  return (
    <div className="container py-8 md:py-12 max-w-3xl animate-fade-in">
      <Link
        href="/cuenta"
        className="inline-flex items-center gap-2 text-sm text-muted-foreground hover:text-foreground transition-colors"
      >
        <ArrowLeft className="h-4 w-4" /> Mi cuenta
      </Link>

      <div className="mt-3 flex flex-wrap items-end justify-between gap-3">
        <div>
          <h1 className="text-2xl md:text-3xl font-bold tracking-tight">Mis direcciones</h1>
          <p className="text-sm text-muted-foreground mt-1">
            Guarda dónde quieres recibir tus pedidos. Se almacenan en este navegador.
          </p>
        </div>
        <Button onClick={() => setDialog({ kind: "create" })} className="gap-2">
          <Plus className="h-4 w-4" /> Añadir dirección
        </Button>
      </div>

      {addresses.length === 0 ? (
        <div className="mt-8 rounded-2xl border border-dashed border-border bg-white p-10 text-center">
          <div className="mx-auto h-14 w-14 rounded-full bg-brand-100 inline-flex items-center justify-center mb-4">
            <MapPin className="h-6 w-6 text-brand-700" />
          </div>
          <h2 className="font-bold">Sin direcciones guardadas</h2>
          <p className="text-sm text-muted-foreground mt-1 max-w-md mx-auto">
            Añade una dirección para que el pedido llegue al lugar correcto y para calcular el envío.
          </p>
          <Button className="mt-5 gap-2" onClick={() => setDialog({ kind: "create" })}>
            <Plus className="h-4 w-4" /> Añadir mi primera dirección
          </Button>
        </div>
      ) : (
        <ul className="mt-6 space-y-3">
          {addresses.map((a) => (
            <li key={a.id} className="rounded-xl border border-border bg-white p-4 md:p-5">
              <div className="flex items-start gap-4">
                <div className="h-11 w-11 rounded-xl bg-brand-100/70 text-brand-700 inline-flex items-center justify-center flex-shrink-0">
                  <MapPin className="h-5 w-5" />
                </div>
                <div className="min-w-0 flex-1">
                  <div className="flex flex-wrap items-center gap-2">
                    <span className="font-semibold">{a.label || "Sin etiqueta"}</span>
                    {a.isDefault && <Badge variant="default">Predeterminada</Badge>}
                  </div>
                  <div className="text-sm text-foreground/80 mt-0.5">{a.street}</div>
                  {a.reference && (
                    <div className="text-xs text-muted-foreground mt-0.5">{a.reference}</div>
                  )}
                  <div className="text-[11px] text-muted-foreground/80 mt-1 font-mono">
                    {a.latitude.toFixed(4)}, {a.longitude.toFixed(4)}
                  </div>
                </div>
                <div className="flex flex-col sm:flex-row gap-2 flex-shrink-0">
                  {!a.isDefault && (
                    <Button
                      size="sm"
                      variant="outline"
                      className="gap-1.5"
                      onClick={() => {
                        setDefault(a.id);
                        toast.success("Predeterminada actualizada");
                      }}
                    >
                      <Star className="h-3.5 w-3.5" />
                      <span className="hidden sm:inline">Predeterminar</span>
                    </Button>
                  )}
                  <Button
                    size="sm"
                    variant="outline"
                    className="gap-1.5"
                    onClick={() => setDialog({ kind: "edit", address: a })}
                  >
                    <Pencil className="h-3.5 w-3.5" />
                    <span className="hidden sm:inline">Editar</span>
                  </Button>
                  <Button
                    size="sm"
                    variant="outline"
                    className="gap-1.5 text-red-600 hover:bg-red-50 hover:text-red-700"
                    onClick={() => onRemove(a)}
                  >
                    <Trash2 className="h-3.5 w-3.5" />
                    <span className="hidden sm:inline">Eliminar</span>
                  </Button>
                </div>
              </div>
            </li>
          ))}
        </ul>
      )}

      <AddressFormDialog
        open={dialog.kind !== "closed"}
        onOpenChange={(open) => !open && setDialog({ kind: "closed" })}
        mode={dialog.kind === "edit" ? { kind: "edit", address: dialog.address } : { kind: "create" }}
      />
    </div>
  );
}
