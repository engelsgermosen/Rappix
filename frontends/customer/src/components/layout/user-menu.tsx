"use client";

import { LogOut, MapPin, Package, User as UserIcon } from "lucide-react";
import Link from "next/link";
import { useRouter } from "next/navigation";

import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import { useAuthStore } from "@/lib/auth/store";
import { initials } from "@/lib/utils";

export function UserMenu() {
  const router = useRouter();
  const user = useAuthStore((s) => s.user);
  const logout = useAuthStore((s) => s.logout);

  if (!user) {
    return (
      <Link
        href="/login"
        className="inline-flex h-10 items-center gap-2 rounded-lg bg-white/15 px-4 text-sm font-medium text-white hover:bg-white/25 transition-colors"
      >
        Iniciar sesión
      </Link>
    );
  }

  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <button className="inline-flex items-center gap-2 rounded-full bg-white/10 pl-3 pr-1 py-1 hover:bg-white/20 transition-colors focus:outline-none focus:ring-2 focus:ring-white/40">
          <span className="text-white text-sm font-medium hidden sm:inline">{user.firstName}</span>
          <span className="inline-flex h-8 w-8 items-center justify-center rounded-full bg-accent text-white text-xs font-bold">
            {initials(`${user.firstName} ${user.lastName}`)}
          </span>
        </button>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="end">
        <DropdownMenuLabel>
          <div className="text-foreground/80 font-semibold normal-case tracking-normal">
            {user.firstName} {user.lastName}
          </div>
          <div className="text-xs text-muted-foreground normal-case tracking-normal">{user.email}</div>
        </DropdownMenuLabel>
        <DropdownMenuSeparator />
        <DropdownMenuItem asChild>
          <Link href="/cuenta" className="cursor-pointer">
            <UserIcon className="h-4 w-4" />
            Mi cuenta
          </Link>
        </DropdownMenuItem>
        <DropdownMenuItem asChild>
          <Link href="/pedidos" className="cursor-pointer">
            <Package className="h-4 w-4" />
            Mis pedidos
          </Link>
        </DropdownMenuItem>
        <DropdownMenuItem asChild>
          <Link href="/direcciones" className="cursor-pointer">
            <MapPin className="h-4 w-4" />
            Direcciones
          </Link>
        </DropdownMenuItem>
        <DropdownMenuSeparator />
        <DropdownMenuItem
          className="text-red-600 focus:text-red-700"
          onClick={async () => {
            await logout();
            router.push("/login");
          }}
        >
          <LogOut className="h-4 w-4" />
          Cerrar sesión
        </DropdownMenuItem>
      </DropdownMenuContent>
    </DropdownMenu>
  );
}
