"use client";

import { CourierMap } from "@/components/map/courier-map";
import { DeliveryInfoPanel } from "@/components/courier/delivery-info-panel";
import type { CurrentAssignmentResponse } from "@/lib/api/types";

/**
 * Layout responsive de la pantalla de pedido activo (estados 3 y 3b del diseno).
 *
 *  - Movil (<lg): apilado vertical — mapa h-[40vh] arriba, panel debajo (scroll
 *    natural si el contenido excede).
 *  - Desktop (>=lg): two-column grid — mapa flex-1 izquierda (toma toda la
 *    altura disponible, que es donde Google Maps brilla), panel info derecha
 *    con ancho fijo 420px (sticky para que las acciones queden a mano si el
 *    contenido del panel crece).
 *
 * La altura del mapa es EXPLICITA en ambos breakpoints (h-[40vh] / h-full
 * con padre h-[calc(100vh-XXpx)]) para evitar el bug clasico de mapa
 * "gris" en contenedor sin tamano al mount.
 */
export function ActiveDelivery({ assignment }: { assignment: CurrentAssignmentResponse }) {
  return (
    <div className="mx-auto w-full max-w-screen-2xl">
      {/* En desktop, el grid tiene altura calculada para que el mapa rellene
          el viewport menos el header (sticky, ~64px). En movil cada columna
          mantiene su altura natural. */}
      <div className="grid grid-cols-1 gap-4 lg:grid-cols-[1fr_420px] lg:h-[calc(100vh-72px)] lg:gap-6">
        <div className="h-[40vh] lg:h-full">
          <CourierMap
            pickup={assignment.pickup}
            delivery={assignment.delivery}
            className="h-full"
          />
        </div>
        <div className="lg:overflow-y-auto lg:pr-2">
          <DeliveryInfoPanel assignment={assignment} />
        </div>
      </div>
    </div>
  );
}
