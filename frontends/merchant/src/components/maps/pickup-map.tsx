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

function ClickHandler({ onPick, enabled }: { onPick: (lat: number, lng: number) => void; enabled: boolean }) {
  useMapEvents({
    click(e) {
      if (enabled) onPick(e.latlng.lat, e.latlng.lng);
    },
  });
  return null;
}

function Recenter({ lat, lng, token }: { lat: number; lng: number; token: number }) {
  const map = useMap();
  useEffect(() => {
    map.flyTo([lat, lng], Math.max(map.getZoom(), 14), { duration: 0.5 });
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [token]);
  return null;
}

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
 * Single-pin picker for the merchant's pickup location. Drag the pin or click
 * anywhere on the map to set lat/lng. The address text input lives in the
 * parent — there is no reverse geocoding (documented gap).
 */
export function PickupMap({
  latitude,
  longitude,
  recenterToken,
  onChange,
  className,
  interactive = true,
}: PickupMapProps) {
  return (
    <div className={cn("rounded-lg overflow-hidden border border-border h-72", className)}>
      <MapContainer
        center={[latitude, longitude]}
        zoom={15}
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
        <Marker
          position={[latitude, longitude]}
          icon={PIN_ICON}
          draggable={interactive}
          eventHandlers={{
            dragend(e) {
              const m = e.target as L.Marker;
              const ll = m.getLatLng();
              onChange(ll.lat, ll.lng);
            },
          }}
        />
        <ClickHandler onPick={onChange} enabled={interactive} />
        <Recenter lat={latitude} lng={longitude} token={recenterToken} />
        <InvalidateOnMount />
      </MapContainer>
    </div>
  );
}
