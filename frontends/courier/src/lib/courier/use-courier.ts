"use client";

import { useQuery } from "@tanstack/react-query";

import { courierApi } from "@/lib/api/courier";
import type { CourierResponse } from "@/lib/api/types";

/**
 * Lee el perfil del courier (status + vehicle + lastLocation). Se llama desde
 * el home autenticado para enrutar el UI por estado y desde la pantalla
 * /vehiculo despues de guardar.
 *
 * staleTime 5s: el status es crucial para que las transiciones (Offline -> Online
 * -> Busy) se reflejen pronto en pantalla, pero no tan agresivo como para
 * martillar el endpoint en cada navegacion.
 */
export const courierQueryKey = ["courier", "me"] as const;

export function useCourier(enabled = true) {
  return useQuery<CourierResponse>({
    queryKey: [...courierQueryKey],
    queryFn: courierApi.me,
    enabled,
    staleTime: 5_000,
  });
}
