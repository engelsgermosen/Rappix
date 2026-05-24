"use client";

import { useRouter } from "next/navigation";
import { useEffect, type ReactNode } from "react";
import { useQuery } from "@tanstack/react-query";

import { getMyMerchant } from "@/lib/api/merchants-me";
import { useAuthStore } from "@/lib/auth/store";
import type { MerchantStatus } from "@/lib/api/types";

const ROUTE_FOR_STATUS: Record<MerchantStatus, string> = {
  Draft: "/onboarding/datos-basicos",
  Pending: "/en-revision",
  Active: "/console/pedidos",
  Paused: "/console/pedidos",
  Suspended: "/suspendido",
  Rejected: "/suspendido",
};

/**
 * Wraps a subtree that should only render for merchants in one of `allow`.
 * Reads `/merchants/me` and redirects elsewhere if the status doesn't match.
 *
 * Use this inside RequireAuth — it assumes there's a user.
 */
export function RequireMerchantStatus({
  allow,
  children,
}: {
  allow: MerchantStatus[];
  children: ReactNode;
}) {
  const router = useRouter();
  const user = useAuthStore((s) => s.user);

  const merchant = useQuery({
    queryKey: ["merchant", "me"],
    queryFn: getMyMerchant,
    enabled: !!user,
    // The Draft row is created by an async consumer; if the user just registered
    // it may not be there for a second. Don't blow up — just refetch on focus
    // until it appears.
    retry: (failureCount, error) => {
      if (error instanceof Error && error.message.includes("404")) return failureCount < 5;
      return failureCount < 2;
    },
    retryDelay: 800,
  });

  useEffect(() => {
    if (!merchant.data) return;
    if (!allow.includes(merchant.data.status)) {
      router.replace(ROUTE_FOR_STATUS[merchant.data.status]);
    }
  }, [merchant.data, allow, router]);

  if (merchant.isPending) {
    return (
      <div className="flex min-h-screen items-center justify-center">
        <p className="text-sm text-muted-foreground">Cargando tu comercio...</p>
      </div>
    );
  }

  if (merchant.isError) {
    return (
      <div className="flex min-h-screen items-center justify-center p-8 text-center">
        <div className="space-y-2">
          <p className="text-sm font-semibold text-destructive">No pudimos cargar tu comercio.</p>
          <p className="text-sm text-muted-foreground">
            Verifica tu conexión y vuelve a intentar. Si persiste, contacta a soporte.
          </p>
        </div>
      </div>
    );
  }

  if (!allow.includes(merchant.data.status)) {
    // Effect already triggered the replace; render nothing while it happens.
    return null;
  }

  return <>{children}</>;
}
