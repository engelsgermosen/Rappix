"use client";

import { useEffect, useState } from "react";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { Pencil } from "lucide-react";
import { toast } from "sonner";

import { ServiceAreaMap } from "@/components/maps/service-area-map-loader";
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
import { Label } from "@/components/ui/label";
import { Slider } from "@/components/ui/slider";
import { addServiceArea, removeServiceArea } from "@/lib/api/merchants-me";
import { env } from "@/lib/env";
import { describeError } from "@/lib/errors";
import { formatDistanceKm } from "@/lib/utils";
import type { MerchantResponse } from "@/lib/api/types";

const MIN = 500;
const MAX = 10_000;

export function ZonaCard({ merchant }: { merchant: MerchantResponse }) {
  const queryClient = useQueryClient();
  const [open, setOpen] = useState(false);

  const circle = merchant.serviceAreas.find(
    (a) => a.type === "Circle" && a.centerLatitude != null && a.centerLongitude != null && a.radiusMeters,
  );

  const [centerLat, setCenterLat] = useState(circle?.centerLatitude ?? env.defaultLat);
  const [centerLng, setCenterLng] = useState(circle?.centerLongitude ?? env.defaultLng);
  const [radius, setRadius] = useState(circle?.radiusMeters ?? 2_400);
  const [recenterToken, setRecenterToken] = useState(0);

  useEffect(() => {
    if (circle) {
      setCenterLat(circle.centerLatitude!);
      setCenterLng(circle.centerLongitude!);
      setRadius(circle.radiusMeters!);
      setRecenterToken((t) => t + 1);
    }
  }, [circle]);

  const save = useMutation({
    mutationFn: async () => {
      for (const area of merchant.serviceAreas) {
        if (area.type === "Circle") await removeServiceArea(area.id);
      }
      return addServiceArea({
        type: "Circle",
        centerLatitude: centerLat,
        centerLongitude: centerLng,
        radiusMeters: Math.round(radius),
      });
    },
    onSuccess: (data) => {
      queryClient.setQueryData(["merchant", "me"], data);
      toast.success("Zona de servicio actualizada.");
      setOpen(false);
    },
    onError: (err) => toast.error(describeError(err)),
  });

  const zonesConfigured = merchant.serviceAreas.length;

  return (
    <Card className="overflow-hidden">
      <div className="flex flex-wrap items-end justify-between gap-3 border-b border-border px-6 py-4">
        <div>
          <h2 className="text-lg font-bold text-foreground">Zona de servicio</h2>
          <p className="text-xs text-muted-foreground">Área donde puedes entregar</p>
        </div>
        <Button type="button" variant="outline" size="sm" onClick={() => setOpen(true)}>
          <Pencil className="h-3.5 w-3.5" />
          Editar
        </Button>
      </div>
      <div className="px-6 py-5">
        <div className="overflow-hidden rounded-lg border border-border">
          <ServiceAreaMap
            centerLat={centerLat}
            centerLng={centerLng}
            radiusMeters={radius}
            recenterToken={recenterToken}
            onChange={() => {}}
            interactive={false}
            className="h-40"
          />
        </div>
        <p className="mt-3 text-xs text-muted-foreground">
          Radio actual: <span className="font-semibold text-foreground">{formatDistanceKm(radius / 1000)}</span> ·{" "}
          {zonesConfigured} {zonesConfigured === 1 ? "zona configurada" : "zonas configuradas"}
        </p>
      </div>

      <Dialog open={open} onOpenChange={setOpen}>
        <DialogContent className="max-w-2xl">
          <DialogHeader>
            <DialogTitle>Editar zona de servicio</DialogTitle>
            <DialogDescription>
              Arrastra el centro o el handle para ajustar el área. Cambia el radio con el slider.
            </DialogDescription>
          </DialogHeader>
          <ServiceAreaMap
            centerLat={centerLat}
            centerLng={centerLng}
            radiusMeters={radius}
            recenterToken={recenterToken}
            onChange={({ centerLat, centerLng, radiusMeters }) => {
              setCenterLat(centerLat);
              setCenterLng(centerLng);
              setRadius(radiusMeters);
            }}
          />
          <div className="space-y-2">
            <div className="flex items-center justify-between">
              <Label>Radio</Label>
              <span className="text-sm font-semibold">{formatDistanceKm(radius / 1000)}</span>
            </div>
            <Slider
              min={MIN}
              max={MAX}
              step={100}
              value={[radius]}
              onValueChange={(v) => setRadius(v[0]!)}
            />
          </div>
          <DialogFooter>
            <Button type="button" variant="outline" onClick={() => setOpen(false)}>
              Cancelar
            </Button>
            <Button type="button" onClick={() => save.mutate()} disabled={save.isPending}>
              {save.isPending ? "Guardando..." : "Guardar cambios"}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </Card>
  );
}
