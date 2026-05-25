"use client";

// Stub minimo para que `next build` arranque limpio en el commit 1. El cliente
// real (con defaults retry/staleTime) se monta en el commit 3 junto a la auth.
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { useState, type ReactNode } from "react";

export function QueryProvider({ children }: { children: ReactNode }) {
  const [client] = useState(() => new QueryClient());
  return <QueryClientProvider client={client}>{children}</QueryClientProvider>;
}
