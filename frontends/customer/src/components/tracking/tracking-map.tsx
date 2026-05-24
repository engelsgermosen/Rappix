"use client";

import L from "leaflet";
import { useEffect, useMemo, useRef } from "react";
import { MapContainer, Marker, Polyline, TileLayer, useMap } from "react-leaflet";

import type { OrderTrackingResponse } from "@/lib/api/types";

// Fix default-marker URLs in webpack/turbopack — Leaflet's images break otherwise.
// We replace them with inline SVG data URIs to keep the page self-contained.
const MERCHANT_ICON = L.divIcon({
  className: "",
  html: `<span style="display:inline-flex;align-items:center;justify-content:center;width:28px;height:28px;border-radius:50%;background:#D85A30;color:#fff;border:3px solid #fff;box-shadow:0 1px 4px rgba(0,0,0,.3);font-size:14px;">●</span>`,
  iconSize: [28, 28],
  iconAnchor: [14, 14],
});

const COURIER_ICON = L.divIcon({
  className: "",
  html: `<span style="display:inline-flex;align-items:center;justify-content:center;width:30px;height:30px;border-radius:50%;background:#534AB7;color:#fff;border:3px solid #fff;box-shadow:0 1px 4px rgba(0,0,0,.3);font-size:14px;">🛵</span>`,
  iconSize: [30, 30],
  iconAnchor: [15, 15],
});

const DELIVERY_ICON = L.divIcon({
  className: "",
  html: `<span style="display:inline-flex;align-items:center;justify-content:center;width:28px;height:36px;font-size:24px;">📍</span>`,
  iconSize: [28, 36],
  iconAnchor: [14, 36],
});

type Props = {
  tracking: OrderTrackingResponse;
};

function FitBounds({ points }: { points: [number, number][] }) {
  const map = useMap();
  useEffect(() => {
    if (points.length === 0) return;
    const bounds = L.latLngBounds(points.map(([lat, lng]) => L.latLng(lat, lng)));
    map.fitBounds(bounds, { padding: [40, 40], maxZoom: 15 });
  }, [points, map]);
  return null;
}

export function TrackingMap({ tracking }: Props) {
  const mapRef = useRef<L.Map | null>(null);

  const points = useMemo(() => {
    const ps: [number, number][] = [];
    ps.push([tracking.pickup.lat, tracking.pickup.lng]);
    ps.push([tracking.delivery.lat, tracking.delivery.lng]);
    if (tracking.lastLocation) ps.push([tracking.lastLocation.lat, tracking.lastLocation.lng]);
    return ps;
  }, [tracking]);

  const center: [number, number] = tracking.lastLocation
    ? [tracking.lastLocation.lat, tracking.lastLocation.lng]
    : [(tracking.pickup.lat + tracking.delivery.lat) / 2, (tracking.pickup.lng + tracking.delivery.lng) / 2];

  return (
    <div className="relative h-[420px] md:h-[480px] rounded-xl overflow-hidden border border-border">
      <MapContainer
        center={center}
        zoom={14}
        scrollWheelZoom={false}
        ref={(m) => { mapRef.current = m as L.Map | null; }}
        className="h-full w-full"
      >
        <TileLayer
          attribution="© OpenStreetMap"
          url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png"
        />
        <Marker position={[tracking.pickup.lat, tracking.pickup.lng]} icon={MERCHANT_ICON} />
        <Marker position={[tracking.delivery.lat, tracking.delivery.lng]} icon={DELIVERY_ICON} />
        {tracking.lastLocation && (
          <Marker
            position={[tracking.lastLocation.lat, tracking.lastLocation.lng]}
            icon={COURIER_ICON}
          />
        )}
        {tracking.lastLocation && (
          <>
            <Polyline
              positions={[
                [tracking.pickup.lat, tracking.pickup.lng],
                [tracking.lastLocation.lat, tracking.lastLocation.lng],
              ]}
              pathOptions={{ color: "#534AB7", weight: 4, opacity: 0.55, dashArray: "6 8" }}
            />
            <Polyline
              positions={[
                [tracking.lastLocation.lat, tracking.lastLocation.lng],
                [tracking.delivery.lat, tracking.delivery.lng],
              ]}
              pathOptions={{ color: "#534AB7", weight: 4, opacity: 0.9 }}
            />
          </>
        )}
        <FitBounds points={points} />
      </MapContainer>
      <div className="absolute top-3 left-3 inline-flex items-center gap-1.5 rounded-full bg-white px-3 py-1 text-xs font-semibold shadow-md">
        <span className="h-2 w-2 rounded-full bg-emerald-500 animate-pulse" />
        En vivo
      </div>
      <div className="absolute bottom-3 left-3 flex flex-wrap gap-2 text-[11px]">
        <span className="inline-flex items-center gap-1 bg-white/95 rounded-full px-2.5 py-1 shadow-sm">
          <span className="h-2 w-2 rounded-full bg-accent" /> Comercio
        </span>
        <span className="inline-flex items-center gap-1 bg-white/95 rounded-full px-2.5 py-1 shadow-sm">
          <span className="h-2 w-2 rounded-full bg-brand" /> Repartidor
        </span>
        <span className="inline-flex items-center gap-1 bg-white/95 rounded-full px-2.5 py-1 shadow-sm">
          <span className="h-2 w-2 rounded-full bg-blue-500" /> Tu dirección
        </span>
      </div>
    </div>
  );
}
