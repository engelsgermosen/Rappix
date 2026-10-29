"use client";

import { Pin } from "@vis.gl/react-google-maps";
import { useMemo } from "react";

import { FitBounds, GoogleMap, MapPin } from "@/components/map/google-map";

export type LatLng = { latitude: number; longitude: number };

/**
 * Mapa del pedido activo con 2 marcadores: pickup (morado #534AB7 = comercio)
 * y delivery (coral #D85A30 = cliente). Sin polyline en v1 — el sistema de
 * Routes API de Google es de pago y el zoom-to-fit con dos pines comunica
 * el viaje. Si en el futuro existen estados intermedios PickedUp/InTransit
 * que justifiquen una ruta, se agrega aqui (follow-up documentado en
 * FINDINGS.md).
 *
 * Sin API key (NEXT_PUBLIC_GOOGLE_MAPS_API_KEY vacio) GoogleMap renderiza el
 * estado "Mapa no disponible" con boton "Abrir ruta en Google Maps" — el resto
 * del portal sigue funcionando.
 *
 * Resize: con vector maps + mapId el contenedor se redibuja solo, siempre que
 * tenga altura explicita ANTES del mount (por eso el contenedor exterior aqui
 * lleva `h-[40vh] lg:h-full`). FitBounds se re-ejecuta cuando cambian los
 * markers para mantener ambos pines visibles.
 */
export function CourierMap({
  pickup,
  delivery,
  className,
}: {
  pickup: LatLng;
  delivery: LatLng;
  className?: string;
}) {
  const pickupPos = useMemo(
    () => ({ lat: pickup.latitude, lng: pickup.longitude }),
    [pickup.latitude, pickup.longitude],
  );
  const deliveryPos = useMemo(
    () => ({ lat: delivery.latitude, lng: delivery.longitude }),
    [delivery.latitude, delivery.longitude],
  );
  const center = useMemo(
    () => ({ lat: (pickupPos.lat + deliveryPos.lat) / 2, lng: (pickupPos.lng + deliveryPos.lng) / 2 }),
    [pickupPos, deliveryPos],
  );
  const points = useMemo(() => [pickupPos, deliveryPos], [pickupPos, deliveryPos]);

  const gmapsUrl =
    `https://www.google.com/maps/dir/?api=1&origin=${pickup.latitude},${pickup.longitude}&destination=${delivery.latitude},${delivery.longitude}`;

  return (
    <GoogleMap
      defaultCenter={center}
      defaultZoom={14}
      gestureHandling="greedy"
      className={className}
      fallbackAction={
        <a
          href={gmapsUrl}
          target="_blank"
          rel="noopener noreferrer"
          className="text-sm font-semibold text-brand hover:underline"
        >
          Abrir ruta en Google Maps
        </a>
      }
    >
      <MapPin position={pickupPos} title="Recoger en el comercio">
        <Pin background="#534AB7" borderColor="#3D3590" glyphColor="#FFFFFF" />
      </MapPin>
      <MapPin position={deliveryPos} title="Entregar al cliente">
        <Pin background="#D85A30" borderColor="#B14525" glyphColor="#FFFFFF" />
      </MapPin>
      {/* El primer fit usa padding mayor para no quedar muy zoom-in. */}
      <FitBounds points={points} padding={80} firstPadding={120} />
    </GoogleMap>
  );
}
