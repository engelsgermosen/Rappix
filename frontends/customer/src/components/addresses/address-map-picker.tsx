"use client";

import { useMemo } from "react";

import { GoogleMap, MapPin, Recenter } from "@/components/map/google-map";
import { cn } from "@/lib/utils";

export type AddressMapPickerProps = {
  latitude: number;
  longitude: number;
  /** Bumped by the parent to force a programmatic recenter (geolocation, edit load). */
  recenterToken: number;
  onChange: (lat: number, lng: number) => void;
  className?: string;
};

/**
 * Interactive map for picking a delivery address (Google Maps).
 *
 * - Click anywhere → marker moves there, parent's onChange fires.
 * - Drag the marker → same.
 * - Parent can force a recenter by bumping `recenterToken`.
 *
 * Loaded via dynamic({ ssr: false }) by the address form dialog so the map
 * script only loads on the client. Google vector maps resize themselves once
 * the Radix Dialog finishes animating — no invalidateSize kick needed.
 */
export function AddressMapPicker({
  latitude,
  longitude,
  recenterToken,
  onChange,
  className,
}: AddressMapPickerProps) {
  const position = useMemo(() => ({ lat: latitude, lng: longitude }), [latitude, longitude]);

  return (
    <GoogleMap
      defaultCenter={position}
      defaultZoom={15}
      gestureHandling="greedy"
      zoomControl
      onClick={(p) => onChange(p.lat, p.lng)}
      className={cn("rounded-lg h-64 sm:h-72", className)}
    >
      <MapPin position={position} draggable onDragEnd={(p) => onChange(p.lat, p.lng)}>
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
