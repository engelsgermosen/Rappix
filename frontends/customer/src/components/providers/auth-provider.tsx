"use client";

import { useEffect, type ReactNode } from "react";

import { useAuthStore } from "@/lib/auth/store";

/** Hydrates the auth store from localStorage and wires the cross-window event. */
export function AuthProvider({ children }: { children: ReactNode }) {
  useEffect(() => {
    useAuthStore.getState().hydrate();
  }, []);
  return <>{children}</>;
}
