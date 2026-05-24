"use client";

import Link from "next/link";
import Image from "next/image";
import { usePathname } from "next/navigation";
import { useQuery } from "@tanstack/react-query";
import { ShoppingBag, Layers, Settings, User } from "lucide-react";

import { RappixLogo } from "@/components/layout/rappix-logo";
import { getLogoUrl } from "@/lib/api/merchants-public";
import { getMyMerchant } from "@/lib/api/merchants-me";
import { listPending } from "@/lib/api/merchant-orders";
import { cn, initials } from "@/lib/utils";
import type { MerchantStatus } from "@/lib/api/types";

const NAV_ITEMS = [
  { href: "/console/pedidos", label: "Pedidos", icon: ShoppingBag, badgeQuery: true },
  { href: "/console/catalogo", label: "Catálogo", icon: Layers, badgeQuery: false },
  { href: "/console/configuracion", label: "Configuración", icon: Settings, badgeQuery: false },
  { href: "/console/cuenta", label: "Mi cuenta", icon: User, badgeQuery: false },
] as const;

const STATUS_DOT: Record<MerchantStatus, { color: string; label: string }> = {
  Draft: { color: "bg-muted-foreground", label: "Borrador" },
  Pending: { color: "bg-amber-500", label: "En revisión" },
  Active: { color: "bg-success", label: "Comercio activo" },
  Paused: { color: "bg-amber-500", label: "Pausado" },
  Suspended: { color: "bg-red-500", label: "Suspendido" },
  Rejected: { color: "bg-red-500", label: "Rechazado" },
};

export function ConsoleSidebar() {
  const pathname = usePathname();

  const merchant = useQuery({ queryKey: ["merchant", "me"], queryFn: getMyMerchant });
  const logoUrl = useQuery({
    queryKey: ["merchant", "logo-url", merchant.data?.id],
    queryFn: () => getLogoUrl(merchant.data!.id),
    enabled: !!merchant.data?.id && !!merchant.data?.logoObjectKey,
    staleTime: 50 * 60_000,
  });
  // Real "Pedidos pendientes" count — drives the sidebar badge.
  const pending = useQuery({
    queryKey: ["orders", "merchant-pending"],
    queryFn: () => listPending({ pageSize: 100 }),
    refetchInterval: 10_000,
    refetchOnWindowFocus: true,
  });
  const pendingCount = pending.data?.length ?? 0;

  const status = merchant.data?.status ?? "Draft";
  const dot = STATUS_DOT[status];

  return (
    <aside className="sticky top-0 flex h-screen w-60 shrink-0 flex-col border-r border-border bg-white">
      <div className="px-5 pt-5">
        <RappixLogo variant="dark" subtitle="COMERCIO" />
      </div>

      <nav className="mt-6 flex-1 space-y-1 px-3">
        {NAV_ITEMS.map((item) => {
          const active = pathname === item.href || pathname.startsWith(`${item.href}/`);
          const Icon = item.icon;
          return (
            <Link
              key={item.href}
              href={item.href}
              className={cn(
                "flex items-center justify-between gap-3 rounded-lg px-3 py-2.5 text-sm font-medium transition-colors",
                active ? "bg-brand text-white" : "text-foreground/70 hover:bg-muted",
              )}
            >
              <span className="flex items-center gap-3">
                <Icon className="h-4 w-4" />
                <span>{item.label}</span>
              </span>
              {item.badgeQuery && pendingCount > 0 ? (
                <span
                  className={cn(
                    "inline-flex h-5 min-w-5 items-center justify-center rounded-full px-1.5 text-xs font-semibold",
                    active ? "bg-white text-brand" : "bg-accent text-white",
                  )}
                >
                  {pendingCount}
                </span>
              ) : null}
            </Link>
          );
        })}
      </nav>

      <div className="border-t border-border p-3">
        <div className="flex items-center gap-3 rounded-lg bg-muted/40 p-2">
          {logoUrl.data ? (
            <Image
              src={logoUrl.data}
              alt={merchant.data?.name ?? "Comercio"}
              width={36}
              height={36}
              className="h-9 w-9 rounded-lg object-cover"
              unoptimized
            />
          ) : (
            <div className="flex h-9 w-9 items-center justify-center rounded-lg bg-accent text-xs font-bold text-white">
              {initials(merchant.data?.name)}
            </div>
          )}
          <div className="flex min-w-0 flex-1 flex-col leading-tight">
            <span className="truncate text-sm font-semibold text-foreground">
              {merchant.data?.name ?? "Mi comercio"}
            </span>
            <span className="flex items-center gap-1.5 text-xs text-muted-foreground">
              <span className={cn("h-1.5 w-1.5 rounded-full", dot.color)} />
              {dot.label}
            </span>
          </div>
        </div>
      </div>
    </aside>
  );
}
