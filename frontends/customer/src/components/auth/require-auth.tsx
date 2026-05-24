"use client";

import { useRouter } from "next/navigation";
import { useEffect, type ReactNode } from "react";

import { useAuthStore } from "@/lib/auth/store";

/**
 * Client-side guard: redirects to /login?redirect=<current> when no session.
 * Renders `fallback` (or null) while resolving. Pages that must be private
 * wrap their content in this. We do this client-side because the JWT lives
 * only in localStorage today.
 */
export function RequireAuth({ children, fallback }: { children: ReactNode; fallback?: ReactNode }) {
  const router = useRouter();
  const user = useAuthStore((s) => s.user);
  const hydrated = useAuthStore((s) => s.hydrated);

  useEffect(() => {
    if (!hydrated) return;
    if (!user) {
      const redirect = typeof window !== "undefined" ? window.location.pathname + window.location.search : "/";
      router.replace(`/login?redirect=${encodeURIComponent(redirect)}`);
    }
  }, [hydrated, user, router]);

  if (!hydrated) return fallback ?? null;
  if (!user) return fallback ?? null;
  return <>{children}</>;
}
