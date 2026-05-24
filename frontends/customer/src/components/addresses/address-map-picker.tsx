"use client";

import L from "leaflet";
import { useEffect } from "react";
import { MapContainer, Marker, TileLayer, useMap, useMapEvents } from "react-leaflet";

import { cn } from "@/lib/utils";

const PIN_ICON = L.divIcon({
  className: "",
  html: `<span style="display:inline-flex;align-items:center;justify-content:center;width:30px;height:38px;font-size:28px;line-height:1;filter:drop-shadow(0 2px 3px rgba(0,0,0,.3));">📍</span>`,
  iconSize: [30, 38],
  iconAnchor: [15, 36],
});

/**
 * Captures map clicks and reports the (lat, lng) to the parent so the
 * marker can move + the form state can update.
 */
function ClickHandler({ onPick }: { onPick: (lat: number, lng: number) => void }) {
  useMapEvents({
    click(e) {
      onPick(e.latlng.lat, e.latlng.lng);
    },
  });
  return null;
}

/**
 * Recenters / re-zooms the map when the latitude/longitude props change
 * EXTERNALLY (geolocation result, edit-mode load). Internal clicks/drags
 * already moved the camera implicitly so this only matters for jumps.
 */
function Recenter({ lat, lng, key }: { lat: number; lng: number; key: number }) {
  const map = useMap();
  useEffect(() => {
    map.flyTo([lat, lng], Math.max(map.getZoom(), 14), { duration: 0.5 });
    // `key` is the "external change" tick — parent bumps it when it
    // wants a forced recenter (geolocation, edit load).
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [key]);
  return null;
}

/**
 * Fixes Leaflet's typical "tiles only render a quarter of the container"
 * bug inside a portaled Radix Dialog. The dialog finishes its open
 * animation a few hundred ms after mount, so the map needs a kick to
 * recompute its size once the container is at its final dimensions.
 */
function InvalidateOnMount() {
  const map = useMap();
  useEffect(() => {
    const id = window.setTimeout(() => map.invalidateSize(), 50);
    const id2 = window.setTimeout(() => map.invalidateSize(), 350);
    return () => {
      window.clearTimeout(id);
      window.clearTimeout(id2);
    };
  }, [map]);
  return null;
}

export type AddressMapPickerProps = {
  latitude: number;
  longitude: number;
  /** Bumped by the parent to force a programmatic recenter (geolocation, edit load). */
  recenterToken: number;
  onChange: (lat: number, lng: number) => void;
  className?: string;
};

/**
 * Interactive map for picking a delivery address.
 *
 * - Click anywhere → marker moves there, parent's onChange fires.
 * - Drag the marker → same.
 * - Parent can force a recenter by bumping `recenterToken`.
 *
 * Loaded via dynamic({ ssr: false }) by the address form dialog —
 * Leaflet imports `window` at module load, which crashes SSR.
 */
export function AddressMapPicker({
  latitude,
  longitude,
  recenterToken,
  onChange,
  className,
}: AddressMapPickerProps) {
  return (
    <div className={cn("rounded-lg overflow-hidden border border-border h-64 sm:h-72", className)}>
      <MapContainer
        center={[latitude, longitude]}
        zoom={15}
        scrollWheelZoom
        className="h-full w-full"
      >
        <TileLayer
          attribution="© OpenStreetMap"
          url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png"
        />
        <Marker
          position={[latitude, longitude]}
          icon={PIN_ICON}
          draggable
          eventHandlers={{
            dragend(e) {
              const m = e.target as L.Marker;
              const ll = m.getLatLng();
              onChange(ll.lat, ll.lng);
            },
          }}
        />
        <ClickHandler onPick={onChange} />
        <Recenter lat={latitude} lng={longitude} key={recenterToken} />
        <InvalidateOnMount />
      </MapContainer>
    </div>
  );
}
