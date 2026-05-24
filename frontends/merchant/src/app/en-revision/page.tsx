"use client";

import { useRouter } from "next/navigation";
import { useEffect } from "react";
import { useQuery } from "@tanstack/react-query";
import { ArrowRight, BookOpen, Receipt, ShoppingBag } from "lucide-react";

import { RequireAuth } from "@/components/auth/require-auth";
import { RappixLogo } from "@/components/layout/rappix-logo";
import { Badge } from "@/components/ui/badge";
import { Card } from "@/components/ui/card";
import { getMyMerchant } from "@/lib/api/merchants-me";
import { useAuthStore } from "@/lib/auth/store";

function PendingScreen() {
  const router = useRouter();
  const user = useAuthStore((s) => s.user);

  const merchant = useQuery({
    queryKey: ["merchant", "me"],
    queryFn: getMyMerchant,
    refetchInterval: 15_000,
  });

  // The portal stops polling and redirects out as soon as status changes.
  useEffect(() => {
    if (!merchant.data) return;
    if (merchant.data.status !== "Pending") {
      router.replace("/");
    }
  }, [merchant.data, router]);

  return (
    <div className="flex min-h-screen flex-col bg-background">
      <header className="border-b border-border bg-white">
        <div className="container mx-auto flex h-16 items-center justify-between">
          <RappixLogo variant="dark" />
          <button
            onClick={() => useAuthStore.getState().logout()}
            className="text-sm text-muted-foreground hover:text-foreground"
          >
            Cerrar sesión
          </button>
        </div>
      </header>

      <main className="container mx-auto flex flex-1 items-center justify-center px-4 py-12">
        <Card className="w-full max-w-2xl p-10 text-center">
          {/* Animated progress ring around the R logo */}
          <div className="relative mx-auto mb-6 flex h-28 w-28 items-center justify-center">
            <svg className="absolute inset-0 h-full w-full -rotate-90" viewBox="0 0 100 100">
              <circle cx="50" cy="50" r="46" fill="none" stroke="hsl(240 4.8% 95.9%)" strokeWidth="4" />
              <circle
                cx="50"
                cy="50"
                r="46"
                fill="none"
                stroke="#534AB7"
                strokeWidth="4"
                strokeLinecap="round"
                strokeDasharray="289"
                strokeDashoffset="200"
                className="animate-[spin_3s_linear_infinite]"
                style={{ transformOrigin: "center" }}
              />
            </svg>
            <div className="flex h-20 w-20 items-center justify-center rounded-2xl bg-brand-50 text-3xl font-bold text-brand">
              R
            </div>
          </div>

          <Badge variant="pending" className="mx-auto mb-4">
            <span className="h-1.5 w-1.5 rounded-full bg-amber-500" />
            Esperando aprobación
          </Badge>

          <h1 className="text-3xl font-bold text-foreground">Tu comercio está en revisión</h1>
          <p className="mx-auto mt-3 max-w-md text-sm text-muted-foreground">
            Nuestro equipo revisará tu perfil y documentación. El proceso suele tomar entre{" "}
            <span className="font-semibold text-foreground">4 y 24 horas hábiles</span>. Te enviaremos un correo a{" "}
            <span className="font-semibold text-foreground">{user?.email ?? "tu cuenta"}</span> cuando esté listo.
          </p>

          <div className="mt-8 rounded-xl border border-brand-100 bg-brand-50/60 p-5 text-left">
            <p className="text-xs font-semibold uppercase tracking-wider text-brand-700">Mientras tanto</p>
            <ul className="mt-3 space-y-2 text-sm text-foreground/80">
              <li className="flex items-start gap-2">
                <ShoppingBag className="mt-0.5 h-4 w-4 shrink-0 text-brand" />
                <span>El catálogo se habilita cuando seas aprobado.</span>
              </li>
              <li className="flex items-start gap-2">
                <Receipt className="mt-0.5 h-4 w-4 shrink-0 text-brand" />
                <span>Revisa tus datos personales en Mi cuenta.</span>
              </li>
              <li className="flex items-start gap-2">
                <BookOpen className="mt-0.5 h-4 w-4 shrink-0 text-brand" />
                <span>
                  Lee la guía de mejores prácticas para llegar con todo listo el día que tu comercio se active.
                </span>
              </li>
            </ul>
          </div>

          <p className="mt-6 inline-flex items-center gap-2 text-xs text-muted-foreground">
            <span className="inline-block h-2 w-2 animate-pulse rounded-full bg-brand" />
            Verificamos el estado automáticamente
            <ArrowRight className="h-3 w-3" />
            cada 15 s
          </p>
        </Card>
      </main>
    </div>
  );
}

export default function EnRevisionPage() {
  return (
    <RequireAuth>
      <PendingScreen />
    </RequireAuth>
  );
}
