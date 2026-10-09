"use client";

import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { useState, type ReactNode } from "react";

import { ApiError } from "@/lib/api/errors";

/**
 * QueryClient con los defaults del proyecto:
 *  - staleTime 30s: cache "caliente" — el courier polea current-assignment cada
 *    pocos segundos, no queremos hits redundantes al volver al tab.
 *  - refetchOnWindowFocus false: el polling explicito se encarga; el window
 *    focus es ruido para la pantalla activa en la calle.
 *  - retry: NUNCA reintenta 4xx semanticos (mal request/token/permiso/state),
 *    si el handler ya rechazo no insistimos; reintenta hasta 2 veces 5xx/red.
 */
export function QueryProvider({ children }: { children: ReactNode }) {
  const [client] = useState(
    () =>
      new QueryClient({
        defaultOptions: {
          queries: {
            staleTime: 30_000,
            gcTime: 5 * 60_000,
            refetchOnWindowFocus: false,
            retry: (failureCount, error) => {
              if (error instanceof ApiError && [400, 401, 403, 404, 409, 422].includes(error.status)) {
                return false;
              }
              return failureCount < 2;
            },
          },
          mutations: {
            retry: false,
          },
        },
      }),
  );

  return <QueryClientProvider client={client}>{children}</QueryClientProvider>;
}
