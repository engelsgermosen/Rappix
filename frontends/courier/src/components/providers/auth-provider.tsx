"use client";

// Stub minimo. El AuthProvider real (hidrata Zustand + escucha el evento
// rappix:auth:changed) se monta en el commit 3.
import type { ReactNode } from "react";

export function AuthProvider({ children }: { children: ReactNode }) {
  return <>{children}</>;
}
