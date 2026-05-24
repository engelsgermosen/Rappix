"use client";

import { useEffect, useState } from "react";
import { toast } from "sonner";

import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { env } from "@/lib/env";
import { useAddressStore } from "@/lib/addresses";
import type { SavedAddress } from "@/lib/addresses";

type Mode = { kind: "create" } | { kind: "edit"; address: SavedAddress };

/**
 * Add-or-edit address dialog. Reused by:
 *  - header dropdown (when there are no addresses or the user clicks "Añadir")
 *  - /direcciones management page (add + edit)
 *  - checkout inline picker (add)
 *
 * Coordinates: today the FE has no geocoding, so we default lat/lng to the
 * Santo Domingo center (env-driven) and let the user override. Documented in
 * FINDINGS.md — a real geocoder is a backend/follow-up concern.
 */
export function AddressFormDialog({
  open,
  onOpenChange,
  mode,
  onSaved,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  mode: Mode;
  onSaved?: (address: SavedAddress) => void;
}) {
  const add = useAddressStore((s) => s.add);
  const update = useAddressStore((s) => s.update);

  const [label, setLabel] = useState("");
  const [street, setStreet] = useState("");
  const [reference, setReference] = useState("");
  const [lat, setLat] = useState<string>(String(env.defaultLat));
  const [lng, setLng] = useState<string>(String(env.defaultLng));

  // Reset / preload on every open or mode swap.
  useEffect(() => {
    if (!open) return;
    if (mode.kind === "edit") {
      setLabel(mode.address.label ?? "");
      setStreet(mode.address.street);
      setReference(mode.address.reference ?? "");
      setLat(String(mode.address.latitude));
      setLng(String(mode.address.longitude));
    } else {
      setLabel("");
      setStreet("");
      setReference("");
      setLat(String(env.defaultLat));
      setLng(String(env.defaultLng));
    }
  }, [open, mode]);

  function onSubmit(e: React.FormEvent) {
    e.preventDefault();
    const latitude = Number(lat);
    const longitude = Number(lng);
    if (!Number.isFinite(latitude) || latitude < -90 || latitude > 90) {
      toast.error("Latitud inválida (debe estar entre -90 y 90)");
      return;
    }
    if (!Number.isFinite(longitude) || longitude < -180 || longitude > 180) {
      toast.error("Longitud inválida (debe estar entre -180 y 180)");
      return;
    }
    if (street.trim().length === 0) {
      toast.error("La dirección no puede estar vacía");
      return;
    }
    const payload = {
      label: label.trim() || null,
      street: street.trim(),
      reference: reference.trim() || null,
      latitude,
      longitude,
    };
    const saved =
      mode.kind === "edit" ? update(mode.address.id, payload) : add(payload);
    if (saved) {
      toast.success(mode.kind === "edit" ? "Dirección actualizada" : "Dirección guardada");
      onSaved?.(saved);
      onOpenChange(false);
    }
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>{mode.kind === "edit" ? "Editar dirección" : "Añadir dirección"}</DialogTitle>
          <DialogDescription>
            Tus direcciones se guardan en este navegador. Pronto las podrás sincronizar a tu cuenta.
          </DialogDescription>
        </DialogHeader>

        <form className="space-y-3" onSubmit={onSubmit}>
          <div className="space-y-1.5">
            <Label htmlFor="addr-label">Etiqueta (opcional)</Label>
            <Input
              id="addr-label"
              value={label}
              onChange={(e) => setLabel(e.target.value)}
              placeholder="Casa, Oficina…"
              maxLength={32}
            />
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="addr-street">Dirección</Label>
            <Input
              id="addr-street"
              value={street}
              onChange={(e) => setStreet(e.target.value)}
              required
              placeholder="C/ José Reyes 14, Apto 3B"
              maxLength={200}
              autoFocus
            />
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="addr-reference">Referencia (opcional)</Label>
            <Input
              id="addr-reference"
              value={reference}
              onChange={(e) => setReference(e.target.value)}
              placeholder="Zona Colonial, Santo Domingo"
              maxLength={200}
            />
          </div>
          <div className="grid grid-cols-2 gap-3">
            <div className="space-y-1.5">
              <Label htmlFor="addr-lat">Latitud</Label>
              <Input
                id="addr-lat"
                inputMode="decimal"
                value={lat}
                onChange={(e) => setLat(e.target.value)}
                required
              />
            </div>
            <div className="space-y-1.5">
              <Label htmlFor="addr-lng">Longitud</Label>
              <Input
                id="addr-lng"
                inputMode="decimal"
                value={lng}
                onChange={(e) => setLng(e.target.value)}
                required
              />
            </div>
          </div>
          <p className="text-xs text-muted-foreground">
            Las coordenadas se usan para calcular envío y mostrar la ruta. Si no sabes los valores
            exactos, deja los predeterminados (Santo Domingo) y edítalos luego.
          </p>

          <DialogFooter>
            <Button type="button" variant="outline" onClick={() => onOpenChange(false)}>
              Cancelar
            </Button>
            <Button type="submit">{mode.kind === "edit" ? "Guardar cambios" : "Guardar dirección"}</Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
