"use client";

import L from "leaflet";
import { useEffect, useMemo } from "react";
import { Circle, MapContainer, Marker, TileLayer, useMap } from "react-leaflet";

import { cn } from "@/lib/utils";

const CENTER_ICON = L.divIcon({
  className: "",
  html: `<span style="display:inline-flex;align-items:center;justify-content:center;width:26px;height:26px;border-radius:50%;background:#534AB7;color:#fff;border:3px solid #fff;box-shadow:0 1px 4px rgba(0,0,0,.3);font-size:12px;">●</span>`,
  iconSize: [26, 26],
  iconAnchor: [13, 13],
});

const HANDLE_ICON = L.divIcon({
  className: "",
  html: `<span style="display:inline-flex;align-items:center;justify-content:center;width:20px;height:20px;border-radius:50%;background:#fff;border:3px solid #534AB7;box-shadow:0 1px 3px rgba(0,0,0,.3);"></span>`,
  iconSize: [20, 20],
  iconAnchor: [10, 10],
});

function InvalidateOnMount() {
  const map = useMap();
  useEffect(() => {
    const a = window.setTimeout(() => map.invalidateSize(), 50);
    const b = window.setTimeout(() => map.invalidateSize(), 350);
    return () => {
      window.clearTimeout(a);
      window.clearTimeout(b);
    };
  }, [map]);
  return null;
}

function FitToCircle({ lat, lng, radius, token }: { lat: number; lng: number; radius: number; token: number }) {
  const map = useMap();
  useEffect(() => {
    const center = L.latLng(lat, lng);
    const bounds = center.toBounds(radius * 2.4);
    map.fitBounds(bounds, { padding: [20, 20] });
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [token]);
  return null;
}

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
 * Circle service area editor: a draggable center marker + a draggable handle
 * marker positioned at `center + radius east`. Dragging the center moves the
 * whole circle; dragging the handle changes the radius. The slider in the
 * parent stays in sync.
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
  // Compute the handle's lat/lng: `radius` meters due east of the center.
  // L.LatLng.toBounds gives the east edge as the "right" of the rectangle.
  const handlePos = useMemo<[number, number]>(() => {
    const center = L.latLng(centerLat, centerLng);
    const east = center.toBounds(radiusMeters * 2).getEast();
    return [centerLat, east];
  }, [centerLat, centerLng, radiusMeters]);

  return (
    <div className={cn("rounded-lg overflow-hidden border border-border h-80", className)}>
      <MapContainer
        center={[centerLat, centerLng]}
        zoom={13}
        scrollWheelZoom={interactive}
        dragging={interactive}
        doubleClickZoom={interactive}
        zoomControl={interactive}
        className="h-full w-full"
      >
        <TileLayer
          attribution="© OpenStreetMap"
          url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png"
        />

        <Circle
          center={[centerLat, centerLng]}
          radius={radiusMeters}
          pathOptions={{
            color: "#534AB7",
            weight: 2,
            opacity: 0.85,
            dashArray: "6 6",
            fillColor: "#534AB7",
            fillOpacity: 0.12,
          }}
        />

        <Marker
          position={[centerLat, centerLng]}
          icon={CENTER_ICON}
          draggable={interactive}
          eventHandlers={{
            dragend(e) {
              const m = e.target as L.Marker;
              const ll = m.getLatLng();
              onChange({ centerLat: ll.lat, centerLng: ll.lng, radiusMeters });
            },
          }}
        />

        {interactive ? (
          <Marker
            position={handlePos}
            icon={HANDLE_ICON}
            draggable
            eventHandlers={{
              dragend(e) {
                const m = e.target as L.Marker;
                const ll = m.getLatLng();
                const newRadius = L.latLng(centerLat, centerLng).distanceTo(ll);
                const clamped = Math.max(300, Math.min(15_000, newRadius));
                onChange({ centerLat, centerLng, radiusMeters: clamped });
              },
            }}
          />
        ) : null}

        <FitToCircle lat={centerLat} lng={centerLng} radius={radiusMeters} token={recenterToken} />
        <InvalidateOnMount />
      </MapContainer>
    </div>
  );
}
