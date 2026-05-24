"use client";

import { useEffect, type ReactNode } from "react";

import { useAuthStore } from "@/lib/auth/store";

/**
 * Hydrates the merchant auth store from localStorage on mount and wires the
 * cross-component `rappix:auth:changed` event. No other stores need hydration
 * in this portal (no cart, no addresses).
 */
export function AuthProvider({ children }: { children: ReactNode }) {
  useEffect(() => {
    useAuthStore.getState().hydrate();
  }, []);
  return <>{children}</>;
}
