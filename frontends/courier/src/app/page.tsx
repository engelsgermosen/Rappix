"use client";

import { AlertCircle, RefreshCw } from "lucide-react";

import { AppHeader } from "@/components/courier/app-header";
import { OnlineToggle } from "@/components/courier/online-toggle";
import { WaitingCard } from "@/components/courier/waiting-card";
import { RequireAuth } from "@/components/auth/require-auth";
import { Button } from "@/components/ui/button";
import { Skeleton } from "@/components/ui/skeleton";
import { useCourier } from "@/lib/courier/use-courier";
import { useCurrentAssignment } from "@/lib/courier/use-current-assignment";

/**
 * Home autenticada — status-aware. Switch ramas segun `courier.status`:
 *
 *  - Loading / 404 transitorio (carrera con UserRegisteredConsumer de Dispatch):
 *    skeleton + retry visible.
 *  - Offline: OnlineToggle (con sub-ramas: sin vehiculo / con vehiculo).
 *  - Online sin asignacion: OnlineToggle ("Desconectarme") + WaitingCard.
 *  - Online con asignacion (200 del poll): placeholder "pedido activo" — el
 *    contenido completo (mapa + panel) llega en el commit 6.
 *  - Busy: idem, pero el poll es mas agresivo (3s).
 *
 * El polling de `useCurrentAssignment` esta gated por status (no corre si
 * Offline). El heartbeat de location se monta en el commit 7.
 */
function HomeContent() {
  const courier = useCourier();
  const assignment = useCurrentAssignment(courier.data?.status);

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
        ) : (
          <ActiveDeliveryPlaceholder
            orderId={assignment.data?.orderId}
            merchantName={assignment.data?.pickup.merchantName}
          />
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

function ActiveDeliveryPlaceholder({
  orderId,
  merchantName,
}: {
  orderId: string | undefined;
  merchantName: string | undefined;
}) {
  // Contenido real (mapa + panel + acciones "Recogi"/"Entregue") llega en el
  // commit 6. Aqui confirmamos que el poll funciona y llegamos al Busy branch
  // con datos del snapshot enriquecido de Fase 13.6.
  return (
    <div className="mx-auto w-full max-w-[600px] rounded-2xl border border-brand-200 bg-brand-50 p-6 text-center">
      <p className="text-xs font-semibold uppercase tracking-wide text-brand-700">Pedido activo</p>
      <p className="mt-2 text-lg font-bold text-foreground">
        {merchantName ? `Recoger en ${merchantName}` : "Asignacion en curso"}
      </p>
      {orderId ? (
        <p className="mt-1 text-xs text-muted-foreground">orderId: {orderId}</p>
      ) : null}
      <p className="mt-4 text-sm text-muted-foreground">
        La pantalla completa de entrega (mapa + direcciones + acciones) se conecta en el siguiente commit.
      </p>
    </div>
  );
}

export default function HomePage() {
  return (
    <RequireAuth>
      <HomeContent />
    </RequireAuth>
  );
}
