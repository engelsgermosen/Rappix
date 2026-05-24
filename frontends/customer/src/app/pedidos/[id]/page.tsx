"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { AlertCircle, ArrowLeft, Share2 } from "lucide-react";
import Link from "next/link";
import { use, useEffect, useState } from "react";
import { toast } from "sonner";

import { RequireAuth } from "@/components/auth/require-auth";
import { OrderStatusBadge } from "@/components/orders/order-status-badge";
import { CourierCard } from "@/components/tracking/courier-card";
import { TrackingMap } from "@/components/tracking/tracking-map-loader";
import { TrackingTimeline } from "@/components/tracking/tracking-timeline";
import { Button } from "@/components/ui/button";
import { Separator } from "@/components/ui/separator";
import { Skeleton } from "@/components/ui/skeleton";
import { ApiError } from "@/lib/api/errors";
import { ordersApi } from "@/lib/api/orders";
import { trackingApi } from "@/lib/api/tracking";
import type { OrderTrackingResponse, TrackingStatus } from "@/lib/api/types";
import { isOrderCancellable, isOrderTerminal, ORDER_STATUS_LABEL } from "@/lib/orders/status";
import { startTracking } from "@/lib/tracking/hub";
import { formatMoney, shortId } from "@/lib/utils";

export default function OrderDetailPage({ params }: { params: Promise<{ id: string }> }) {
  return (
    <RequireAuth fallback={<div className="container py-16 text-center text-muted-foreground">Cargando…</div>}>
      <OrderDetail params={params} />
    </RequireAuth>
  );
}

function OrderDetail({ params }: { params: Promise<{ id: string }> }) {
  const { id } = use(params);
  const qc = useQueryClient();
  const [liveStatus, setLiveStatus] = useState<TrackingStatus | null>(null);
  const [hubError, setHubError] = useState<string | null>(null);
  const [hubConnected, setHubConnected] = useState(false);

  const orderQ = useQuery({
    queryKey: ["orders", "by-id", id],
    queryFn: () => ordersApi.byId(id),
    refetchInterval: (q) => {
      const order = q.state.data;
      if (!order) return 10_000;
      return isOrderTerminal(order.status) ? false : 15_000;
    },
  });

  const trackingQ = useQuery<OrderTrackingResponse>({
    enabled: !!orderQ.data,
    queryKey: ["tracking", "snapshot", id],
    queryFn: () => trackingApi.snapshot(id),
    refetchInterval: (q) => {
      const order = orderQ.data;
      if (!order) return false;
      return isOrderTerminal(order.status) ? false : 20_000;
    },
  });

  // SignalR — apply live pushes by patching the snapshot cache directly.
  useEffect(() => {
    if (!orderQ.data) return;
    if (isOrderTerminal(orderQ.data.status)) return;

    const dispose = startTracking(id, {
      onConnected: () => {
        setHubConnected(true);
        setHubError(null);
      },
      onDisconnected: () => setHubConnected(false),
      onLocation: (p) => {
        qc.setQueryData<OrderTrackingResponse | undefined>(["tracking", "snapshot", id], (prev) => {
          if (!prev) return prev;
          return {
            ...prev,
            courierId: p.courierId,
            lastLocation: { lat: p.latitude, lng: p.longitude, reportedAtUtc: p.reportedAtUtc },
          };
        });
      },
      onStatus: (p) => {
        setLiveStatus(p.status);
        qc.setQueryData<OrderTrackingResponse | undefined>(["tracking", "snapshot", id], (prev) =>
          prev ? { ...prev, status: p.status, statusReason: p.reason } : prev,
        );
        if (p.status === "Delivered" || p.status === "Cancelled" || p.status === "Failed") {
          // Force-refresh the order to pick up `completedAtUtc` / `closedAtUtc`.
          qc.invalidateQueries({ queryKey: ["orders", "by-id", id] });
        }
      },
      onError: (err) => {
        setHubConnected(false);
        setHubError(err.message);
      },
    });
    return dispose;
  }, [id, orderQ.data, qc]);

  const cancelMut = useMutation({
    mutationFn: () => ordersApi.cancel(id, "Cancelado por el cliente"),
    onSuccess: () => {
      toast.success("Solicitud de cancelación enviada");
      qc.invalidateQueries({ queryKey: ["orders", "by-id", id] });
    },
    onError: (err) => toast.error(err instanceof ApiError ? err.display() : "No pudimos cancelar"),
  });

  if (orderQ.isLoading) {
    return (
      <div className="container py-6 md:py-8 space-y-4">
        <Skeleton className="h-8 w-1/3" />
        <Skeleton className="h-[420px] w-full" />
      </div>
    );
  }
  if (orderQ.isError || !orderQ.data) {
    return (
      <div className="container py-16 text-center">
        <div className="mx-auto max-w-md rounded-2xl border border-dashed border-border bg-white p-8 text-muted-foreground">
          <AlertCircle className="h-8 w-8 text-amber-500 mx-auto mb-3" />
          <div className="font-semibold text-foreground">No encontramos este pedido</div>
          <p className="text-sm mt-1">Es posible que aún no esté listo o que no te pertenezca.</p>
          <Button asChild className="mt-4">
            <Link href="/pedidos">Volver a mis pedidos</Link>
          </Button>
        </div>
      </div>
    );
  }

  const order = orderQ.data;
  const eta = orderEta(order);

  return (
    <div className="container py-6 md:py-8 animate-fade-in">
      <Link href="/pedidos" className="inline-flex items-center gap-2 text-sm text-muted-foreground hover:text-foreground">
        <ArrowLeft className="h-4 w-4" /> Volver
      </Link>

      <div className="mt-3 flex flex-wrap items-start justify-between gap-4">
        <div>
          <div className="text-[10px] font-semibold uppercase tracking-widest text-muted-foreground">
            Pedido {shortId(order.orderId, "#RPX-")}
          </div>
          <h1 className="text-2xl md:text-3xl font-bold tracking-tight mt-0.5">
            {isOrderTerminal(order.status)
              ? ORDER_STATUS_LABEL[order.status]
              : `Llega en ~${eta} min`}
          </h1>
          <div className="text-sm text-muted-foreground mt-1.5 flex items-center gap-2 flex-wrap">
            <span>{order.lines.length} producto{order.lines.length === 1 ? "" : "s"} · {order.vertical}</span>
            {!isOrderTerminal(order.status) && (
              <span
                className={`inline-flex items-center gap-1.5 rounded-full px-2 py-0.5 text-[11px] font-medium ${
                  hubConnected
                    ? "bg-emerald-50 text-emerald-700"
                    : "bg-amber-50 text-amber-700"
                }`}
                title={hubConnected ? "WebSocket conectado al hub de tracking" : "Conexión en vivo no disponible — usando polling"}
              >
                <span className={`h-1.5 w-1.5 rounded-full ${hubConnected ? "bg-emerald-500 animate-pulse" : "bg-amber-500"}`} />
                {hubConnected ? "En vivo" : "Polling"}
              </span>
            )}
          </div>
        </div>
        <div className="flex items-center gap-2 flex-wrap">
          <OrderStatusBadge status={order.status} />
          <Button variant="outline" size="sm" className="gap-1.5">
            <Share2 className="h-4 w-4" /> Compartir
          </Button>
          {isOrderCancellable(order.status) && (
            <Button variant="destructive" size="sm" onClick={() => cancelMut.mutate()} disabled={cancelMut.isPending}>
              {cancelMut.isPending ? "Cancelando…" : "Cancelar"}
            </Button>
          )}
        </div>
      </div>

      <div className="mt-6 grid lg:grid-cols-[1fr_380px] gap-6 lg:gap-8">
        <div className="space-y-4">
          {trackingQ.data ? (
            <TrackingMap tracking={trackingQ.data} />
          ) : trackingQ.isLoading ? (
            <Skeleton className="h-[420px] w-full" />
          ) : (
            <div className="rounded-xl border border-dashed border-border bg-white p-8 text-center text-muted-foreground">
              Aún no hay datos de seguimiento. {trackingQ.error instanceof ApiError ? trackingQ.error.display() : ""}
            </div>
          )}
          {trackingQ.data && <CourierCard tracking={trackingQ.data} />}
          {hubError && !hubConnected && (
            <div className="rounded-lg bg-amber-50 text-amber-800 text-xs p-3">
              Conexión en vivo intermitente: {hubError}. Seguimos actualizando con polling.
            </div>
          )}
        </div>

        <aside className="space-y-4 lg:sticky lg:top-24 lg:self-start">
          <section className="rounded-xl border border-border bg-white p-5">
            <h2 className="font-semibold mb-3">Estado del pedido</h2>
            <TrackingTimeline order={order} liveStatus={liveStatus} />
          </section>

          <section className="rounded-xl border border-border bg-white p-5">
            <h2 className="font-semibold">Tu pedido</h2>
            <ul className="mt-3 space-y-2 text-sm">
              {order.lines.map((l) => (
                <li key={l.itemId} className="flex justify-between">
                  <span className="text-foreground/80 truncate pr-2">
                    {l.quantity}× {l.itemName}
                  </span>
                  <span className="font-medium">{formatMoney(l.lineSubtotal, order.currency)}</span>
                </li>
              ))}
            </ul>
            <Separator className="my-3" />
            <ul className="space-y-1.5 text-sm">
              <li className="flex justify-between text-muted-foreground"><span>Subtotal</span><span>{formatMoney(order.subtotal, order.currency)}</span></li>
              <li className="flex justify-between text-muted-foreground"><span>Envío</span><span>{formatMoney(order.deliveryFee, order.currency)}</span></li>
              <li className="flex justify-between text-muted-foreground"><span>Servicio</span><span>{formatMoney(order.serviceFee, order.currency)}</span></li>
              <li className="flex justify-between text-muted-foreground"><span>Impuestos</span><span>{formatMoney(order.tax, order.currency)}</span></li>
              <li className="flex justify-between text-muted-foreground"><span>Propina</span><span>{formatMoney(order.tip, order.currency)}</span></li>
              {order.discountAmount > 0 && (
                <li className="flex justify-between text-emerald-700"><span>Descuento</span><span>-{formatMoney(order.discountAmount, order.currency)}</span></li>
              )}
            </ul>
            <Separator className="my-3" />
            <div className="flex justify-between font-bold text-lg">
              <span>Total pagado</span>
              <span>{formatMoney(order.totalAmount, order.currency)}</span>
            </div>
            <Button variant="outline" className="w-full mt-4" asChild>
              <Link href="/">Volver al inicio</Link>
            </Button>
          </section>
        </aside>
      </div>
    </div>
  );
}

function orderEta(order: { createdAtUtc: string }) {
  // Synthetic ETA: ~30 min from creation, clamped 1–45. The backend doesn't
  // emit a real ETA today (Tracking has no ETA field) — flagged as a backend
  // follow-up in API_MAP.md.
  const created = new Date(order.createdAtUtc).getTime();
  const elapsed = (Date.now() - created) / 60_000;
  const eta = Math.max(1, Math.round(30 - elapsed));
  return Math.min(45, eta);
}
