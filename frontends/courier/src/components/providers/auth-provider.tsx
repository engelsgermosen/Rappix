"use client";

import { useEffect, type ReactNode } from "react";

import { useAuthStore } from "@/lib/auth/store";

/**
 * Hidrata la store de auth desde localStorage al montar + suscribe al evento
 * `rappix:auth:changed` (escrito por tokenStore). Ningun otro store necesita
 * hidratacion en este portal — el courier no tiene carrito, direcciones,
 * historial, etc.
 */
export function AuthProvider({ children }: { children: ReactNode }) {
  useEffect(() => {
    useAuthStore.getState().hydrate();
  }, []);
  return <>{children}</>;
}
