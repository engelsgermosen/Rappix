"use client";

import { useRouter } from "next/navigation";
import { useEffect, useState } from "react";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";
import { Crosshair, MapPin } from "lucide-react";

import { StepShell } from "@/components/onboarding/step-shell";
import { PickupMap } from "@/components/maps/pickup-map-loader";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { setPickupLocation } from "@/lib/api/merchants-me";
import { env } from "@/lib/env";
import { describeError } from "@/lib/errors";

const ADDRESS_KEY = "rappix.merchant.pickup-address";
const PICKUP_DONE_KEY = "rappix.merchant.pickup-done";

export default function UbicacionPage() {
  const router = useRouter();
  const queryClient = useQueryClient();

  const [lat, setLat] = useState(env.defaultLat);
  const [lng, setLng] = useState(env.defaultLng);
  const [recenterToken, setRecenterToken] = useState(0);
  const [address, setAddress] = useState("");

  // Restore the locally-stored address text (the backend doesn't store it).
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
      () => toast.error("No pudimos obtener tu ubicación. Permite el acceso e inténtalo de nuevo."),
    );
  }

  const save = useMutation({
    mutationFn: () => setPickupLocation({ latitude: lat, longitude: lng }),
    onSuccess: (data) => {
      queryClient.setQueryData(["merchant", "me"], data);
      if (typeof window !== "undefined") {
        window.localStorage.setItem(ADDRESS_KEY, address);
        window.localStorage.setItem(PICKUP_DONE_KEY, "1");
      }
      router.push("/onboarding/logo");
    },
    onError: (err) => toast.error(describeError(err)),
  });

  return (
    <StepShell
      stepNumber={4}
      title="Ubicación"
      description="Marca la ubicación exacta donde los repartidores deben recoger los pedidos."
      onBack={() => router.push("/onboarding/zona")}
      onContinue={() => save.mutate()}
      continueLabel={save.isPending ? "Guardando..." : "Continuar"}
      continueDisabled={save.isPending}
    >
      <div className="space-y-5">
        <div className="relative">
          <PickupMap
            latitude={lat}
            longitude={lng}
            recenterToken={recenterToken}
            onChange={(la, ln) => {
              setLat(la);
              setLng(ln);
            }}
          />
          <button
            type="button"
            onClick={useMyLocation}
            className="absolute right-3 top-3 inline-flex items-center gap-1.5 rounded-md bg-white px-2.5 py-1.5 text-xs font-semibold text-foreground shadow-md hover:bg-muted"
          >
            <Crosshair className="h-3.5 w-3.5" />
            Usar mi ubicación
          </button>
          <div className="absolute bottom-3 left-3 inline-flex items-center gap-2 rounded-md bg-white px-3 py-1.5 text-xs font-semibold shadow-md">
            <MapPin className="h-3.5 w-3.5 text-brand" />
            {lat.toFixed(5)}, {lng.toFixed(5)}
          </div>
        </div>

        <div className="space-y-1.5">
          <Label htmlFor="address">Dirección de recogida</Label>
          <Input
            id="address"
            value={address}
            onChange={(e) => setAddress(e.target.value)}
            placeholder="Av. Independencia 245, Gascue, Santo Domingo"
          />
          <p className="text-xs text-muted-foreground">
            Solo se guarda en este dispositivo para tu referencia. Lo que importa al backend es la lat/lng del pin.
          </p>
        </div>

        <Button type="button" variant="ghost" onClick={useMyLocation} className="w-full sm:w-auto">
          <Crosshair className="h-4 w-4" />
          Usar mi ubicación actual
        </Button>
      </div>
    </StepShell>
  );
}
