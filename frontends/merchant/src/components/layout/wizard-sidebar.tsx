"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { Check } from "lucide-react";

import { Card } from "@/components/ui/card";
import { cn } from "@/lib/utils";

export type WizardStep = {
  number: number;
  href: string;
  title: string;
  subtitle: string;
};

export const ONBOARDING_STEPS: WizardStep[] = [
  {
    number: 1,
    href: "/onboarding/datos-basicos",
    title: "Datos básicos",
    subtitle: "Nombre, vertical y descripción",
  },
  {
    number: 2,
    href: "/onboarding/horarios",
    title: "Horarios",
    subtitle: "Cuándo está abierto",
  },
  {
    number: 3,
    href: "/onboarding/zona",
    title: "Zona de servicio",
    subtitle: "Área de cobertura",
  },
  {
    number: 4,
    href: "/onboarding/ubicacion",
    title: "Ubicación",
    subtitle: "Dirección de recogida",
  },
  {
    number: 5,
    href: "/onboarding/logo",
    title: "Logo",
    subtitle: "Imagen del comercio",
  },
];

/**
 * Left sidebar with the 5-step progress. Completed steps are derived from
 * the backend state passed in by the layout (rnc set, has hours, has zones,
 * has pickup, has logoObjectKey).
 */
export function WizardSidebar({ completed }: { completed: Set<number> }) {
  const pathname = usePathname();
  const currentIndex = ONBOARDING_STEPS.findIndex((s) => pathname.startsWith(s.href));

  return (
    <Card className="p-2">
      <ol className="space-y-1">
        {ONBOARDING_STEPS.map((step, idx) => {
          const isCompleted = completed.has(step.number);
          const isCurrent = idx === currentIndex;
          return (
            <li key={step.number}>
              <Link
                href={step.href}
                className={cn(
                  "flex items-start gap-3 rounded-lg px-3 py-3 transition-colors",
                  isCurrent
                    ? "bg-brand-50"
                    : isCompleted
                      ? "hover:bg-muted"
                      : "hover:bg-muted",
                )}
              >
                <div
                  className={cn(
                    "mt-0.5 flex h-7 w-7 shrink-0 items-center justify-center rounded-full text-xs font-bold",
                    isCompleted
                      ? "bg-success text-white"
                      : isCurrent
                        ? "bg-brand text-white"
                        : "bg-muted text-muted-foreground",
                  )}
                >
                  {isCompleted ? <Check className="h-4 w-4" /> : step.number}
                </div>
                <div className="min-w-0 flex-1">
                  <p
                    className={cn(
                      "text-sm font-semibold",
                      isCurrent ? "text-brand" : isCompleted ? "text-foreground" : "text-foreground/70",
                    )}
                  >
                    {step.title}
                  </p>
                  <p className="text-xs text-muted-foreground">{step.subtitle}</p>
                </div>
              </Link>
            </li>
          );
        })}
      </ol>
    </Card>
  );
}
