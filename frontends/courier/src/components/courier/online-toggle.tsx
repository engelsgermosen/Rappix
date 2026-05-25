"use client";

import { useMutation, useQueryClient } from "@tanstack/react-query";
import Link from "next/link";
import { useState } from "react";
import { toast } from "sonner";
import { Bike, Power, PowerOff } from "lucide-react";

import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { courierApi } from "@/lib/api/courier";
import type { CourierResponse } from "@/lib/api/types";
import { ApiError } from "@/lib/api/errors";
import { courierQueryKey } from "@/lib/courier/use-courier";
import { describeError } from "@/lib/errors";
import { cn } from "@/lib/utils";

/**
 * El toggle gigante de la pantalla principal cuando el courier NO tiene
 * asignacion activa. Tres ramas visuales segun el estado:
 *
 *  - Offline sin vehiculo: CTA "Configurar vehiculo" (no se puede ir Online
 *    sin uno; el backend devolveria 400 VehicleRequired).
 *  - Offline con vehiculo: boton verde grande "Conectarme".
 *  - Online: boton rojo grande "Desconectarme" (oculto si el padre detecto
 *    Busy — la pantalla activa renderiza otra cosa).
 *
 * El boton mide >= 64px (la clase `.btn-giant` de globals.css) — diseno hecho
 * para usar en la calle, con guantes o al sol.
 */
export function OnlineToggle({ courier }: { courier: CourierResponse }) {
  const qc = useQueryClient();
  const [confirmingOffline, setConfirmingOffline] = useState(false);

  const onlineMut = useMutation({
    mutationFn: courierApi.goOnline,
    onSuccess: (data) => {
      qc.setQueryData(courierQueryKey, data);
      toast.success("Conectado. Buscando pedidos cerca de ti.");
    },
    onError: (err) => {
      // 400 VehicleRequired es el caso esperado si entran al toggle sin vehiculo
      // configurado. Mostramos un mensaje accionable.
      if (err instanceof ApiError && err.code === "Dispatch.Courier.VehicleRequired") {
        toast.error("Configura tu vehiculo antes de conectarte.");
      } else {
        toast.error(describeError(err));
      }
    },
  });

  const offlineMut = useMutation({
    mutationFn: courierApi.goOffline,
    onSuccess: (data) => {
      qc.setQueryData(courierQueryKey, data);
      toast.success("Desconectado.");
      setConfirmingOffline(false);
    },
    onError: (err) => {
      // 409 BusyCannotGoOffline es el caso especial — describeError ya lo
      // mapea pero no esta de mas redirigirlos visualmente.
      toast.error(describeError(err));
      setConfirmingOffline(false);
    },
  });

  if (courier.status === "Offline" && !courier.vehicle) {
    return (
      <Card className="w-full max-w-[600px] mx-auto">
        <CardContent className="space-y-4 p-6 text-center">
          <Bike className="mx-auto h-12 w-12 text-brand" />
          <div className="space-y-1">
            <h2 className="text-2xl font-bold">Primero, tu vehiculo</h2>
            <p className="text-sm text-muted-foreground">
              Para conectarte y empezar a recibir pedidos, dinos en que entregas.
            </p>
          </div>
          <Button asChild size="lg" className="btn-giant w-full">
            <Link href="/vehiculo">Configurar vehiculo</Link>
          </Button>
        </CardContent>
      </Card>
    );
  }

  if (courier.status === "Offline") {
    return (
      <Card className="w-full max-w-[600px] mx-auto">
        <CardContent className="space-y-5 p-6 text-center">
          <div className="space-y-1">
            <h2 className="text-2xl font-bold">Listo cuando quieras</h2>
            <p className="text-sm text-muted-foreground">
              Estas desconectado. Conectate para recibir tu primer pedido.
            </p>
          </div>
          <Button
            type="button"
            onClick={() => onlineMut.mutate()}
            disabled={onlineMut.isPending}
            className={cn("btn-giant w-full bg-success hover:bg-success-600 text-white")}
          >
            <Power className="mr-2 h-5 w-5" />
            {onlineMut.isPending ? "Conectando..." : "Conectarme"}
          </Button>
          <p className="text-xs text-muted-foreground">
            Vehiculo: {courier.vehicle?.type ?? "—"}
            {courier.vehicle?.plate ? ` · ${courier.vehicle.plate}` : ""}
          </p>
        </CardContent>
      </Card>
    );
  }

  // Online (sin asignacion — el padre ya enruto Busy a otra pantalla).
  return (
    <Card className="w-full max-w-[600px] mx-auto">
      <CardContent className="space-y-5 p-6 text-center">
        <div className="space-y-1">
          <h2 className="text-2xl font-bold">Esperando pedido</h2>
          <p className="text-sm text-muted-foreground">
            Estas conectado. Te avisamos cuando tengas una asignacion.
          </p>
        </div>
        {confirmingOffline ? (
          <div className="space-y-2">
            <p className="text-sm font-semibold text-reject">Seguro? Dejaras de recibir asignaciones.</p>
            <div className="flex gap-2">
              <Button
                type="button"
                variant="outline"
                className="flex-1"
                onClick={() => setConfirmingOffline(false)}
              >
                Cancelar
              </Button>
              <Button
                type="button"
                onClick={() => offlineMut.mutate()}
                disabled={offlineMut.isPending}
                className="flex-1 bg-reject hover:bg-reject-600 text-white"
              >
                {offlineMut.isPending ? "Desconectando..." : "Si, desconectarme"}
              </Button>
            </div>
          </div>
        ) : (
          <Button
            type="button"
            onClick={() => setConfirmingOffline(true)}
            className={cn("btn-giant w-full bg-reject hover:bg-reject-600 text-white")}
          >
            <PowerOff className="mr-2 h-5 w-5" />
            Desconectarme
          </Button>
        )}
      </CardContent>
    </Card>
  );
}
