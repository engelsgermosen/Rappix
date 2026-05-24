"use client";

import { useEffect, useState } from "react";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";

import { Button } from "@/components/ui/button";
import { Card } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Switch } from "@/components/ui/switch";
import { updateOperatingHours } from "@/lib/api/merchants-me";
import { describeError } from "@/lib/errors";
import { DAYS_ORDERED, DAY_LABEL } from "@/lib/utils";
import type { DayOfWeek, MerchantResponse, OperatingHoursRangeRequest } from "@/lib/api/types";

type DayState = { open: boolean; opensAt: string; closesAt: string };

export function HorariosCard({ merchant }: { merchant: MerchantResponse }) {
  const queryClient = useQueryClient();

  const [days, setDays] = useState<Record<DayOfWeek, DayState>>(() => buildState(merchant));

  useEffect(() => {
    setDays(buildState(merchant));
  }, [merchant]);

  const save = useMutation({
    mutationFn: (hours: OperatingHoursRangeRequest[]) => updateOperatingHours({ hours }),
    onSuccess: (data) => {
      queryClient.setQueryData(["merchant", "me"], data);
      toast.success("Horarios actualizados.");
    },
    onError: (err) => toast.error(describeError(err)),
  });

  function onSave() {
    const ranges: OperatingHoursRangeRequest[] = [];
    for (const d of DAYS_ORDERED) {
      const v = days[d]!;
      if (!v.open) continue;
      if (v.opensAt >= v.closesAt) {
        toast.error(`En ${DAY_LABEL[d]}, la hora de cierre debe ser posterior a la apertura.`);
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
    <Card className="overflow-hidden">
      <div className="border-b border-border px-6 py-4">
        <h2 className="text-lg font-bold text-foreground">Horarios</h2>
        <p className="text-xs text-muted-foreground">Cuándo está abierto tu comercio.</p>
      </div>
      <div className="space-y-2.5 px-6 py-5">
        {DAYS_ORDERED.map((d) => {
          const v = days[d]!;
          return (
            <div
              key={d}
              className="flex flex-wrap items-center gap-3 rounded-lg border border-border bg-white p-3"
            >
              <Label className="w-20 text-sm font-semibold">{DAY_LABEL[d]}</Label>
              <Input
                type="time"
                value={v.opensAt}
                disabled={!v.open}
                onChange={(e) =>
                  setDays((p) => ({ ...p, [d]: { ...p[d]!, opensAt: e.target.value } }))
                }
                className="max-w-[120px]"
              />
              <Input
                type="time"
                value={v.closesAt}
                disabled={!v.open}
                onChange={(e) =>
                  setDays((p) => ({ ...p, [d]: { ...p[d]!, closesAt: e.target.value } }))
                }
                className="max-w-[120px]"
              />
              <div className="ml-auto flex items-center gap-2">
                <span className="text-xs text-muted-foreground">{v.open ? "Abierto" : "Cerrado"}</span>
                <Switch
                  checked={v.open}
                  onCheckedChange={(checked) =>
                    setDays((p) => ({ ...p, [d]: { ...p[d]!, open: checked } }))
                  }
                />
              </div>
            </div>
          );
        })}
        <div className="flex items-center justify-end gap-2 pt-2">
          <Button type="button" onClick={onSave} disabled={save.isPending}>
            {save.isPending ? "Guardando..." : "Guardar horarios"}
          </Button>
        </div>
      </div>
    </Card>
  );
}

function buildState(m: MerchantResponse): Record<DayOfWeek, DayState> {
  const next: Record<string, DayState> = {};
  for (const d of DAYS_ORDERED) {
    next[d] = { open: false, opensAt: "11:00", closesAt: "22:00" };
  }
  for (const row of m.operatingHours) {
    next[row.dayOfWeek] = { open: true, opensAt: row.opensAt, closesAt: row.closesAt };
  }
  return next as Record<DayOfWeek, DayState>;
}
