"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { Bike, ChevronDown, LogOut, User } from "lucide-react";

import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import { RappixLogo } from "@/components/layout/rappix-logo";
import { Badge } from "@/components/ui/badge";
import { useAuthStore } from "@/lib/auth/store";
import type { CourierStatus } from "@/lib/api/types";
import { formatStatus } from "@/lib/courier/format";
import { cn } from "@/lib/utils";

/**
 * Header del courier autenticado: logo, badge de status (con dot animado si
 * Online/Busy), dropdown con "Mi cuenta" + "Configurar vehiculo" + cerrar
 * sesion. Compartido por todas las pantallas autenticadas (`/`, `/vehiculo`,
 * `/cuenta`).
 */
export function AppHeader({ status }: { status?: CourierStatus }) {
  const router = useRouter();
  const user = useAuthStore((s) => s.user);
  const logout = useAuthStore((s) => s.logout);

  const initials = user
    ? `${user.firstName[0] ?? ""}${user.lastName[0] ?? ""}`.toUpperCase() || "?"
    : "?";

  async function onLogout() {
    await logout();
    router.replace("/login");
  }

  return (
    <header className="sticky top-0 z-30 border-b border-border bg-white/95 backdrop-blur">
      <div className="mx-auto flex max-w-screen-2xl items-center justify-between gap-4 px-4 py-3 md:px-6">
        <Link href="/" className="flex items-center" aria-label="Inicio">
          <RappixLogo variant="dark" />
        </Link>

        <div className="flex items-center gap-3">
          {status ? <StatusBadge status={status} /> : null}

          <DropdownMenu>
            <DropdownMenuTrigger asChild>
              <button
                type="button"
                className="flex items-center gap-2 rounded-lg border border-border bg-white px-2.5 py-1.5 text-sm hover:bg-muted/60"
              >
                <span className="grid h-7 w-7 place-items-center rounded-full bg-brand text-xs font-semibold text-white">
                  {initials}
                </span>
                <span className="hidden text-foreground sm:inline">{user?.firstName ?? "Mi cuenta"}</span>
                <ChevronDown className="h-4 w-4 text-muted-foreground" />
              </button>
            </DropdownMenuTrigger>
            <DropdownMenuContent align="end" sideOffset={8} className="min-w-[200px]">
              <DropdownMenuLabel className="text-xs text-muted-foreground">
                {user?.email}
              </DropdownMenuLabel>
              <DropdownMenuSeparator />
              <DropdownMenuItem asChild>
                <Link href="/vehiculo" className="flex items-center gap-2">
                  <Bike className="h-4 w-4" /> Mi vehiculo
                </Link>
              </DropdownMenuItem>
              <DropdownMenuItem asChild>
                <Link href="/cuenta" className="flex items-center gap-2">
                  <User className="h-4 w-4" /> Mi cuenta
                </Link>
              </DropdownMenuItem>
              <DropdownMenuSeparator />
              <DropdownMenuItem onClick={onLogout} className="text-reject focus:text-reject">
                <LogOut className="h-4 w-4" /> Cerrar sesion
              </DropdownMenuItem>
            </DropdownMenuContent>
          </DropdownMenu>
        </div>
      </div>
    </header>
  );
}

function StatusBadge({ status }: { status: CourierStatus }) {
  const cfg = {
    Offline: { dot: "bg-muted-foreground", text: "text-muted-foreground", border: "border-border" },
    Online:  { dot: "bg-success animate-pulse-online", text: "text-success-700", border: "border-success-100" },
    Busy:    { dot: "bg-accent animate-pulse-online", text: "text-accent-700", border: "border-accent-100" },
  }[status];

  return (
    <Badge variant="outline" className={cn("gap-1.5 border bg-white", cfg.border)}>
      <span className={cn("h-1.5 w-1.5 rounded-full", cfg.dot)} />
      <span className={cn("text-xs font-semibold", cfg.text)}>{formatStatus(status)}</span>
    </Badge>
  );
}
