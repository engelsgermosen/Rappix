"use client";

import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { toast } from "sonner";
import {
  CheckCircle2,
  ClipboardList,
  Home,
  MapPin,
  PackageCheck,
  User,
} from "lucide-react";

import {
  Tooltip,
  TooltipContent,
  TooltipProvider,
  TooltipTrigger,
} from "@/components/ui/tooltip";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { courierApi } from "@/lib/api/courier";
import type { CurrentAssignmentResponse } from "@/lib/api/types";
import { courierQueryKey } from "@/lib/courier/use-courier";
import { currentAssignmentQueryKey } from "@/lib/courier/use-current-assignment";
import { describeError } from "@/lib/errors";
import { formatCustomerShortId } from "@/lib/courier/format";
import { formatMoney } from "@/lib/utils";

/**
 * Panel info de la pantalla de pedido activo. Tres cards (recoger / entregar /
 * resumen) + dos botones de accion al final.
 *
 * El boton "Recogi en el comercio" se renderiza como dice el diseno (con tag
 * FUTURO + disabled + tooltip) pero NO tiene onClick ni estado local — es
 * diseno visible, no comportamiento. Estados intermedios PickedUp/InTransit
 * son follow-up del backend (Gap #3 ADR-0008).
 *
 * El boton "Entregue al cliente" (verde, btn-giant) es el unico funcional:
 * POST /me/current-assignment/delivered. 204 -> invalida el cache de
 * current-assignment + courier para volver a idle. 404 -> "ya no esta activa".
 */
export function DeliveryInfoPanel({ assignment }: { assignment: CurrentAssignmentResponse }) {
  const qc = useQueryClient();
  const [confirming, setConfirming] = useState(false);

  const deliver = useMutation({
    mutationFn: courierApi.markDelivered,
    onSuccess: () => {
      // El backend acaba de publicar OrderDeliveredIntegrationEvent — el
      // OrderTerminalEventsConsumer va a liberar al courier (Busy -> Online)
      // en milisegundos. Invalidamos los dos caches para que el poll proximo
      // refleje el estado real (204 No Content + status Online).
      qc.invalidateQueries({ queryKey: [...currentAssignmentQueryKey] });
      qc.invalidateQueries({ queryKey: [...courierQueryKey] });
      toast.success("Entrega confirmada. Buen trabajo.");
      setConfirming(false);
    },
    onError: (err) => {
      // 404 NoActiveAssignment puede ocurrir si el customer ya cancelo entre
      // ticks del poll y el courier toca igual. describeError lo mapea bien;
      // ademas invalidamos para que el UI se rehidrate al estado real.
      toast.error(describeError(err));
      qc.invalidateQueries({ queryKey: [...currentAssignmentQueryKey] });
      setConfirming(false);
    },
  });

  return (
    <div className="flex flex-col gap-4">
      {/* Pickup */}
      <Card>
        <CardContent className="space-y-2 p-5">
          <div className="flex items-center gap-2">
            <span className="grid h-8 w-8 place-items-center rounded-full bg-brand text-white">
              <MapPin className="h-4 w-4" />
            </span>
            <span className="text-xs font-semibold uppercase tracking-wide text-brand-700">
              Recoger en
            </span>
          </div>
          <p className="text-lg font-bold leading-tight">{assignment.pickup.merchantName}</p>
          <p className="text-xs italic text-muted-foreground">
            Solo ubicacion en mapa (el comercio no comparte direccion exacta hoy).
          </p>
          <p className="text-xs text-muted-foreground">
            {assignment.pickup.latitude.toFixed(5)}, {assignment.pickup.longitude.toFixed(5)}
          </p>
        </CardContent>
      </Card>

      {/* Delivery */}
      <Card>
        <CardContent className="space-y-2 p-5">
          <div className="flex items-center gap-2">
            <span className="grid h-8 w-8 place-items-center rounded-full bg-accent text-white">
              <Home className="h-4 w-4" />
            </span>
            <span className="text-xs font-semibold uppercase tracking-wide text-accent-700">
              Entregar en
            </span>
          </div>
          <p className="text-lg font-bold leading-tight">{assignment.delivery.street}</p>
          {assignment.delivery.reference ? (
            <p className="text-sm text-foreground">{assignment.delivery.reference}</p>
          ) : null}
          <div className="flex items-center gap-1.5 text-xs text-muted-foreground">
            <User className="h-3.5 w-3.5" /> {formatCustomerShortId(assignment.customerUserId)}
          </div>
        </CardContent>
      </Card>

      {/* Resumen del pedido */}
      <Card>
        <CardContent className="space-y-3 p-5">
          <div className="flex items-center gap-2">
            <ClipboardList className="h-4 w-4 text-muted-foreground" />
            <span className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">
              Resumen
            </span>
          </div>
          <ul className="space-y-1.5 text-sm">
            {assignment.lines.map((line, i) => (
              <li key={i} className="flex justify-between gap-3">
                <span className="text-foreground">{line.itemName}</span>
                <span className="font-semibold text-muted-foreground">× {line.quantity}</span>
              </li>
            ))}
          </ul>
          <div className="flex justify-between border-t border-border pt-3 text-sm font-bold">
            <span>Total a entregar</span>
            <span>{formatMoney(assignment.orderTotal, assignment.orderCurrency)}</span>
          </div>
        </CardContent>
      </Card>

      {/* Acciones */}
      <div className="space-y-3">
        {/* FUTURO: "Recogi en el comercio" — diseno visible, no comportamiento.
            Disabled + tooltip explicando porque. Sin onClick. */}
        <TooltipProvider>
          <Tooltip>
            <TooltipTrigger asChild>
              <div className="relative">
                <Button
                  type="button"
                  variant="outline"
                  disabled
                  className="btn-giant w-full opacity-60"
                  aria-label="Recogi en el comercio (no disponible aun)"
                >
                  <PackageCheck className="mr-2 h-5 w-5" />
                  Recogi en el comercio
                </Button>
                <Badge variant="warn" className="absolute right-3 top-3 text-[10px]">FUTURO</Badge>
              </div>
            </TooltipTrigger>
            <TooltipContent side="top">
              Los estados intermedios (recogido / en camino) llegan cuando el backend exponga PickedUp/InTransit (Gap #3, ADR-0008).
            </TooltipContent>
          </Tooltip>
        </TooltipProvider>

        {/* Entregue — el unico funcional */}
        {confirming ? (
          <div className="space-y-2 rounded-lg border border-success-100 bg-success-50 p-4">
            <p className="text-sm font-semibold text-success-700">
              Confirmas que entregaste el pedido al cliente?
            </p>
            <div className="flex gap-2">
              <Button
                type="button"
                variant="outline"
                className="flex-1"
                onClick={() => setConfirming(false)}
              >
                Cancelar
              </Button>
              <Button
                type="button"
                onClick={() => deliver.mutate()}
                disabled={deliver.isPending}
                className="flex-1 bg-success hover:bg-success-600 text-white"
              >
                {deliver.isPending ? "Confirmando..." : "Si, entregue"}
              </Button>
            </div>
          </div>
        ) : (
          <Button
            type="button"
            onClick={() => setConfirming(true)}
            className="btn-giant w-full bg-success hover:bg-success-600 text-white"
          >
            <CheckCircle2 className="mr-2 h-5 w-5" />
            Entregue al cliente
          </Button>
        )}
      </div>
    </div>
  );
}
