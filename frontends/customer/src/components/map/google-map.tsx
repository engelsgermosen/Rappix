"use client";

/**
 * Integracion compartida de Google Maps (Maps JavaScript API via
 * @vis.gl/react-google-maps). Este archivo es IDENTICO en courier, customer y
 * merchant — los tres frontends son proyectos independientes sin workspace
 * compartido, asi que se duplica tal cual. Si lo cambias, copialo a los otros
 * dos (`src/components/map/google-map.tsx`).
 *
 * - Sin NEXT_PUBLIC_GOOGLE_MAPS_API_KEY: renderiza un estado "Mapa no
 *   disponible" en vez de romper la pantalla.
 * - Map ID: NEXT_PUBLIC_GOOGLE_MAPS_MAP_ID; si falta se usa DEMO_MAP_ID
 *   (solo valido en desarrollo — AdvancedMarker exige un mapId). Produccion
 *   necesita un Map ID real (ver PENDIENTES.md).
 * - Vector maps se redimensionan solos: el contenedor solo necesita altura
 *   explicita antes del mount (no hace falta forzar resize).
 * - Polyline/Circle son imperativos: se crean una vez por mapa, se actualizan
 *   con setPath/setCenter/setRadius/setOptions y se desmontan con setMap(null).
 *   Importante en el tracking en vivo (SignalR empuja ubicaciones seguido).
 */

import {
  APIProvider,
  AdvancedMarker,
  AdvancedMarkerAnchorPoint,
  Map,
  useMap,
  useMapsLibrary,
  type MapMouseEvent,
} from "@vis.gl/react-google-maps";
import { useEffect, useRef, type ReactNode } from "react";
import { MapPinOff } from "lucide-react";

import { env } from "@/lib/env";
import { cn } from "@/lib/utils";

export type LatLngLiteral = { lat: number; lng: number };

/** Fallback de desarrollo. Produccion: definir NEXT_PUBLIC_GOOGLE_MAPS_MAP_ID. */
const DEV_FALLBACK_MAP_ID = "DEMO_MAP_ID";

function pathKey(points: LatLngLiteral[]): string {
  return points.map((p) => `${p.lat},${p.lng}`).join("|");
}

export type GoogleMapProps = {
  defaultCenter: LatLngLiteral;
  defaultZoom: number;
  /** "greedy" = un dedo arrastra (picker), "cooperative" = ctrl+scroll, "none" = estatico. */
  gestureHandling?: "greedy" | "cooperative" | "none" | "auto";
  /** Muestra el control de zoom (+/-). */
  zoomControl?: boolean;
  onClick?: (pos: LatLngLiteral) => void;
  /** Contenido extra del estado "Mapa no disponible" (ej. link a Google Maps). */
  fallbackAction?: ReactNode;
  className?: string;
  children?: ReactNode;
};

/**
 * Contenedor del mapa. `className` debe dar altura explicita; se aplica tanto
 * al mapa como al estado "no disponible" para que el layout no salte.
 */
export function GoogleMap({
  defaultCenter,
  defaultZoom,
  gestureHandling = "greedy",
  zoomControl = false,
  onClick,
  fallbackAction,
  className,
  children,
}: GoogleMapProps) {
  if (!env.googleMapsApiKey) {
    return <MapUnavailable className={className}>{fallbackAction}</MapUnavailable>;
  }

  return (
    // `isolate` crea un stacking context: los z-index internos del mapa no
    // tapan headers/sidebars sticky.
    <div className={cn("isolate w-full overflow-hidden rounded-xl border border-border bg-muted", className)}>
      <APIProvider apiKey={env.googleMapsApiKey}>
        <Map
          mapId={env.googleMapsMapId || DEV_FALLBACK_MAP_ID}
          defaultCenter={defaultCenter}
          defaultZoom={defaultZoom}
          gestureHandling={gestureHandling}
          disableDefaultUI
          zoomControl={zoomControl}
          clickableIcons={false}
          onClick={
            onClick
              ? (e: MapMouseEvent) => {
                  const ll = e.detail.latLng;
                  if (ll) onClick({ lat: ll.lat, lng: ll.lng });
                }
              : undefined
          }
          className="h-full w-full"
        >
          {children}
        </Map>
      </APIProvider>
    </div>
  );
}

export function MapUnavailable({ className, children }: { className?: string; children?: ReactNode }) {
  return (
    <div
      role="status"
      className={cn(
        "flex w-full flex-col items-center justify-center gap-3 rounded-xl border border-dashed border-border bg-muted/40 p-6 text-center",
        className,
      )}
    >
      <MapPinOff className="h-10 w-10 text-muted-foreground" />
      <div className="space-y-1">
        <p className="text-sm font-semibold">Mapa no disponible: falta la clave de Google Maps</p>
        <p className="text-xs text-muted-foreground">
          Configura <code>NEXT_PUBLIC_GOOGLE_MAPS_API_KEY</code> para verlo.
        </p>
      </div>
      {children}
    </div>
  );
}

/**
 * Marcador (AdvancedMarker). `children` = HTML custom; sin children se pinta el
 * Pin por defecto de Google. `anchor="center"` para iconos circulares.
 */
export function MapPin({
  position,
  title,
  draggable = false,
  onDragEnd,
  anchor = "bottom",
  zIndex,
  children,
}: {
  position: LatLngLiteral;
  title?: string;
  draggable?: boolean;
  onDragEnd?: (pos: LatLngLiteral) => void;
  anchor?: "bottom" | "center";
  zIndex?: number;
  children?: ReactNode;
}) {
  return (
    <AdvancedMarker
      position={position}
      title={title}
      draggable={draggable}
      zIndex={zIndex}
      anchorPoint={anchor === "center" ? AdvancedMarkerAnchorPoint.CENTER : AdvancedMarkerAnchorPoint.BOTTOM}
      onDragEnd={(e) => {
        const ll = e.latLng;
        if (ll && onDragEnd) onDragEnd({ lat: ll.lat(), lng: ll.lng() });
      }}
    >
      {children}
    </AdvancedMarker>
  );
}

/** Linea recta entre puntos (no usa Routes API). */
export function MapPolyline({
  path,
  color,
  weight = 4,
  opacity = 1,
  dashed = false,
}: {
  path: LatLngLiteral[];
  color: string;
  weight?: number;
  opacity?: number;
  dashed?: boolean;
}) {
  const map = useMap();
  const lineRef = useRef<google.maps.Polyline | null>(null);
  const key = pathKey(path);

  useEffect(() => {
    if (!map) return;
    const line = new google.maps.Polyline({ map, clickable: false });
    lineRef.current = line;
    return () => {
      line.setMap(null);
      lineRef.current = null;
    };
  }, [map]);

  useEffect(() => {
    lineRef.current?.setOptions(
      dashed
        ? {
            strokeColor: color,
            strokeOpacity: 0,
            strokeWeight: weight,
            icons: [
              {
                icon: { path: "M 0,-1 0,1", strokeOpacity: opacity, strokeWeight: weight, scale: weight / 2 },
                offset: "0",
                repeat: `${weight * 3.5}px`,
              },
            ],
          }
        : { strokeColor: color, strokeOpacity: opacity, strokeWeight: weight, icons: [] },
    );
  }, [map, color, weight, opacity, dashed]);

  useEffect(() => {
    lineRef.current?.setPath(path);
    // `key` resume `path` — evita setPath en cada render si el array es nuevo pero igual.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [map, key]);

  return null;
}

/** Circulo (radio en metros). No captura clicks: el mapa los sigue recibiendo. */
export function MapCircle({
  center,
  radius,
  color,
  fillOpacity = 0.12,
}: {
  center: LatLngLiteral;
  radius: number;
  color: string;
  fillOpacity?: number;
}) {
  const map = useMap();
  const circleRef = useRef<google.maps.Circle | null>(null);

  useEffect(() => {
    if (!map) return;
    const circle = new google.maps.Circle({ map, clickable: false });
    circleRef.current = circle;
    return () => {
      circle.setMap(null);
      circleRef.current = null;
    };
  }, [map]);

  useEffect(() => {
    circleRef.current?.setOptions({
      strokeColor: color,
      strokeOpacity: 0.85,
      strokeWeight: 2,
      fillColor: color,
      fillOpacity,
    });
  }, [map, color, fillOpacity]);

  useEffect(() => {
    circleRef.current?.setCenter(center);
  }, [map, center.lat, center.lng]); // eslint-disable-line react-hooks/exhaustive-deps

  useEffect(() => {
    circleRef.current?.setRadius(radius);
  }, [map, radius]);

  return null;
}

/**
 * Encaja el viewport a los puntos cada vez que cambian. `firstPadding` permite
 * un primer fit mas holgado; `maxZoom` evita acercar de mas con puntos juntos.
 */
export function FitBounds({
  points,
  padding = 40,
  firstPadding,
  maxZoom,
}: {
  points: LatLngLiteral[];
  padding?: number;
  firstPadding?: number;
  maxZoom?: number;
}) {
  const map = useMap();
  const ranOnceRef = useRef(false);
  const key = pathKey(points);

  useEffect(() => {
    if (!map || points.length === 0) return;
    const bounds = new google.maps.LatLngBounds();
    points.forEach((p) => bounds.extend(p));
    map.fitBounds(bounds, ranOnceRef.current ? padding : (firstPadding ?? padding));
    ranOnceRef.current = true;
    if (maxZoom === undefined) return;
    const listener = google.maps.event.addListenerOnce(map, "idle", () => {
      const z = map.getZoom();
      if (z !== undefined && z > maxZoom) map.setZoom(maxZoom);
    });
    return () => listener.remove();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [map, key, padding, firstPadding, maxZoom]);

  return null;
}

/**
 * Recentra cuando el padre sube `token` (geolocalizacion, carga de edicion).
 * Clicks/drags internos no lo suben, asi que no hay saltos.
 */
export function Recenter({ center, token, minZoom = 14 }: { center: LatLngLiteral; token: number; minZoom?: number }) {
  const map = useMap();
  useEffect(() => {
    if (!map) return;
    map.panTo(center);
    map.setZoom(Math.max(map.getZoom() ?? minZoom, minZoom));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [map, token]);
  return null;
}

/**
 * Encaja el viewport a un circulo (centro + radio * margin) cuando sube `token`.
 */
export function FitCircle({
  center,
  radius,
  token,
  margin = 1.2,
  padding = 20,
}: {
  center: LatLngLiteral;
  radius: number;
  token: number;
  margin?: number;
  padding?: number;
}) {
  const map = useMap();
  const geometry = useMapsLibrary("geometry");
  useEffect(() => {
    if (!map || !geometry) return;
    const bounds = new google.maps.LatLngBounds();
    for (const heading of [0, 90, 180, 270]) {
      bounds.extend(geometry.spherical.computeOffset(center, radius * margin, heading));
    }
    map.fitBounds(bounds, padding);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [map, geometry, token]);
  return null;
}
