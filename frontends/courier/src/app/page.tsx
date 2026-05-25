"use client";

import { AlertCircle, RefreshCw } from "lucide-react";

import { ActiveDelivery } from "@/components/courier/active-delivery";
import { AppHeader } from "@/components/courier/app-header";
import { OnlineToggle } from "@/components/courier/online-toggle";
import { WaitingCard } from "@/components/courier/waiting-card";
import { RequireAuth } from "@/components/auth/require-auth";
import { Button } from "@/components/ui/button";
import { Skeleton } from "@/components/ui/skeleton";
import { useCourier } from "@/lib/courier/use-courier";
import { useCurrentAssignment } from "@/lib/courier/use-current-assignment";
import { useLocationHeartbeat } from "@/lib/courier/use-location-heartbeat";

/**
 * Home autenticada — status-aware. Switch ramas segun `courier.status`:
 *
 *  - Loading / 404 transitorio (carrera con UserRegisteredConsumer de Dispatch):
 *    skeleton + retry visible.
 *  - Offline: OnlineToggle (con sub-ramas: sin vehiculo / con vehiculo).
 *  - Online sin asignacion: OnlineToggle ("Desconectarme") + WaitingCard.
 *  - Online con asignacion (200 del poll): ActiveDelivery (mapa + panel).
 *  - Busy: idem, pero el poll es mas agresivo (3s).
 *
 * Dos heartbeats:
 *   - useCurrentAssignment: polling adaptativo del endpoint (3s Busy / 5s Online).
 *   - useLocationHeartbeat: navigator.geolocation.watchPosition + throttle
 *     adaptativo (5s Busy / 12s Online). Ambos solo corren con status valido.
 */
function HomeContent() {
  const courier = useCourier();
  const assignment = useCurrentAssignment(courier.data?.status);
  useLocationHeartbeat({ status: courier.data?.status });

  if (courier.isLoading || !courier.data) {
    return <HomeLoading error={courier.error} onRetry={() => courier.refetch()} />;
  }

  const status = courier.data.status;
  const hasAssignment = !!assignment.data;

  return (
    <>
      <AppHeader status={status} />
      <main className="flex-1 px-4 py-8 md:px-6">
        {status === "Offline" ? (
          <OnlineToggle courier={courier.data} />
        ) : status === "Online" && !hasAssignment ? (
          <div className="space-y-6">
            <OnlineToggle courier={courier.data} />
            <WaitingCard />
          </div>
        ) : assignment.data ? (
          <ActiveDelivery assignment={assignment.data} />
        ) : (
          // Busy en el courier pero el poll todavia no devolvio el snapshot
          // (carrera muy corta entre status flip y proxima vuelta). Spinner.
          <WaitingCard />
        )}
      </main>
    </>
  );
}

function HomeLoading({ error, onRetry }: { error: unknown; onRetry: () => void }) {
  return (
    <>
      <AppHeader />
      <main className="flex-1 px-4 py-8 md:px-6">
        <div className="mx-auto w-full max-w-[600px] space-y-3">
          <Skeleton className="h-32 w-full" />
          <Skeleton className="h-12 w-full" />
        </div>
        {error ? (
          <div className="mx-auto mt-6 flex w-full max-w-[600px] items-start gap-3 rounded-lg border border-amber-200 bg-amber-50 p-4 text-sm text-amber-900">
            <AlertCircle className="mt-0.5 h-5 w-5 shrink-0 text-amber-600" />
            <div className="flex-1 space-y-1">
              <p className="font-semibold">No pudimos cargar tu perfil</p>
              <p>Esto puede ocurrir justo despues de registrarte. Reintenta en unos segundos.</p>
            </div>
            <Button type="button" variant="outline" size="sm" onClick={onRetry}>
              <RefreshCw className="mr-1.5 h-4 w-4" /> Reintentar
            </Button>
          </div>
        ) : null}
      </main>
    </>
  );
}

export default function HomePage() {
  return (
    <RequireAuth>
      <HomeContent />
    </RequireAuth>
  );
}
