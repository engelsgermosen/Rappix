"use client";

import { useEffect, useState } from "react";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { Crosshair, MapPin, Pencil } from "lucide-react";
import { toast } from "sonner";

import { PickupMap } from "@/components/maps/pickup-map-loader";
import { Button } from "@/components/ui/button";
import { Card } from "@/components/ui/card";
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
import { setPickupLocation } from "@/lib/api/merchants-me";
import { env } from "@/lib/env";
import { describeError } from "@/lib/errors";

const ADDRESS_KEY = "rappix.merchant.pickup-address";

export function PickupCard() {
  const queryClient = useQueryClient();
  const [open, setOpen] = useState(false);
  const [lat, setLat] = useState(env.defaultLat);
  const [lng, setLng] = useState(env.defaultLng);
  const [address, setAddress] = useState("");
  const [recenterToken, setRecenterToken] = useState(0);

  useEffect(() => {
    if (typeof window === "undefined") return;
    const stored = window.localStorage.getItem(ADDRESS_KEY);
    if (stored) setAddress(stored);
  }, []);

  function useMyLocation() {
    if (!("geolocation" in navigator)) {
      toast.error("Tu navegador no soporta geolocalización.");
      return;
    }
    navigator.geolocation.getCurrentPosition(
      (pos) => {
        setLat(pos.coords.latitude);
        setLng(pos.coords.longitude);
        setRecenterToken((t) => t + 1);
      },
      () => toast.error("No pudimos obtener tu ubicación."),
    );
  }

  const save = useMutation({
    mutationFn: () => setPickupLocation({ latitude: lat, longitude: lng }),
    onSuccess: (data) => {
      queryClient.setQueryData(["merchant", "me"], data);
      if (typeof window !== "undefined") window.localStorage.setItem(ADDRESS_KEY, address);
      toast.success("Ubicación de recogida actualizada.");
      setOpen(false);
    },
    onError: (err) => toast.error(describeError(err)),
  });

  return (
    <Card className="overflow-hidden">
      <div className="flex flex-wrap items-end justify-between gap-3 border-b border-border px-6 py-4">
        <div>
          <h2 className="text-lg font-bold text-foreground">Ubicación de recogida</h2>
          <p className="text-xs text-muted-foreground">Dónde retiran los repartidores</p>
        </div>
        <Button type="button" variant="outline" size="sm" onClick={() => setOpen(true)}>
          <Pencil className="h-3.5 w-3.5" />
          Editar
        </Button>
      </div>
      <div className="px-6 py-5">
        <div className="overflow-hidden rounded-lg border border-border">
          <PickupMap
            latitude={lat}
            longitude={lng}
            recenterToken={recenterToken}
            onChange={() => {}}
            interactive={false}
            className="h-40"
          />
        </div>
        <p className="mt-3 inline-flex items-center gap-1.5 text-xs text-muted-foreground">
          <MapPin className="h-3.5 w-3.5 text-brand" />
          {address ? address : `${lat.toFixed(5)}, ${lng.toFixed(5)}`}
        </p>
      </div>

      <Dialog open={open} onOpenChange={setOpen}>
        <DialogContent className="max-w-2xl">
          <DialogHeader>
            <DialogTitle>Editar ubicación de recogida</DialogTitle>
            <DialogDescription>
              Arrastra el pin o haz click en el mapa. La dirección de texto se guarda solo en este dispositivo.
            </DialogDescription>
          </DialogHeader>
          <PickupMap
            latitude={lat}
            longitude={lng}
            recenterToken={recenterToken}
            onChange={(la, ln) => {
              setLat(la);
              setLng(ln);
            }}
          />
          <div className="space-y-1.5">
            <Label htmlFor="cfg-pickup-addr">Dirección</Label>
            <Input
              id="cfg-pickup-addr"
              value={address}
              onChange={(e) => setAddress(e.target.value)}
              placeholder="Av. Independencia 245, Gascue, Santo Domingo"
            />
          </div>
          <DialogFooter className="sm:justify-between">
            <Button type="button" variant="ghost" onClick={useMyLocation}>
              <Crosshair className="h-4 w-4" />
              Usar mi ubicación
            </Button>
            <div className="flex gap-2">
              <Button type="button" variant="outline" onClick={() => setOpen(false)}>
                Cancelar
              </Button>
              <Button type="button" onClick={() => save.mutate()} disabled={save.isPending}>
                {save.isPending ? "Guardando..." : "Guardar cambios"}
              </Button>
            </div>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </Card>
  );
}
