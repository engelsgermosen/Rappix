"use client";

import { useQuery } from "@tanstack/react-query";

import { courierApi } from "@/lib/api/courier";
import type { CourierStatus, CurrentAssignmentResponse } from "@/lib/api/types";

/**
 * Polea GET /me/current-assignment con cadencia adaptativa segun el status
 * del courier:
 *
 *  - status === "Busy"   → cada 3s (queremos detectar pronto cuando el
 *                          customer cancela o el sistema libera la asignacion).
 *  - status === "Online" → cada 5s (esperando una asignacion nueva).
 *  - otherwise           → no polea (Offline, o sin perfil aun).
 *
 * Devuelve `null` cuando no hay asignacion (204 No Content normalizado por
 * `courierApi.currentAssignment`).
 *
 * `refetchOnWindowFocus: true` para que volver al tab traiga el estado real
 * inmediato (no esperar al proximo tick).
 */
export const currentAssignmentQueryKey = ["courier", "current-assignment"] as const;

export function useCurrentAssignment(status: CourierStatus | undefined) {
  const intervalMs =
    status === "Busy" ? 3_000 : status === "Online" ? 5_000 : false;

  return useQuery<CurrentAssignmentResponse | null>({
    queryKey: [...currentAssignmentQueryKey],
    queryFn: courierApi.currentAssignment,
    enabled: status === "Online" || status === "Busy",
    refetchInterval: intervalMs,
    refetchOnWindowFocus: true,
    staleTime: 0,
  });
}
