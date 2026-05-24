"use client";

import { useEffect, type ReactNode } from "react";

import { useAddressStore } from "@/lib/addresses";
import { useAuthStore } from "@/lib/auth/store";

/**
 * Hydrates auth + addresses stores from localStorage and wires the cross-
 * window events. Addresses are app-wide (the header pill needs them on every
 * page) so they hydrate at the root, not lazily.
 */
export function AuthProvider({ children }: { children: ReactNode }) {
  useEffect(() => {
    useAuthStore.getState().hydrate();
    useAddressStore.getState().hydrate();
  }, []);
  return <>{children}</>;
}
