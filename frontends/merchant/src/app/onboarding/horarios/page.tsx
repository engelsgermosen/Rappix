"use client";

import { useRouter } from "next/navigation";
import { useEffect, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";

import { StepShell } from "@/components/onboarding/step-shell";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Switch } from "@/components/ui/switch";
import { getMyMerchant, updateOperatingHours } from "@/lib/api/merchants-me";
import type { DayOfWeek, OperatingHoursRangeRequest } from "@/lib/api/types";
import { DAYS_ORDERED, DAY_LABEL } from "@/lib/utils";
import { describeError } from "@/lib/errors";

type DayState = {
  open: boolean;
  opensAt: string;
  closesAt: string;
};

const DEFAULT_RANGE: DayState = { open: true, opensAt: "11:00", closesAt: "22:00" };

export default function HorariosPage() {
  const router = useRouter();
  const queryClient = useQueryClient();
  const merchant = useQuery({ queryKey: ["merchant", "me"], queryFn: getMyMerchant });

  const [days, setDays] = useState<Record<DayOfWeek, DayState>>(() => {
    const initial: Record<string, DayState> = {};
    for (const d of DAYS_ORDERED) initial[d] = { ...DEFAULT_RANGE };
    return initial as Record<DayOfWeek, DayState>;
  });

  // Prefill from server state.
  useEffect(() => {
    if (!merchant.data) return;
    const next: Record<string, DayState> = {};
    for (const d of DAYS_ORDERED) next[d] = { ...DEFAULT_RANGE };
    for (const row of merchant.data.operatingHours) {
      next[row.dayOfWeek] = { open: true, opensAt: row.opensAt, closesAt: row.closesAt };
    }
    // Mark days the merchant DOESN'T have as closed.
    for (const d of DAYS_ORDERED) {
      const hasRow = merchant.data.operatingHours.some((h) => h.dayOfWeek === d);
      if (!hasRow) next[d] = { ...next[d]!, open: false };
    }
    setDays(next as Record<DayOfWeek, DayState>);
  }, [merchant.data]);

  const save = useMutation({
    mutationFn: (hours: OperatingHoursRangeRequest[]) => updateOperatingHours({ hours }),
    onSuccess: (data) => {
      queryClient.setQueryData(["merchant", "me"], data);
      router.push("/onboarding/zona");
    },
    onError: (err) => toast.error(describeError(err)),
  });

  function onContinue() {
    const ranges: OperatingHoursRangeRequest[] = [];
    for (const d of DAYS_ORDERED) {
      const v = days[d]!;
      if (!v.open) continue;
      if (!v.opensAt || !v.closesAt) continue;
      if (v.opensAt >= v.closesAt) {
        toast.error(`En ${DAY_LABEL[d]}, la hora de cierre debe ser posterior a la de apertura.`);
        return;
      }
      ranges.push({ dayOfWeek: d, opensAt: v.opensAt, closesAt: v.closesAt });
    }
    if (ranges.length === 0) {
      toast.error("Tienes que abrir al menos un día.");
      return;
    }
    save.mutate(ranges);
  }

  return (
    <StepShell
      stepNumber={2}
      title="Horarios"
      description="Define los horarios de atención. Los clientes solo podrán pedir cuando estés abierto."
      onBack={() => router.push("/onboarding/datos-basicos")}
      onContinue={onContinue}
      continueLabel={save.isPending ? "Guardando..." : "Continuar"}
      continueDisabled={save.isPending}
    >
      <div className="space-y-3">
        {DAYS_ORDERED.map((d) => {
          const v = days[d]!;
          return (
            <div
              key={d}
              className="flex flex-wrap items-center gap-4 rounded-lg border border-border bg-white p-4"
            >
              <div className="w-24 shrink-0">
                <Label className="text-sm font-semibold">{DAY_LABEL[d]}</Label>
              </div>
              <div className="flex flex-1 gap-3">
                <Input
                  type="time"
                  value={v.opensAt}
                  disabled={!v.open}
                  onChange={(e) =>
                    setDays((prev) => ({ ...prev, [d]: { ...prev[d]!, opensAt: e.target.value } }))
                  }
                  className="max-w-[140px]"
                />
                <Input
                  type="time"
                  value={v.closesAt}
                  disabled={!v.open}
                  onChange={(e) =>
                    setDays((prev) => ({ ...prev, [d]: { ...prev[d]!, closesAt: e.target.value } }))
                  }
                  className="max-w-[140px]"
                />
              </div>
              <div className="flex items-center gap-3">
                <span className="text-sm text-muted-foreground">{v.open ? "Abierto" : "Cerrado"}</span>
                <Switch
                  checked={v.open}
                  onCheckedChange={(checked) =>
                    setDays((prev) => ({ ...prev, [d]: { ...prev[d]!, open: checked } }))
                  }
                />
              </div>
            </div>
          );
        })}
      </div>
    </StepShell>
  );
}
