"use client";

import { useRouter } from "next/navigation";
import { useQuery } from "@tanstack/react-query";
import { ChevronLeft, ChevronRight } from "lucide-react";
import type { ReactNode } from "react";

import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card } from "@/components/ui/card";
import { ONBOARDING_STEPS } from "@/components/layout/wizard-sidebar";
import { getMyMerchant } from "@/lib/api/merchants-me";
import { cn } from "@/lib/utils";
import type { MerchantResponse } from "@/lib/api/types";

/**
 * Computes which steps already have backing data on the merchant aggregate.
 * Used to render the "✓" marks in the sidebar and to gate `submit-for-approval`.
 */
export function completedStepsFor(merchant: MerchantResponse | undefined): Set<number> {
  if (!merchant) return new Set();
  const done = new Set<number>();
  // Step 1: basic data (name + vertical are set in CreateDraft, so we require RNC to count as "done").
  if (merchant.rnc && merchant.rnc.trim().length > 0) done.add(1);
  // Step 2: at least one operating hours row.
  if (merchant.operatingHours.length > 0) done.add(2);
  // Step 3: at least one service area.
  if (merchant.serviceAreas.length > 0) done.add(3);
  // Step 4: pickup location is internal — the easiest read-back is whether
  // any service area exists, but pickup is independent. The backend exposes
  // it via the gRPC activation response; here we proxy via the presence of
  // a logo (next step) OR we trust the user finished it. Since /merchants/me
  // does not return the pickup directly, we treat step 4 as done if the
  // user has visited it and pressed Continuar — that flips a localStorage
  // flag. (Documented in FINDINGS.)
  // For now, we mark step 4 done by best-effort using localStorage.
  if (typeof window !== "undefined" && window.localStorage.getItem("rappix.merchant.pickup-done") === "1") {
    done.add(4);
  }
  // Step 5: logo uploaded.
  if (merchant.logoObjectKey && merchant.logoObjectKey.length > 0) done.add(5);
  return done;
}

export function StepShell({
  stepNumber,
  title,
  description,
  children,
  onBack,
  onContinue,
  continueDisabled,
  continueLabel = "Continuar",
}: {
  stepNumber: number;
  title: string;
  description: string;
  children: ReactNode;
  onBack?: () => void;
  onContinue: () => void;
  continueDisabled?: boolean;
  continueLabel?: string;
}) {
  const router = useRouter();
  const merchant = useQuery({ queryKey: ["merchant", "me"], queryFn: getMyMerchant });

  const totalSteps = ONBOARDING_STEPS.length;

  return (
    <Card className="overflow-hidden">
      {/* Header with step badge + progress bar */}
      <div className="border-b border-border px-6 py-5">
        <div className="flex items-center justify-between gap-4">
          <p className="text-xs font-semibold uppercase tracking-[0.18em] text-muted-foreground">
            Paso {stepNumber} de {totalSteps}
          </p>
          <Badge variant="draft">
            <span className={cn("h-1.5 w-1.5 rounded-full bg-muted-foreground")} />
            Estado: {merchant.data?.status ?? "—"}
          </Badge>
        </div>
        <h2 className="mt-1 text-2xl font-bold text-foreground">{title}</h2>
        <p className="mt-1 text-sm text-muted-foreground">{description}</p>

        {/* Progress bar */}
        <div className="mt-4 flex gap-1.5">
          {ONBOARDING_STEPS.map((s) => (
            <span
              key={s.number}
              className={cn(
                "h-1.5 flex-1 rounded-full",
                s.number < stepNumber ? "bg-brand" : s.number === stepNumber ? "bg-brand" : "bg-muted",
              )}
            />
          ))}
        </div>
      </div>

      {/* Step body */}
      <div className="px-6 py-6">{children}</div>

      {/* Footer with nav buttons */}
      <div className="flex items-center justify-between gap-3 border-t border-border px-6 py-4">
        <Button
          type="button"
          variant="outline"
          onClick={onBack ?? (() => router.back())}
        >
          <ChevronLeft className="h-4 w-4" />
          Atrás
        </Button>
        <Button type="button" onClick={onContinue} disabled={continueDisabled}>
          {continueLabel}
          <ChevronRight className="h-4 w-4" />
        </Button>
      </div>
    </Card>
  );
}
