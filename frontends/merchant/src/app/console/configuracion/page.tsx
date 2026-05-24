"use client";

import { useQuery } from "@tanstack/react-query";

import { DatosCard } from "@/components/configuracion/datos-card";
import { HorariosCard } from "@/components/configuracion/horarios-card";
import { LogoCard } from "@/components/configuracion/logo-card";
import { PickupCard } from "@/components/configuracion/pickup-card";
import { ZonaCard } from "@/components/configuracion/zona-card";
import { Card } from "@/components/ui/card";
import { getMyMerchant } from "@/lib/api/merchants-me";

export default function ConfiguracionPage() {
  const merchant = useQuery({ queryKey: ["merchant", "me"], queryFn: getMyMerchant });

  if (merchant.isPending) {
    return (
      <section className="px-8 py-8">
        <Card className="h-64 animate-pulse" />
      </section>
    );
  }

  if (!merchant.data) {
    return (
      <section className="px-8 py-8">
        <Card className="p-8 text-center text-sm text-muted-foreground">
          No pudimos cargar la configuración de tu comercio.
        </Card>
      </section>
    );
  }

  const m = merchant.data;

  return (
    <section className="space-y-6 px-8 py-8">
      <header>
        <h1 className="text-3xl font-bold text-foreground">Configuración</h1>
        <p className="mt-1 text-sm text-muted-foreground">Datos del comercio, horarios y zonas.</p>
      </header>

      <DatosCard merchant={m} />
      <HorariosCard merchant={m} />

      <div className="grid gap-6 lg:grid-cols-2">
        <PickupCard />
        <ZonaCard merchant={m} />
      </div>

      <LogoCard merchant={m} />
    </section>
  );
}
