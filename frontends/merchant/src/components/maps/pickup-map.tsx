"use client";

import { useMemo } from "react";

import { GoogleMap, MapPin, Recenter } from "@/components/map/google-map";
import { cn } from "@/lib/utils";

export type PickupMapProps = {
  latitude: number;
  longitude: number;
  recenterToken: number;
  onChange: (lat: number, lng: number) => void;
  className?: string;
  /** When false the map renders but doesn't accept clicks/drags (used in mini preview). */
  interactive?: boolean;
};

/**
 * Single-pin picker for the merchant's pickup location (Google Maps). Drag the
 * pin or click anywhere on the map to set lat/lng. The address text input
 * lives in the parent — there is no reverse geocoding (documented gap).
 */
export function PickupMap({
  latitude,
  longitude,
  recenterToken,
  onChange,
  className,
  interactive = true,
}: PickupMapProps) {
  const position = useMemo(() => ({ lat: latitude, lng: longitude }), [latitude, longitude]);

  return (
    <GoogleMap
      defaultCenter={position}
      defaultZoom={15}
      gestureHandling={interactive ? "greedy" : "none"}
      zoomControl={interactive}
      onClick={interactive ? (p) => onChange(p.lat, p.lng) : undefined}
      className={cn("rounded-lg h-72", className)}
    >
      <MapPin position={position} draggable={interactive} onDragEnd={(p) => onChange(p.lat, p.lng)}>
        <span
          style={{
            display: "inline-flex",
            alignItems: "center",
            justifyContent: "center",
            width: 30,
            height: 38,
            fontSize: 28,
            lineHeight: 1,
            filter: "drop-shadow(0 2px 3px rgba(0,0,0,.3))",
          }}
        >
          📍
        </span>
      </MapPin>
      <Recenter center={position} token={recenterToken} />
    </GoogleMap>
  );
}
