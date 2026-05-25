"use client";

import {
  APIProvider,
  AdvancedMarker,
  Map,
  Pin,
  useMap,
} from "@vis.gl/react-google-maps";
import { useEffect, useMemo, useRef } from "react";
import { MapPinOff } from "lucide-react";

import { env } from "@/lib/env";
import { cn } from "@/lib/utils";

export type LatLng = { latitude: number; longitude: number };

/**
 * Mapa del pedido activo con 2 marcadores: pickup (morado #534AB7 = comercio)
 * y delivery (coral #D85A30 = cliente). Sin polyline en v1 — el sistema de
 * Routes API de Google es de pago y el zoom-to-fit con dos pines comunica
 * el viaje. Si en el futuro existen estados intermedios PickedUp/InTransit
 * que justifiquen una ruta, se agrega aqui (follow-up documentado en
 * FINDINGS.md).
 *
 * Sin API key (NEXT_PUBLIC_GOOGLE_MAPS_API_KEY vacio) renderiza un placeholder
 * con boton "Abrir en Google Maps" — el resto del portal sigue funcionando.
 *
 * Resize: NO usamos el truco viejo `google.maps.event.trigger(map, "resize")`
 * que aplica a mapas raster; con vector maps + mapId el contenedor se
 * redibuja solo, siempre que tenga altura explicita ANTES del mount (por eso
 * el contenedor exterior aqui lleva `h-[40vh] lg:h-full` y el div interno
 * `h-full w-full`). useMap + fitBounds se re-ejecuta cuando cambian los
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
  // useMemo SIEMPRE (antes de cualquier early return) — rules-of-hooks.
  const center = useMemo(
    () => ({
      lat: (pickup.latitude + delivery.latitude) / 2,
      lng: (pickup.longitude + delivery.longitude) / 2,
    }),
    [pickup.latitude, pickup.longitude, delivery.latitude, delivery.longitude],
  );

  if (!env.googleMapsApiKey) {
    return <MapPlaceholder pickup={pickup} delivery={delivery} className={className} />;
  }

  return (
    <div className={cn("w-full overflow-hidden rounded-xl border border-border bg-muted", className)}>
      <APIProvider apiKey={env.googleMapsApiKey}>
        <Map
          mapId={env.googleMapsMapId || undefined}
          defaultCenter={center}
          defaultZoom={14}
          gestureHandling="greedy"
          disableDefaultUI
          className="h-full w-full"
        >
          <MapMarkers pickup={pickup} delivery={delivery} />
          <FitBoundsOnChange pickup={pickup} delivery={delivery} />
        </Map>
      </APIProvider>
    </div>
  );
}

/**
 * Markers con AdvancedMarker (vector map) o fallback a un overlay simple si
 * el mapId no esta configurado y el mapa cayo a raster.
 */
function MapMarkers({ pickup, delivery }: { pickup: LatLng; delivery: LatLng }) {
  // AdvancedMarker funciona en ambos modos en @vis.gl/react-google-maps; con
  // mapId vacio el Pin se sigue pintando aunque sea raster (la libreria
  // hace el shim). Cumple para el portal v1.
  return (
    <>
      <AdvancedMarker
        position={{ lat: pickup.latitude, lng: pickup.longitude }}
        title="Recoger en el comercio"
      >
        <Pin background="#534AB7" borderColor="#3D3590" glyphColor="#FFFFFF" />
      </AdvancedMarker>
      <AdvancedMarker
        position={{ lat: delivery.latitude, lng: delivery.longitude }}
        title="Entregar al cliente"
      >
        <Pin background="#D85A30" borderColor="#B14525" glyphColor="#FFFFFF" />
      </AdvancedMarker>
    </>
  );
}

/**
 * Encaja el viewport a un bounds que cubre ambos markers cada vez que cambian
 * (asignacion nueva, o ubicacion del courier reportada). Padding generoso
 * para que los pines no queden contra el borde.
 */
function FitBoundsOnChange({ pickup, delivery }: { pickup: LatLng; delivery: LatLng }) {
  const map = useMap();
  const ranOnceRef = useRef(false);

  useEffect(() => {
    if (!map) return;
    const bounds = new google.maps.LatLngBounds();
    bounds.extend({ lat: pickup.latitude, lng: pickup.longitude });
    bounds.extend({ lat: delivery.latitude, lng: delivery.longitude });
    // El primer fit usa padding mayor para no quedar muy zoom-in.
    const padding = ranOnceRef.current ? 80 : 120;
    map.fitBounds(bounds, padding);
    ranOnceRef.current = true;
  }, [map, pickup.latitude, pickup.longitude, delivery.latitude, delivery.longitude]);

  return null;
}

function MapPlaceholder({
  pickup,
  delivery,
  className,
}: {
  pickup: LatLng;
  delivery: LatLng;
  className?: string;
}) {
  const gmapsUrl =
    `https://www.google.com/maps/dir/?api=1&origin=${pickup.latitude},${pickup.longitude}&destination=${delivery.latitude},${delivery.longitude}`;
  return (
    <div
      className={cn(
        "flex w-full flex-col items-center justify-center gap-3 rounded-xl border border-dashed border-border bg-muted/40 p-6 text-center",
        className,
      )}
    >
      <MapPinOff className="h-10 w-10 text-muted-foreground" />
      <div className="space-y-1">
        <p className="text-sm font-semibold">Mapa no disponible</p>
        <p className="text-xs text-muted-foreground">
          Configura <code>NEXT_PUBLIC_GOOGLE_MAPS_API_KEY</code> en tu <code>.env.local</code> para verlo en vivo.
        </p>
      </div>
      <a
        href={gmapsUrl}
        target="_blank"
        rel="noopener noreferrer"
        className="text-sm font-semibold text-brand hover:underline"
      >
        Abrir ruta en Google Maps
      </a>
    </div>
  );
}
