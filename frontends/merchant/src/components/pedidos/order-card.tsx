"use client";

import { useState } from "react";
import { Check, MapPin, X } from "lucide-react";

import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card } from "@/components/ui/card";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Textarea } from "@/components/ui/textarea";
import { formatMoney, relativeTime, shortId } from "@/lib/utils";
import type { OrderResponse } from "@/lib/api/types";

export function OrderCard({
  order,
  onAccept,
  onReject,
  busy,
}: {
  order: OrderResponse;
  onAccept: () => void;
  onReject: (reason: string) => void;
  busy: boolean;
}) {
  const [rejectOpen, setRejectOpen] = useState(false);
  const [reason, setReason] = useState("");

  return (
    <Card className="flex flex-col overflow-hidden">
      <div className="flex items-center justify-between gap-2 border-b border-border px-4 py-3">
        <div className="flex items-center gap-2 text-xs text-muted-foreground">
          <span className="font-mono font-semibold text-foreground">
            #{shortId(order.orderId, "RPX-")}
          </span>
          <span>·</span>
          <span>{relativeTime(order.createdAtUtc)}</span>
        </div>
        <Badge variant="pending">
          <span className="h-1.5 w-1.5 rounded-full bg-amber-500" />
          Esperando
        </Badge>
      </div>

      <div className="space-y-4 px-4 py-4">
        <div className="flex items-start gap-3">
          <div className="flex h-9 w-9 shrink-0 items-center justify-center rounded-lg bg-brand-50 text-brand">
            <MapPin className="h-4 w-4" />
          </div>
          <div className="min-w-0 flex-1 text-sm">
            <p className="font-semibold text-foreground">
              Cliente #{shortId(order.customerUserId)}
            </p>
            <p className="text-xs text-muted-foreground">
              {order.deliveryAddress.street}
            </p>
          </div>
        </div>

        <ul className="space-y-1.5 text-sm">
          {order.lines.map((line) => (
            <li key={line.itemId} className="flex items-center justify-between gap-3">
              <span className="text-foreground/80">
                <span className="font-semibold text-foreground">{line.quantity}×</span>{" "}
                {line.itemName}
              </span>
              <span className="font-mono text-foreground/70">{formatMoney(line.lineSubtotal, order.currency)}</span>
            </li>
          ))}
        </ul>

        {order.deliveryAddress.reference ? (
          <div className="rounded-md bg-amber-50 px-3 py-2 text-xs text-amber-900">
            <span className="font-semibold">Nota:</span> {order.deliveryAddress.reference}
          </div>
        ) : null}

        <div className="flex items-center justify-between border-t border-border pt-3">
          <span className="text-xs uppercase tracking-wider text-muted-foreground">
            Total a cobrar al cliente
          </span>
          <span className="text-lg font-bold text-brand">
            {formatMoney(order.totalAmount, order.currency)}
          </span>
        </div>
      </div>

      <div className="grid grid-cols-2 gap-2 border-t border-border bg-muted/20 px-4 py-3">
        <Button type="button" variant="reject" disabled={busy} onClick={() => setRejectOpen(true)}>
          <X className="h-4 w-4" />
          Rechazar
        </Button>
        <Button type="button" variant="success" disabled={busy} onClick={onAccept}>
          <Check className="h-4 w-4" />
          Aceptar pedido
        </Button>
      </div>

      <Dialog open={rejectOpen} onOpenChange={setRejectOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Rechazar pedido #{shortId(order.orderId, "RPX-")}</DialogTitle>
            <DialogDescription>
              Cuéntale al cliente por qué no puedes prepararlo. Si lo dejas vacío usaremos "Rechazado por el merchant".
            </DialogDescription>
          </DialogHeader>
          <Textarea
            value={reason}
            onChange={(e) => setReason(e.target.value)}
            placeholder="Sin stock de ingredientes, cocina cerrada, etc."
            rows={3}
            maxLength={280}
          />
          <DialogFooter>
            <Button type="button" variant="outline" onClick={() => setRejectOpen(false)}>
              Cancelar
            </Button>
            <Button
              type="button"
              variant="reject"
              onClick={() => {
                onReject(reason.trim());
                setRejectOpen(false);
                setReason("");
              }}
            >
              Rechazar
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </Card>
  );
}
