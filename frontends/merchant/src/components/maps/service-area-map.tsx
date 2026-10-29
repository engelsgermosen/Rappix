"use client";

import { useMapsLibrary } from "@vis.gl/react-google-maps";
import { useMemo, type CSSProperties } from "react";

import { FitCircle, GoogleMap, MapCircle, MapPin, type LatLngLiteral } from "@/components/map/google-map";
import { cn } from "@/lib/utils";

const MIN_RADIUS_M = 300;
const MAX_RADIUS_M = 15_000;

const CENTER_ICON: CSSProperties = {
  display: "inline-flex",
  alignItems: "center",
  justifyContent: "center",
  width: 26,
  height: 26,
  borderRadius: "50%",
  background: "#534AB7",
  color: "#fff",
  border: "3px solid #fff",
  boxShadow: "0 1px 4px rgba(0,0,0,.3)",
  fontSize: 12,
};

const HANDLE_ICON: CSSProperties = {
  display: "inline-flex",
  width: 20,
  height: 20,
  borderRadius: "50%",
  background: "#fff",
  border: "3px solid #534AB7",
  boxShadow: "0 1px 3px rgba(0,0,0,.3)",
};

export type ServiceAreaMapProps = {
  centerLat: number;
  centerLng: number;
  radiusMeters: number;
  /** Bumped by the parent to force a fit (initial load, "reset"). */
  recenterToken: number;
  onChange: (next: { centerLat: number; centerLng: number; radiusMeters: number }) => void;
  className?: string;
  interactive?: boolean;
};

/**
 * Drag handle at `center + radius` due east. Needs the Maps `geometry` library,
 * so it lives inside the map (APIProvider) and renders nothing until it loads.
 */
function RadiusHandle({
  center,
  radiusMeters,
  onRadiusChange,
}: {
  center: LatLngLiteral;
  radiusMeters: number;
  onRadiusChange: (radiusMeters: number) => void;
}) {
  const geometry = useMapsLibrary("geometry");

  const handlePos = useMemo(() => {
    if (!geometry) return null;
    const east = geometry.spherical.computeOffset(center, radiusMeters, 90);
    return { lat: east.lat(), lng: east.lng() };
  }, [geometry, center, radiusMeters]);

  if (!geometry || !handlePos) return null;

  return (
    <MapPin
      position={handlePos}
      anchor="center"
      draggable
      onDragEnd={(p) => {
        const newRadius = geometry.spherical.computeDistanceBetween(center, p);
        onRadiusChange(Math.max(MIN_RADIUS_M, Math.min(MAX_RADIUS_M, newRadius)));
      }}
    >
      <span style={HANDLE_ICON} />
    </MapPin>
  );
}

/**
 * Circle service area editor (Google Maps): a draggable center marker + a
 * draggable handle marker positioned at `center + radius east`. Dragging the
 * center moves the whole circle; dragging the handle changes the radius. The
 * slider in the parent stays in sync.
 *
 * Polygon support is out of MVP scope (see plan §11). The backend already
 * accepts polygons — only the editor is missing.
 */
export function ServiceAreaMap({
  centerLat,
  centerLng,
  radiusMeters,
  recenterToken,
  onChange,
  className,
  interactive = true,
}: ServiceAreaMapProps) {
  const center = useMemo(() => ({ lat: centerLat, lng: centerLng }), [centerLat, centerLng]);

  return (
    <GoogleMap
      defaultCenter={center}
      defaultZoom={13}
      gestureHandling={interactive ? "greedy" : "none"}
      zoomControl={interactive}
      className={cn("rounded-lg h-80", className)}
    >
      <MapCircle center={center} radius={radiusMeters} color="#534AB7" />

      <MapPin
        position={center}
        anchor="center"
        draggable={interactive}
        onDragEnd={(p) => onChange({ centerLat: p.lat, centerLng: p.lng, radiusMeters })}
      >
        <span style={CENTER_ICON}>●</span>
      </MapPin>

      {interactive ? (
        <RadiusHandle
          center={center}
          radiusMeters={radiusMeters}
          onRadiusChange={(r) => onChange({ centerLat, centerLng, radiusMeters: r })}
        />
      ) : null}

      <FitCircle center={center} radius={radiusMeters} token={recenterToken} />
    </GoogleMap>
  );
}
