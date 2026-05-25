"use client";

import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useRouter } from "next/navigation";
import { useEffect, useState, type FormEvent } from "react";
import { toast } from "sonner";
import { Bike, Car, CheckCircle2 } from "lucide-react";

import { AppHeader } from "@/components/courier/app-header";
import { RequireAuth } from "@/components/auth/require-auth";
import { Button } from "@/components/ui/button";
import { Card } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { courierApi } from "@/lib/api/courier";
import type { VehicleType } from "@/lib/api/types";
import { courierQueryKey, useCourier } from "@/lib/courier/use-courier";
import { describeError } from "@/lib/errors";
import { cn } from "@/lib/utils";

const OPTIONS: Array<{ value: VehicleType; label: string; sublabel: string; icon: React.ReactNode }> = [
  { value: "Moto",  label: "Moto",       sublabel: "Para entregas rapidas y zonas con trafico.",        icon: <MotoIcon /> },
  { value: "Bici",  label: "Bicicleta",  sublabel: "Ideal para pedidos cercanos y bajo costo.",         icon: <Bike className="h-7 w-7" /> },
  { value: "Carro", label: "Carro",      sublabel: "Para pedidos voluminosos o varias entregas.",       icon: <Car className="h-7 w-7" /> },
];

function VehiculoContent() {
  const router = useRouter();
  const qc = useQueryClient();
  const courier = useCourier();

  const [type, setType] = useState<VehicleType>("Moto");
  const [plate, setPlate] = useState("");
  const [capacityKg, setCapacityKg] = useState("");

  // Prefill desde el vehiculo actual (si existe).
  useEffect(() => {
    if (courier.data?.vehicle) {
      setType((prev) => prev || courier.data!.vehicle!.type);
      setPlate((prev) => prev || courier.data!.vehicle!.plate || "");
      setCapacityKg((prev) =>
        prev || (courier.data!.vehicle!.capacityKg != null ? String(courier.data!.vehicle!.capacityKg) : ""),
      );
    }
  }, [courier.data]);

  const save = useMutation({
    mutationFn: courierApi.updateVehicle,
    onSuccess: (data) => {
      qc.setQueryData(courierQueryKey, data);
      toast.success("Vehiculo guardado.");
      router.push("/");
    },
    onError: (err) => toast.error(describeError(err)),
  });

  function onSubmit(e: FormEvent) {
    e.preventDefault();
    const cap = capacityKg.trim();
    const parsed = cap ? Number(cap) : null;
    if (cap && (Number.isNaN(parsed!) || parsed! <= 0)) {
      toast.error("La capacidad debe ser un numero positivo.");
      return;
    }
    save.mutate({
      vehicleType: type,
      plate: plate.trim() || null,
      capacityKg: parsed,
    });
  }

  return (
    <>
      <AppHeader status={courier.data?.status} />
      <main className="flex-1 px-4 py-8 md:px-6">
        <div className="mx-auto w-full max-w-[560px] space-y-6">
          <div className="space-y-1.5">
            <h1 className="text-2xl font-bold">En que entregas</h1>
            <p className="text-sm text-muted-foreground">
              Elige tu vehiculo principal. Puedes cambiarlo despues cuando quieras.
            </p>
          </div>

          <div className="grid grid-cols-1 gap-3 sm:grid-cols-3">
            {OPTIONS.map((opt) => {
              const selected = type === opt.value;
              return (
                <button
                  key={opt.value}
                  type="button"
                  onClick={() => setType(opt.value)}
                  className={cn(
                    "relative flex flex-col items-center gap-2 rounded-xl border bg-white p-4 text-center transition-all",
                    selected
                      ? "border-brand ring-2 ring-brand/30 shadow-sm"
                      : "border-border hover:border-brand-300",
                  )}
                  aria-pressed={selected}
                >
                  {selected ? (
                    <CheckCircle2 className="absolute right-2 top-2 h-5 w-5 text-brand" />
                  ) : null}
                  <span className={cn("text-brand", selected ? "" : "opacity-70")}>{opt.icon}</span>
                  <span className="text-base font-semibold">{opt.label}</span>
                  <span className="text-xs text-muted-foreground">{opt.sublabel}</span>
                </button>
              );
            })}
          </div>

          <Card>
            <form onSubmit={onSubmit} className="space-y-4 p-5">
              <div className="space-y-1.5">
                <Label htmlFor="plate">Placa (opcional)</Label>
                <Input
                  id="plate"
                  placeholder={type === "Bici" ? "Bici sin placa" : "Ej. A1234"}
                  value={plate}
                  onChange={(e) => setPlate(e.target.value)}
                  maxLength={20}
                  autoComplete="off"
                  className="uppercase"
                />
                <p className="text-xs text-muted-foreground">
                  El backend la guarda en mayusculas. Maximo 20 caracteres.
                </p>
              </div>

              <div className="space-y-1.5">
                <Label htmlFor="capacity">Capacidad (kg, opcional)</Label>
                <Input
                  id="capacity"
                  type="number"
                  inputMode="decimal"
                  min="0.01"
                  step="0.01"
                  placeholder="Ej. 15"
                  value={capacityKg}
                  onChange={(e) => setCapacityKg(e.target.value)}
                />
                <p className="text-xs text-muted-foreground">
                  Si lo dejas vacio, queda sin restriccion de capacidad.
                </p>
              </div>

              <Button type="submit" disabled={save.isPending} className="btn-giant w-full">
                {save.isPending ? "Guardando..." : "Guardar vehiculo"}
              </Button>
            </form>
          </Card>
        </div>
      </main>
    </>
  );
}

/** Icono de moto (no hay uno en lucide-react que matchee exactamente Moto vs Bici). */
function MotoIcon() {
  return (
    <svg className="h-7 w-7" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <circle cx="5.5" cy="17.5" r="3.5" />
      <circle cx="18.5" cy="17.5" r="3.5" />
      <path d="M5.5 17.5 9 8h5l3 4h2" />
      <path d="M14 8h-2" />
    </svg>
  );
}

export default function VehiculoPage() {
  return (
    <RequireAuth>
      <VehiculoContent />
    </RequireAuth>
  );
}
