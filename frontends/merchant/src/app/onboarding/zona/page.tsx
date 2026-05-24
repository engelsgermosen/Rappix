"use client";

import { useRouter } from "next/navigation";
import { useEffect, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";

import { StepShell } from "@/components/onboarding/step-shell";
import { ServiceAreaMap } from "@/components/maps/service-area-map-loader";
import { Label } from "@/components/ui/label";
import { Slider } from "@/components/ui/slider";
import { addServiceArea, getMyMerchant, removeServiceArea } from "@/lib/api/merchants-me";
import { env } from "@/lib/env";
import { describeError } from "@/lib/errors";
import { formatDistanceKm } from "@/lib/utils";

const MIN_RADIUS_M = 500;
const MAX_RADIUS_M = 10_000;
const DEFAULT_RADIUS_M = 2_400;

export default function ZonaPage() {
  const router = useRouter();
  const queryClient = useQueryClient();
  const merchant = useQuery({ queryKey: ["merchant", "me"], queryFn: getMyMerchant });

  const [centerLat, setCenterLat] = useState(env.defaultLat);
  const [centerLng, setCenterLng] = useState(env.defaultLng);
  const [radius, setRadius] = useState(DEFAULT_RADIUS_M);
  const [recenterToken, setRecenterToken] = useState(0);

  // Prefill from the first existing Circle (MVP: only Circle areas).
  useEffect(() => {
    if (!merchant.data) return;
    const circle = merchant.data.serviceAreas.find(
      (a) => a.type === "Circle" && a.centerLatitude != null && a.centerLongitude != null && a.radiusMeters,
    );
    if (circle) {
      setCenterLat(circle.centerLatitude!);
      setCenterLng(circle.centerLongitude!);
      setRadius(circle.radiusMeters!);
      setRecenterToken((t) => t + 1);
    }
  }, [merchant.data]);

  const save = useMutation({
    mutationFn: async () => {
      // The backend has no "update area" — to change the circle we delete the
      // existing Circle areas and POST a fresh one. The Draft merchant has
      // at most a few; this is fine.
      if (merchant.data) {
        for (const area of merchant.data.serviceAreas) {
          if (area.type === "Circle") {
            await removeServiceArea(area.id);
          }
        }
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
      router.push("/onboarding/ubicacion");
    },
    onError: (err) => toast.error(describeError(err)),
  });

  return (
    <StepShell
      stepNumber={3}
      title="Zona de servicio"
      description="Define el área donde puedes entregar. Solo recibirás pedidos de clientes dentro de esta zona."
      onBack={() => router.push("/onboarding/horarios")}
      onContinue={() => save.mutate()}
      continueLabel={save.isPending ? "Guardando..." : "Continuar"}
      continueDisabled={save.isPending}
    >
      <div className="space-y-5">
        <div className="relative">
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
          <div className="absolute bottom-3 left-3 inline-flex items-center gap-2 rounded-full bg-white px-3 py-1.5 text-xs font-semibold shadow-md">
            <span className="h-2 w-2 rounded-full bg-brand" />
            Radio {formatDistanceKm(radius / 1000)} · Arrastra para ajustar
          </div>
        </div>

        <div className="grid gap-5 md:grid-cols-2">
          <div className="space-y-1.5">
            <Label>Tipo de zona</Label>
            <div className="flex h-11 items-center rounded-lg border border-border bg-muted/40 px-3 text-sm text-foreground/70">
              Círculo (radio fijo)
            </div>
          </div>
          <div className="space-y-2">
            <div className="flex items-center justify-between">
              <Label>Radio</Label>
              <span className="text-sm font-semibold text-foreground">{formatDistanceKm(radius / 1000)}</span>
            </div>
            <Slider
              min={MIN_RADIUS_M}
              max={MAX_RADIUS_M}
              step={100}
              value={[radius]}
              onValueChange={(v) => setRadius(v[0]!)}
            />
            <p className="text-xs text-muted-foreground">Distancia desde tu ubicación de recogida.</p>
          </div>
        </div>
      </div>
    </StepShell>
  );
}
