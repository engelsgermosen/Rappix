"use client";

import { useEffect, useRef } from "react";
import { toast } from "sonner";

import { courierApi } from "@/lib/api/courier";
import type { CourierStatus } from "@/lib/api/types";

/**
 * Heartbeat de ubicacion mientras el courier esta Online o Busy. Usa
 * `navigator.geolocation.watchPosition` (no llamadas repetidas a getCurrentPosition,
 * que el navegador puede capar/cachear y consume mas bateria) y throttle propio
 * para no martillar el endpoint.
 *
 * Cadencia:
 *   Busy   -> 5_000 ms  (queremos que el cliente vea el courier moviendose en vivo)
 *   Online ->12_000 ms  (idle: solo confirmamos que sigue cerca para matching)
 *
 * Permission denied -> toast persistente una sola vez (no spammeamos en cada tick).
 * El courier puede seguir Online sin location updates — Dispatch hace matching
 * solo con LastLocation conocida (sin update lo descarta del GEOSEARCH al siguiente).
 *
 * Errores HTTP del POST son fire-and-forget: no rompen el UI, los logs y el
 * siguiente tick reconcilian. Esta logica se ALINEA con la guia del COURIER_API_
 * MAP.md §D.4 ("retry on transient 5xx — next tick reconciles").
 */
export function useLocationHeartbeat({
  status,
}: {
  status: CourierStatus | undefined;
}) {
  const lastPostRef = useRef<number>(0);
  const lastPermissionToastRef = useRef<boolean>(false);

  useEffect(() => {
    const enabled = status === "Online" || status === "Busy";
    if (!enabled) return;
    if (typeof navigator === "undefined" || !navigator.geolocation) {
      // Entorno sin geolocation API (SSR o navegador muy viejo). Silencio.
      return;
    }

    const intervalMs = status === "Busy" ? 5_000 : 12_000;

    const watchId = navigator.geolocation.watchPosition(
      (pos) => {
        const now = Date.now();
        if (now - lastPostRef.current < intervalMs) return;
        lastPostRef.current = now;
        courierApi
          .reportLocation({ latitude: pos.coords.latitude, longitude: pos.coords.longitude })
          .catch(() => {
            // Fire-and-forget. No toast — el siguiente tick lo reintenta.
          });
      },
      (err) => {
        if (err.code === err.PERMISSION_DENIED && !lastPermissionToastRef.current) {
          lastPermissionToastRef.current = true;
          toast.error(
            "Activa la ubicacion en tu navegador para que el cliente pueda verte en el mapa.",
            { duration: 10_000 },
          );
        }
        // Otros errores (POSITION_UNAVAILABLE / TIMEOUT) son ruido normal en
        // ciudad — el siguiente tick reintenta solo.
      },
      {
        enableHighAccuracy: true,
        // El watchPosition tiene su propio throttle del navegador; estos
        // valores son sugerencias razonables.
        maximumAge: 5_000,
        timeout: 20_000,
      },
    );

    return () => {
      navigator.geolocation.clearWatch(watchId);
    };
  }, [status]);
}
