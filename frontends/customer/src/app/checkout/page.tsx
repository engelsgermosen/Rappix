"use client";

import { useMutation, useQuery } from "@tanstack/react-query";
import { ArrowLeft, ShieldCheck } from "lucide-react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useEffect, useMemo, useState } from "react";
import { toast } from "sonner";

import { RequireAuth } from "@/components/auth/require-auth";
import { AddressCard, AddressMapPlaceholder } from "@/components/checkout/address-card";
import { PaymentPicker, type PaymentMethodId } from "@/components/checkout/payment-picker";
import { QuoteCountdown } from "@/components/checkout/quote-countdown";
import { TipPicker } from "@/components/checkout/tip-picker";
import { Button } from "@/components/ui/button";
import { Separator } from "@/components/ui/separator";
import { Skeleton } from "@/components/ui/skeleton";
import { ApiError } from "@/lib/api/errors";
import { ordersApi } from "@/lib/api/orders";
import { pricingApi } from "@/lib/api/pricing";
import { useCheckoutStore } from "@/lib/cart/checkout-store";
import { useCartStore } from "@/lib/cart/store";
import { env } from "@/lib/env";
import { formatMoney, newIdempotencyKey } from "@/lib/utils";

export default function CheckoutPage() {
  return (
    <RequireAuth fallback={<div className="container py-16 text-center text-muted-foreground">Cargando…</div>}>
      <CheckoutContent />
    </RequireAuth>
  );
}

function CheckoutContent() {
  const router = useRouter();
  const lines = useCartStore((s) => s.lines);
  const merchantId = useCartStore((s) => s.merchantId);
  const merchantName = useCartStore((s) => s.merchantName);
  const merchantVertical = useCartStore((s) => s.merchantVertical);
  const merchantSlug = useCartStore((s) => s.merchantSlug);
  const cartClear = useCartStore((s) => s.clear);

  const setQuote = useCheckoutStore((s) => s.setQuote);
  const quote = useCheckoutStore((s) => s.quote);
  const tip = useCheckoutStore((s) => s.tip);
  const setTip = useCheckoutStore((s) => s.setTip);
  const address = useCheckoutStore((s) => s.address);

  const [paymentMethod, setPaymentMethod] = useState<PaymentMethodId>("card-visa");
  const [idempotencyKey, setIdempotencyKey] = useState<string>(() => newIdempotencyKey());

  const canQuote = !!merchantId && !!merchantVertical && lines.length > 0;

  // Re-quote when tip or cart changes (TanStack handles dedup + retries).
  const quoteQ = useQuery({
    enabled: canQuote,
    queryKey: ["pricing", "quote", merchantId, tip, lines.map((l) => `${l.itemId}:${l.quantity}:${l.modifierTotal}`).join(",")],
    queryFn: () =>
      pricingApi.createQuote({
        merchantId: merchantId!,
        vertical: merchantVertical!,
        distanceKm: env.defaultDistanceKm,
        tip,
        isFirstOrder: true,
        lines: lines.map((l) => ({ itemId: l.itemId, quantity: l.quantity, modifierTotal: l.modifierTotal })),
      }),
    staleTime: 30_000,
    retry: false,
  });

  useEffect(() => {
    if (quoteQ.data) setQuote(quoteQ.data);
  }, [quoteQ.data, setQuote]);

  const placeOrder = useMutation({
    mutationFn: () => {
      const active = quoteQ.data ?? quote;
      if (!active) throw new Error("No hay cotización activa");
      return ordersApi.place(
        {
          quoteId: active.quoteId,
          street: address.street,
          reference: address.reference,
          latitude: address.latitude,
          longitude: address.longitude,
        },
        idempotencyKey,
      );
    },
    onSuccess: (order) => {
      toast.success("Pedido confirmado");
      cartClear();
      useCheckoutStore.getState().reset();
      router.push(`/pedidos/${order.orderId}`);
    },
    onError: (err) => {
      if (err instanceof ApiError) {
        if (err.code === "Orders.Order.QuoteNotUsable" || err.code === "Pricing.Quote.Expired") {
          toast.error("La cotización expiró. Estamos cotizando de nuevo.");
          quoteQ.refetch();
          setIdempotencyKey(newIdempotencyKey());
          return;
        }
        if (err.status === 409 && err.problem?.title === "Operacion duplicada") {
          toast.error("Detectamos un duplicado. Genera un nuevo intento.");
          setIdempotencyKey(newIdempotencyKey());
          return;
        }
        toast.error(err.display());
      } else {
        toast.error("No pudimos confirmar el pedido");
      }
    },
  });

  // Empty cart guard (post-clear).
  const empty = lines.length === 0;

  const totals = useMemo(() => {
    if (!quoteQ.data) return null;
    const b = quoteQ.data.breakdown;
    return [
      { label: "Subtotal", value: b.subtotal },
      ...(b.surgeAmount > 0 ? [{ label: `Surge ×${b.surgeMultiplier.toFixed(2)}`, value: b.surgeAmount }] : []),
      { label: "Envío", value: b.deliveryFee },
      { label: "Servicio", value: b.serviceFee },
      { label: "Impuestos", value: b.tax },
      { label: "Propina", value: b.tip },
      ...(b.discountAmount > 0 ? [{ label: "Descuento bienvenida", value: -b.discountAmount, negative: true }] : []),
    ];
  }, [quoteQ.data]);

  if (empty) {
    return (
      <div className="container py-16 text-center">
        <div className="mx-auto max-w-md rounded-2xl border border-border bg-white p-8">
          <div className="font-bold text-xl">Tu carrito está vacío</div>
          <p className="text-muted-foreground mt-1 text-sm">Agrega productos antes de continuar al checkout.</p>
          <Button asChild className="mt-4">
            <Link href="/">Ver comercios</Link>
          </Button>
        </div>
      </div>
    );
  }

  return (
    <div className="container py-6 md:py-8 animate-fade-in">
      <Link
        href={merchantSlug ? `/comercios/${merchantSlug}` : "/carrito"}
        className="inline-flex items-center gap-2 text-sm text-muted-foreground hover:text-foreground"
      >
        <ArrowLeft className="h-4 w-4" /> Volver al comercio
      </Link>
      <h1 className="mt-2 text-2xl md:text-3xl font-bold tracking-tight">Confirma tu pedido</h1>

      <div className="mt-6 grid lg:grid-cols-[1fr_360px] gap-6">
        <div className="space-y-5">
          {/* Address */}
          <section className="rounded-xl border border-border bg-white p-5">
            <div className="flex items-center justify-between mb-3">
              <h2 className="font-semibold">Entrega a domicilio</h2>
              <button className="text-sm text-brand font-medium hover:underline">Editar</button>
            </div>
            <AddressCard address={address} />
            <AddressMapPlaceholder />
          </section>

          {/* Payment */}
          <section className="rounded-xl border border-border bg-white p-5">
            <div className="flex items-center justify-between mb-3">
              <h2 className="font-semibold">Método de pago</h2>
              <button className="text-sm text-brand font-medium hover:underline">Cambiar</button>
            </div>
            <PaymentPicker value={paymentMethod} onChange={setPaymentMethod} />
            <div className="mt-3 inline-flex items-center gap-1.5 text-xs text-muted-foreground">
              <ShieldCheck className="h-3.5 w-3.5" />
              En Fase 8 el cobro lo dispara la saga (gateway Fake). Stripe Elements queda como follow-up.
            </div>
          </section>

          {/* Order summary */}
          <section className="rounded-xl border border-border bg-white p-5">
            <div className="flex items-start justify-between mb-3">
              <h2 className="font-semibold">Tu pedido</h2>
              <div className="text-sm text-muted-foreground">{merchantName}</div>
            </div>
            <div className="divide-y divide-border">
              {lines.map((line) => (
                <div key={line.itemId} className="py-3 flex items-center gap-3">
                  <span className="inline-flex h-7 min-w-7 items-center justify-center rounded-md bg-brand-100 text-brand-700 px-1.5 text-xs font-bold">
                    {line.quantity}×
                  </span>
                  <div className="flex-1 min-w-0">
                    <div className="font-medium truncate">{line.itemName}</div>
                    <div className="text-xs text-muted-foreground">
                      {formatMoney(line.unitPrice + line.modifierTotal, line.currency)} c/u
                    </div>
                  </div>
                  <div className="font-bold">
                    {formatMoney((line.unitPrice + line.modifierTotal) * line.quantity, line.currency)}
                  </div>
                </div>
              ))}
            </div>
          </section>

          {/* Tip */}
          <section className="rounded-xl border border-border bg-white p-5">
            <div className="font-semibold">Propina para el repartidor</div>
            <p className="text-sm text-muted-foreground mb-3">El 100% va al repartidor.</p>
            <TipPicker value={tip} onChange={setTip} />
          </section>
        </div>

        {/* Right column — summary */}
        <aside className="lg:sticky lg:top-24 lg:self-start">
          <div className="rounded-xl border border-border bg-white p-5">
            <h2 className="font-semibold mb-3">Resumen</h2>
            {quoteQ.isLoading ? (
              <div className="space-y-2">
                <Skeleton className="h-4 w-full" />
                <Skeleton className="h-4 w-full" />
                <Skeleton className="h-4 w-full" />
                <Skeleton className="h-10 w-full mt-3" />
              </div>
            ) : quoteQ.isError ? (
              <div className="text-sm text-red-600">
                {quoteQ.error instanceof ApiError ? quoteQ.error.display() : "No pudimos cotizar este pedido."}
                <Button variant="outline" size="sm" className="mt-3 w-full" onClick={() => quoteQ.refetch()}>
                  Reintentar
                </Button>
              </div>
            ) : (
              quoteQ.data && (
                <>
                  <ul className="space-y-1.5 text-sm">
                    {totals!.map((t) => (
                      <li key={t.label} className="flex justify-between">
                        <span className={t.negative ? "text-emerald-700" : "text-muted-foreground"}>{t.label}</span>
                        <span className={t.negative ? "text-emerald-700 font-medium" : "text-foreground/90"}>
                          {t.negative ? "-" : ""}{formatMoney(Math.abs(t.value), quoteQ.data.currency)}
                        </span>
                      </li>
                    ))}
                  </ul>
                  <Separator className="my-3" />
                  <div className="flex justify-between items-baseline">
                    <span className="font-semibold">Total</span>
                    <span className="text-xl font-bold text-brand">
                      {formatMoney(quoteQ.data.breakdown.total, quoteQ.data.currency)}
                    </span>
                  </div>
                  <div className="mt-2">
                    <QuoteCountdown
                      expiresAtUtc={quoteQ.data.expiresAtUtc}
                      onExpire={() => quoteQ.refetch()}
                    />
                  </div>
                  <Button
                    size="lg"
                    className="w-full mt-4"
                    onClick={() => placeOrder.mutate()}
                    disabled={placeOrder.isPending || quoteQ.isFetching}
                  >
                    {placeOrder.isPending ? "Confirmando…" : "Pagar y pedir"}
                  </Button>
                  <p className="text-xs text-muted-foreground mt-3 leading-relaxed">
                    Al pagar aceptas los <a href="#" className="underline hover:text-brand">Términos</a> y la{" "}
                    <a href="#" className="underline hover:text-brand">Política de privacidad</a> de Rappix.
                  </p>
                </>
              )
            )}
          </div>
        </aside>
      </div>
    </div>
  );
}
