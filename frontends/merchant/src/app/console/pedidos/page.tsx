"use client";

import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";
import { ShieldCheck } from "lucide-react";

import { OrderCard } from "@/components/pedidos/order-card";
import { PedidosEmptyState } from "@/components/pedidos/empty-state";
import { RefreshBanner } from "@/components/pedidos/refresh-banner";
import { Badge } from "@/components/ui/badge";
import { Card } from "@/components/ui/card";
import { getMyMerchant } from "@/lib/api/merchants-me";
import { acceptOrder, listPending, rejectOrder } from "@/lib/api/merchant-orders";
import { describeError } from "@/lib/errors";
import type { OrderResponse } from "@/lib/api/types";

const POLL_MS = 10_000;

export default function PedidosPage() {
  const queryClient = useQueryClient();
  const merchant = useQuery({ queryKey: ["merchant", "me"], queryFn: getMyMerchant });

  const pending = useQuery({
    queryKey: ["orders", "merchant-pending"],
    queryFn: () => listPending({ pageSize: 50 }),
    refetchInterval: POLL_MS,
    refetchOnWindowFocus: true,
  });

  // Tracks which order is being mutated so we can disable its buttons.
  const [busyOrderId, setBusyOrderId] = useState<string | null>(null);

  function applyOptimisticRemove(orderId: string) {
    queryClient.setQueryData<OrderResponse[]>(["orders", "merchant-pending"], (prev) =>
      (prev ?? []).filter((o) => o.orderId !== orderId),
    );
  }

  const accept = useMutation({
    mutationFn: (orderId: string) => acceptOrder(orderId),
    onMutate: (orderId) => {
      setBusyOrderId(orderId);
      const snapshot = queryClient.getQueryData<OrderResponse[]>(["orders", "merchant-pending"]) ?? [];
      applyOptimisticRemove(orderId);
      return { snapshot };
    },
    onError: (err, _orderId, ctx) => {
      if (ctx) queryClient.setQueryData(["orders", "merchant-pending"], ctx.snapshot);
      toast.error(describeError(err));
    },
    onSuccess: () => {
      toast.success("Pedido aceptado. Rappix se encarga del resto.");
    },
    onSettled: () => {
      setBusyOrderId(null);
      queryClient.invalidateQueries({ queryKey: ["orders", "merchant-pending"] });
    },
  });

  const reject = useMutation({
    mutationFn: ({ orderId, reason }: { orderId: string; reason: string }) =>
      rejectOrder(orderId, { reason: reason.length > 0 ? reason : null }),
    onMutate: ({ orderId }) => {
      setBusyOrderId(orderId);
      const snapshot = queryClient.getQueryData<OrderResponse[]>(["orders", "merchant-pending"]) ?? [];
      applyOptimisticRemove(orderId);
      return { snapshot };
    },
    onError: (err, _vars, ctx) => {
      if (ctx) queryClient.setQueryData(["orders", "merchant-pending"], ctx.snapshot);
      toast.error(describeError(err));
    },
    onSuccess: () => {
      toast.success("Pedido rechazado.");
    },
    onSettled: () => {
      setBusyOrderId(null);
      queryClient.invalidateQueries({ queryKey: ["orders", "merchant-pending"] });
    },
  });

  const orders = pending.data ?? [];
  const pendingCount = orders.length;
  const isActive = merchant.data?.status === "Active";

  return (
    <section className="px-8 py-8">
      <header className="mb-6 flex flex-wrap items-end justify-between gap-3">
        <div>
          <h1 className="text-3xl font-bold text-foreground">Pedidos entrantes</h1>
          <p className="mt-1 text-sm text-muted-foreground">Acepta o rechaza pedidos en tiempo real.</p>
        </div>
        <Badge variant={isActive ? "active" : "paused"}>
          <span
            className={
              isActive
                ? "h-1.5 w-1.5 rounded-full bg-success"
                : "h-1.5 w-1.5 rounded-full bg-amber-500"
            }
          />
          {isActive ? "Comercio activo" : "Comercio pausado"}
        </Badge>
      </header>

      {/* Real KPI from the polling query — see plan §3 for why the other three were dropped. */}
      <div className="mb-6 grid gap-4 md:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4">
        <Card className="p-5">
          <p className="text-xs font-semibold uppercase tracking-wider text-muted-foreground">
            Pendientes ahora
          </p>
          <p className="mt-2 text-3xl font-bold text-foreground">{pendingCount}</p>
          <p className="mt-1 text-xs text-muted-foreground">Esperando tu acción</p>
        </Card>
      </div>

      <div className="mb-4">
        <RefreshBanner
          intervalMs={POLL_MS}
          dataUpdatedAt={pending.dataUpdatedAt}
          onRefresh={() => pending.refetch()}
          refreshing={pending.isFetching}
        />
      </div>

      {pending.isPending ? (
        <div className="grid gap-4 lg:grid-cols-2 xl:grid-cols-3">
          {[1, 2, 3].map((i) => (
            <Card key={i} className="h-72 animate-pulse" />
          ))}
        </div>
      ) : orders.length === 0 ? (
        <PedidosEmptyState />
      ) : (
        <div className="grid gap-4 lg:grid-cols-2 xl:grid-cols-3">
          {orders.map((order) => (
            <OrderCard
              key={order.orderId}
              order={order}
              busy={busyOrderId === order.orderId}
              onAccept={() => accept.mutate(order.orderId)}
              onReject={(reason) => reject.mutate({ orderId: order.orderId, reason })}
            />
          ))}
        </div>
      )}

      <Card className="mt-6 border-brand-100 bg-brand-50/40 px-4 py-3 text-sm">
        <p className="flex items-start gap-2 text-foreground/80">
          <ShieldCheck className="mt-0.5 h-4 w-4 shrink-0 text-brand" />
          <span>
            Al <span className="font-semibold text-brand">aceptar</span> un pedido, Rappix gestiona automáticamente
            el cobro, asigna un repartidor y se encarga de la entrega.{" "}
            <span className="font-semibold">Tú solo prepara el pedido.</span>
          </span>
        </p>
      </Card>
    </section>
  );
}
