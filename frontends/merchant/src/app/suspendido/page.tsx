"use client";

import { useQuery } from "@tanstack/react-query";
import { ShieldAlert } from "lucide-react";

import { RequireAuth } from "@/components/auth/require-auth";
import { RappixLogo } from "@/components/layout/rappix-logo";
import { Card } from "@/components/ui/card";
import { getMyMerchant } from "@/lib/api/merchants-me";
import { useAuthStore } from "@/lib/auth/store";

function SuspendedScreen() {
  const merchant = useQuery({ queryKey: ["merchant", "me"], queryFn: getMyMerchant });
  const status = merchant.data?.status;

  const heading =
    status === "Rejected"
      ? "Tu solicitud no fue aprobada"
      : "Tu comercio está suspendido temporalmente";

  const body =
    status === "Rejected"
      ? "Después de revisar la documentación de tu comercio, no pudimos aprobar la cuenta. Si crees que es un error, contacta a soporte."
      : "Hemos pausado tu comercio. No estás recibiendo pedidos. Contacta a soporte para conocer el motivo y los pasos para reactivar la cuenta.";

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
        <Card className="w-full max-w-xl p-10 text-center">
          <div className="mx-auto mb-6 flex h-16 w-16 items-center justify-center rounded-2xl bg-red-50 text-red-600">
            <ShieldAlert className="h-8 w-8" />
          </div>
          <h1 className="text-2xl font-bold text-foreground">{heading}</h1>
          <p className="mx-auto mt-3 max-w-md text-sm text-muted-foreground">{body}</p>

          <div className="mt-8 space-y-2 text-sm">
            <p className="font-semibold text-foreground">¿Necesitas ayuda?</p>
            <p className="text-muted-foreground">
              Escribe a{" "}
              <a
                href="mailto:soporte@rappix.com"
                className="font-semibold text-brand hover:underline"
              >
                soporte@rappix.com
              </a>
            </p>
          </div>
        </Card>
      </main>
    </div>
  );
}

export default function SuspendidoPage() {
  return (
    <RequireAuth>
      <SuspendedScreen />
    </RequireAuth>
  );
}
