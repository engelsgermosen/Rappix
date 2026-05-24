"use client";

import { useRouter } from "next/navigation";
import { useEffect } from "react";
import { useQuery } from "@tanstack/react-query";

import { getMyMerchant } from "@/lib/api/merchants-me";
import { useAuthStore } from "@/lib/auth/store";

/**
 * Status-aware root: reads /merchants/me and redirects per merchant.status.
 * No UI of its own — it's a router shell. The actual landing screens live
 * under /login, /onboarding/*, /en-revision, /suspendido and /console/*.
 */
export default function RootRouter() {
  const router = useRouter();
  const user = useAuthStore((s) => s.user);
  const hydrated = useAuthStore((s) => s.hydrated);

  const merchant = useQuery({
    queryKey: ["merchant", "me"],
    queryFn: getMyMerchant,
    enabled: !!user,
    retry: (failureCount, error) => {
      // The Draft row is created by a consumer right after register; allow a
      // few retries while it propagates.
      if (error instanceof Error && error.message.includes("404")) return failureCount < 5;
      return failureCount < 2;
    },
    retryDelay: 800,
  });

  useEffect(() => {
    if (!hydrated) return;
    if (!user) {
      router.replace("/login");
      return;
    }
    if (!merchant.data) return;
    switch (merchant.data.status) {
      case "Draft":
        router.replace("/onboarding/datos-basicos");
        break;
      case "Pending":
        router.replace("/en-revision");
        break;
      case "Active":
      case "Paused":
        router.replace("/console/pedidos");
        break;
      case "Suspended":
      case "Rejected":
        router.replace("/suspendido");
        break;
    }
  }, [hydrated, user, merchant.data, router]);

  return (
    <div className="flex min-h-screen items-center justify-center">
      <p className="text-sm text-muted-foreground">Cargando...</p>
    </div>
  );
}
