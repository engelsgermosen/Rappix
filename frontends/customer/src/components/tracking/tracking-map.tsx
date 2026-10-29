"use client";

import { useMemo, type CSSProperties } from "react";

import { FitBounds, GoogleMap, MapPin, MapPolyline } from "@/components/map/google-map";
import type { OrderTrackingResponse } from "@/lib/api/types";

const DOT_BASE: CSSProperties = {
  display: "inline-flex",
  alignItems: "center",
  justifyContent: "center",
  borderRadius: "50%",
  color: "#fff",
  border: "3px solid #fff",
  boxShadow: "0 1px 4px rgba(0,0,0,.3)",
  fontSize: 14,
};

type Props = {
  tracking: OrderTrackingResponse;
};

/**
 * Live tracking map (Google Maps). The page patches `tracking` from SignalR
 * pushes; markers/polylines update in place (MapPolyline reuses the same
 * google.maps.Polyline and calls setPath) and the viewport refits to keep
 * pickup, delivery and the courier visible. Straight lines only — no Routes API.
 */
export function TrackingMap({ tracking }: Props) {
  const pickup = useMemo(
    () => ({ lat: tracking.pickup.lat, lng: tracking.pickup.lng }),
    [tracking.pickup.lat, tracking.pickup.lng],
  );
  const delivery = useMemo(
    () => ({ lat: tracking.delivery.lat, lng: tracking.delivery.lng }),
    [tracking.delivery.lat, tracking.delivery.lng],
  );
  const courierLat = tracking.lastLocation?.lat;
  const courierLng = tracking.lastLocation?.lng;
  const courier = useMemo(
    () => (courierLat !== undefined && courierLng !== undefined ? { lat: courierLat, lng: courierLng } : null),
    [courierLat, courierLng],
  );

  const points = useMemo(
    () => (courier ? [pickup, delivery, courier] : [pickup, delivery]),
    [pickup, delivery, courier],
  );

  const center = courier ?? {
    lat: (pickup.lat + delivery.lat) / 2,
    lng: (pickup.lng + delivery.lng) / 2,
  };

  return (
    <div className="relative h-[420px] md:h-[480px] rounded-xl overflow-hidden border border-border">
      <GoogleMap
        defaultCenter={center}
        defaultZoom={14}
        gestureHandling="cooperative"
        zoomControl
        className="h-full rounded-none border-0"
      >
        <MapPin position={pickup} anchor="center" title="Comercio">
          <span style={{ ...DOT_BASE, width: 28, height: 28, background: "#D85A30" }}>●</span>
        </MapPin>
        <MapPin position={delivery} title="Tu dirección">
          <span
            style={{
              display: "inline-flex",
              alignItems: "center",
              justifyContent: "center",
              width: 28,
              height: 36,
              fontSize: 24,
            }}
          >
            📍
          </span>
        </MapPin>
        {courier && (
          <>
            <MapPin position={courier} anchor="center" title="Repartidor" zIndex={10}>
              <span style={{ ...DOT_BASE, width: 30, height: 30, background: "#534AB7" }}>🛵</span>
            </MapPin>
            <MapPolyline path={[pickup, courier]} color="#534AB7" weight={4} opacity={0.55} dashed />
            <MapPolyline path={[courier, delivery]} color="#534AB7" weight={4} opacity={0.9} />
          </>
        )}
        <FitBounds points={points} padding={40} maxZoom={15} />
      </GoogleMap>
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
