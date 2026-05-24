"use client";

import { ArrowLeft, Minus, Plus, ShoppingBag, Trash2 } from "lucide-react";
import Link from "next/link";
import { useRouter } from "next/navigation";

import { Button } from "@/components/ui/button";
import { Separator } from "@/components/ui/separator";
import { useCartStore } from "@/lib/cart/store";
import { formatMoney } from "@/lib/utils";

export default function CartPage() {
  const router = useRouter();
  const lines = useCartStore((s) => s.lines);
  const merchantId = useCartStore((s) => s.merchantId);
  const merchantName = useCartStore((s) => s.merchantName);
  const merchantSlug = useCartStore((s) => s.merchantSlug);
  const setQuantity = useCartStore((s) => s.setQuantity);
  const remove = useCartStore((s) => s.remove);
  const clear = useCartStore((s) => s.clear);
  const subtotal = useCartStore((s) => s.subtotal());
  const currency = lines[0]?.currency ?? "DOP";

  if (lines.length === 0 || !merchantId) {
    return (
      <div className="container py-16 max-w-xl">
        <div className="rounded-2xl border border-border bg-white p-10 text-center">
          <div className="mx-auto h-14 w-14 rounded-full bg-brand-100 inline-flex items-center justify-center mb-4">
            <ShoppingBag className="h-6 w-6 text-brand-700" />
          </div>
          <h1 className="font-bold text-xl">Tu carrito está vacío</h1>
          <p className="text-sm text-muted-foreground mt-1">Empieza a explorar comercios cerca de ti.</p>
          <Button className="mt-6" asChild>
            <Link href="/">Ver comercios</Link>
          </Button>
        </div>
      </div>
    );
  }

  return (
    <div className="container py-6 md:py-8 max-w-3xl animate-fade-in">
      <Link
        href={merchantSlug ? `/comercios/${merchantSlug}` : "/"}
        className="inline-flex items-center gap-2 text-sm text-muted-foreground hover:text-foreground transition-colors"
      >
        <ArrowLeft className="h-4 w-4" /> Seguir comprando
      </Link>

      <div className="mt-2 flex items-end justify-between">
        <h1 className="text-2xl md:text-3xl font-bold tracking-tight">Tu carrito</h1>
        <button onClick={clear} className="text-sm text-red-600 hover:underline inline-flex items-center gap-1">
          <Trash2 className="h-4 w-4" /> Vaciar
        </button>
      </div>
      <p className="text-sm text-muted-foreground mt-1">
        Productos de <span className="font-medium text-foreground">{merchantName}</span>
      </p>

      <div className="mt-6 rounded-xl border border-border bg-white divide-y divide-border">
        {lines.map((line) => {
          const lineSubtotal = (line.unitPrice + line.modifierTotal) * line.quantity;
          return (
            <div key={line.itemId} className="p-4 md:p-5 flex items-center gap-4">
              <div className="h-14 w-14 rounded-lg bg-brand-100/60 text-brand-700/70 text-[10px] font-semibold inline-flex items-center justify-center uppercase tracking-widest flex-shrink-0">
                Foto
              </div>
              <div className="flex-1 min-w-0">
                <div className="font-semibold truncate">{line.itemName}</div>
                <div className="text-sm text-muted-foreground">
                  {formatMoney(line.unitPrice + line.modifierTotal, line.currency)} c/u
                </div>
              </div>
              <div className="inline-flex items-center gap-2 rounded-full border border-border bg-white px-1">
                <button
                  className="h-8 w-8 inline-flex items-center justify-center rounded-full hover:bg-muted disabled:opacity-30"
                  onClick={() => setQuantity(line.itemId, line.quantity - 1)}
                  aria-label="Restar"
                  disabled={line.quantity <= 1}
                >
                  <Minus className="h-4 w-4" />
                </button>
                <span className="font-semibold min-w-[1.5rem] text-center">{line.quantity}</span>
                <button
                  className="h-8 w-8 inline-flex items-center justify-center rounded-full hover:bg-muted"
                  onClick={() => setQuantity(line.itemId, line.quantity + 1)}
                  aria-label="Sumar"
                >
                  <Plus className="h-4 w-4" />
                </button>
              </div>
              <div className="hidden sm:block w-24 text-right font-bold">
                {formatMoney(lineSubtotal, line.currency)}
              </div>
              <button
                onClick={() => remove(line.itemId)}
                className="text-muted-foreground hover:text-red-600 transition-colors"
                aria-label={`Quitar ${line.itemName}`}
              >
                <Trash2 className="h-4 w-4" />
              </button>
            </div>
          );
        })}
      </div>

      <div className="mt-5 rounded-xl border border-border bg-white p-5 space-y-2">
        <div className="flex justify-between text-sm text-muted-foreground">
          <span>Subtotal</span>
          <span>{formatMoney(subtotal, currency)}</span>
        </div>
        <div className="flex justify-between text-sm text-muted-foreground">
          <span>Envío + servicio + impuestos</span>
          <span className="italic">Se calculan al cotizar</span>
        </div>
        <Separator className="my-2" />
        <div className="flex justify-between font-bold">
          <span>Total estimado</span>
          <span>{formatMoney(subtotal, currency)}</span>
        </div>
        <Button
          size="lg"
          variant="accent"
          className="w-full mt-3"
          onClick={() => router.push("/checkout")}
        >
          Continuar al checkout
        </Button>
        <p className="text-xs text-muted-foreground text-center">
          Generaremos una cotización oficial en el siguiente paso (válida por 10 min).
        </p>
      </div>
    </div>
  );
}
