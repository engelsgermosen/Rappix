"use client";

import { Locate, MapPin } from "lucide-react";
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

import { AddressMapPicker } from "./address-map-picker-loader";

type Mode = { kind: "create" } | { kind: "edit"; address: SavedAddress };

/**
 * Add-or-edit address dialog.
 *
 * The lat/lng come from an INTERACTIVE MAP — the user clicks (or drags
 * the marker, or hits "Usar mi ubicación") instead of typing numbers.
 * Behind the scenes the coords are still stored verbatim in the address
 * store so the order body keeps working unchanged.
 *
 * Geolocation: the "Usar mi ubicación actual" button calls
 * navigator.geolocation.getCurrentPosition. The browser shows its own
 * permission prompt. We handle all three branches (granted / denied /
 * unsupported / context-not-secure) gracefully.
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
  const [lat, setLat] = useState<number>(env.defaultLat);
  const [lng, setLng] = useState<number>(env.defaultLng);
  // Bumped whenever we want the map to fly to a new lat/lng programmatically
  // (open, edit-load, geolocation success). Internal clicks/drags don't bump.
  const [recenterToken, setRecenterToken] = useState(0);
  const [locating, setLocating] = useState(false);

  // Reset / preload on every open or mode swap.
  useEffect(() => {
    if (!open) return;
    if (mode.kind === "edit") {
      setLabel(mode.address.label ?? "");
      setStreet(mode.address.street);
      setReference(mode.address.reference ?? "");
      setLat(mode.address.latitude);
      setLng(mode.address.longitude);
    } else {
      setLabel("");
      setStreet("");
      setReference("");
      setLat(env.defaultLat);
      setLng(env.defaultLng);
    }
    setRecenterToken((t) => t + 1);
  }, [open, mode]);

  function useMyLocation() {
    if (typeof window === "undefined") return;
    if (!("geolocation" in navigator)) {
      toast.error("Tu navegador no soporta geolocalización");
      return;
    }
    // Secure-context check: most browsers refuse geolocation over plain http
    // except on localhost. Surface a friendly error before the API rejects us.
    if (typeof window.isSecureContext === "boolean" && !window.isSecureContext) {
      toast.error("La geolocalización requiere HTTPS o localhost");
      return;
    }
    setLocating(true);
    navigator.geolocation.getCurrentPosition(
      (pos) => {
        setLat(pos.coords.latitude);
        setLng(pos.coords.longitude);
        setRecenterToken((t) => t + 1);
        setLocating(false);
        toast.success("Centramos el mapa en tu ubicación actual");
      },
      (err) => {
        setLocating(false);
        // err.code: 1 PERMISSION_DENIED, 2 POSITION_UNAVAILABLE, 3 TIMEOUT
        const message =
          err.code === 1
            ? "Permiso de ubicación denegado. Puedes elegir el punto en el mapa."
            : err.code === 3
            ? "Se agotó el tiempo de espera. Inténtalo de nuevo."
            : "No pudimos obtener tu ubicación. Elige el punto en el mapa.";
        toast.error(message);
      },
      { enableHighAccuracy: true, timeout: 8000, maximumAge: 60_000 },
    );
  }

  function onSubmit(e: React.FormEvent) {
    e.preventDefault();
    if (street.trim().length === 0) {
      toast.error("La dirección no puede estar vacía");
      return;
    }
    if (!Number.isFinite(lat) || !Number.isFinite(lng)) {
      toast.error("Selecciona un punto en el mapa antes de guardar");
      return;
    }
    const payload = {
      label: label.trim() || null,
      street: street.trim(),
      reference: reference.trim() || null,
      latitude: lat,
      longitude: lng,
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
      <DialogContent className="max-w-xl max-h-[90vh] overflow-y-auto">
        <DialogHeader>
          <DialogTitle>{mode.kind === "edit" ? "Editar dirección" : "Añadir dirección"}</DialogTitle>
          <DialogDescription>
            Haz clic en el mapa o arrastra el marcador para fijar el punto exacto de entrega.
          </DialogDescription>
        </DialogHeader>

        <form className="space-y-4" onSubmit={onSubmit}>
          <div className="grid sm:grid-cols-[1fr_auto] gap-3 items-end">
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
            <Button
              type="button"
              variant="outline"
              onClick={useMyLocation}
              disabled={locating}
              className="gap-2 w-full sm:w-auto"
            >
              <Locate className={locating ? "h-4 w-4 animate-spin" : "h-4 w-4"} />
              {locating ? "Buscando…" : "Usar mi ubicación"}
            </Button>
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

          <div className="space-y-1.5">
            <Label>Punto de entrega en el mapa</Label>
            <AddressMapPicker
              latitude={lat}
              longitude={lng}
              recenterToken={recenterToken}
              onChange={(nextLat, nextLng) => {
                setLat(nextLat);
                setLng(nextLng);
              }}
            />
            <p className="text-xs text-muted-foreground inline-flex items-center gap-1.5 font-mono">
              <MapPin className="h-3 w-3" />
              {lat.toFixed(5)}, {lng.toFixed(5)}
            </p>
          </div>

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
