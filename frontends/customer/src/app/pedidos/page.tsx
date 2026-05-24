"use client";

import { useQuery } from "@tanstack/react-query";
import { ChevronRight, Package } from "lucide-react";
import Link from "next/link";

import { RequireAuth } from "@/components/auth/require-auth";
import { OrderStatusBadge } from "@/components/orders/order-status-badge";
import { Button } from "@/components/ui/button";
import { Skeleton } from "@/components/ui/skeleton";
import { ordersApi } from "@/lib/api/orders";
import { formatMoney, shortId } from "@/lib/utils";

export default function OrdersPage() {
  return (
    <RequireAuth fallback={<div className="container py-16 text-center text-muted-foreground">Cargando…</div>}>
      <OrdersList />
    </RequireAuth>
  );
}

function OrdersList() {
  const q = useQuery({
    queryKey: ["orders", "list"],
    queryFn: () => ordersApi.list({ pageSize: 50 }),
    refetchInterval: 30_000,
  });

  if (q.isLoading) {
    return (
      <div className="container py-6 md:py-8 max-w-3xl space-y-3">
        <Skeleton className="h-8 w-1/3" />
        <Skeleton className="h-24 w-full" />
        <Skeleton className="h-24 w-full" />
        <Skeleton className="h-24 w-full" />
      </div>
    );
  }

  const orders = q.data ?? [];

  return (
    <div className="container py-6 md:py-8 max-w-3xl animate-fade-in">
      <h1 className="text-2xl md:text-3xl font-bold tracking-tight">Mis pedidos</h1>
      <p className="text-sm text-muted-foreground mt-1">Sigue el estado de tus pedidos en tiempo real.</p>

      {orders.length === 0 ? (
        <div className="mt-8 rounded-2xl border border-border bg-white p-10 text-center">
          <div className="mx-auto h-14 w-14 rounded-full bg-brand-100 inline-flex items-center justify-center mb-4">
            <Package className="h-6 w-6 text-brand-700" />
          </div>
          <h2 className="font-bold">Aún no tienes pedidos</h2>
          <p className="text-sm text-muted-foreground mt-1">Cuando hagas tu primer pedido lo verás aquí.</p>
          <Button asChild className="mt-4">
            <Link href="/">Explorar comercios</Link>
          </Button>
        </div>
      ) : (
        <ul className="mt-6 space-y-3">
          {orders.map((o) => (
            <li key={o.orderId}>
              <Link
                href={`/pedidos/${o.orderId}`}
                className="block rounded-xl border border-border bg-white p-4 md:p-5 hover:border-brand/30 hover:shadow-sm transition-all"
              >
                <div className="flex items-center justify-between">
                  <div>
                    <div className="text-[10px] font-semibold uppercase tracking-widest text-muted-foreground">
                      Pedido {shortId(o.orderId, "#RPX-")}
                    </div>
                    <div className="font-semibold mt-0.5">
                      {o.lines.length} producto{o.lines.length === 1 ? "" : "s"} · {formatMoney(o.totalAmount, o.currency)}
                    </div>
                    <div className="text-xs text-muted-foreground mt-0.5">
                      {new Date(o.createdAtUtc).toLocaleString("es-DO", {
                        day: "2-digit",
                        month: "short",
                        hour: "2-digit",
                        minute: "2-digit",
                      })}
                    </div>
                  </div>
                  <div className="flex items-center gap-3">
                    <OrderStatusBadge status={o.status} />
                    <ChevronRight className="h-5 w-5 text-muted-foreground" />
                  </div>
                </div>
              </Link>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
