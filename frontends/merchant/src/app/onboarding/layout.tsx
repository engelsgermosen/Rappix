"use client";

import { useQuery } from "@tanstack/react-query";
import type { ReactNode } from "react";

import { RequireAuth } from "@/components/auth/require-auth";
import { RequireMerchantStatus } from "@/components/auth/require-merchant-status";
import { RappixLogo } from "@/components/layout/rappix-logo";
import { WizardSidebar } from "@/components/layout/wizard-sidebar";
import { completedStepsFor } from "@/components/onboarding/step-shell";
import { getMyMerchant } from "@/lib/api/merchants-me";
import { useAuthStore } from "@/lib/auth/store";

function OnboardingShell({ children }: { children: ReactNode }) {
  const merchant = useQuery({ queryKey: ["merchant", "me"], queryFn: getMyMerchant });
  const completed = completedStepsFor(merchant.data);

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

      <main className="container mx-auto flex-1 px-4 py-8 lg:py-12">
        <div className="mb-8 max-w-3xl">
          <h1 className="text-3xl font-bold text-foreground lg:text-4xl">Configura tu comercio</h1>
          <p className="mt-2 text-sm text-muted-foreground">
            Completa los siguientes pasos para enviar tu comercio a revisión. Te avisaremos por correo cuando esté aprobado.
          </p>
        </div>

        <div className="grid gap-6 lg:grid-cols-[280px,1fr]">
          <div>
            <WizardSidebar completed={completed} />
          </div>
          <div>{children}</div>
        </div>
      </main>
    </div>
  );
}

export default function OnboardingLayout({ children }: { children: ReactNode }) {
  return (
    <RequireAuth>
      <RequireMerchantStatus allow={["Draft"]}>
        <OnboardingShell>{children}</OnboardingShell>
      </RequireMerchantStatus>
    </RequireAuth>
  );
}
