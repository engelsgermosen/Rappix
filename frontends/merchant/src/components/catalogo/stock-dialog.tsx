"use client";

import { useState, type FormEvent } from "react";
import { useMutation } from "@tanstack/react-query";
import { toast } from "sonner";

import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { adjustStock } from "@/lib/api/catalog-me";
import { describeError } from "@/lib/errors";
import { useStockCache } from "@/lib/stock-cache/store";

export function StockDialog({
  open,
  itemId,
  itemName,
  onClose,
}: {
  open: boolean;
  itemId: string | null;
  itemName: string | null;
  onClose: () => void;
}) {
  const [mode, setMode] = useState<"Set" | "Restock">("Set");
  const [quantity, setQuantity] = useState<number>(0);

  const cached = useStockCache((s) => (itemId ? s.cache[itemId] : undefined));
  const setStockCache = useStockCache((s) => s.set);

  const adjust = useMutation({
    mutationFn: () => adjustStock(itemId!, { mode, quantity }),
    onSuccess: (data) => {
      setStockCache(data.itemId, {
        quantity: data.quantity,
        isDepleted: data.isDepleted,
        readAtIso: new Date().toISOString(),
      });
      toast.success(`Stock actualizado: ${data.quantity} unidades.`);
      onClose();
    },
    onError: (err) => toast.error(describeError(err)),
  });

  function onSubmit(e: FormEvent) {
    e.preventDefault();
    if (!Number.isFinite(quantity) || quantity < 0) {
      toast.error("La cantidad debe ser un número mayor o igual a 0.");
      return;
    }
    if (mode === "Restock" && quantity === 0) {
      toast.error("Restock con 0 no hace nada. Usa Set 0 para vaciar el stock.");
      return;
    }
    adjust.mutate();
  }

  return (
    <Dialog open={open} onOpenChange={(v) => !v && onClose()}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Ajustar stock — {itemName ?? ""}</DialogTitle>
          <DialogDescription>
            {cached ? (
              <>
                Última lectura conocida: <span className="font-semibold text-foreground">{cached.quantity} u</span>.
              </>
            ) : (
              "Sin lectura reciente. El backend no expone GET de stock; verás el nuevo valor al guardar."
            )}
          </DialogDescription>
        </DialogHeader>

        <form onSubmit={onSubmit} className="space-y-4">
          <div className="space-y-1.5">
            <Label htmlFor="mode">Modo</Label>
            <Select value={mode} onValueChange={(v) => setMode(v as "Set" | "Restock")}>
              <SelectTrigger id="mode">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="Set">Set (valor absoluto)</SelectItem>
                <SelectItem value="Restock">Restock (sumar al actual)</SelectItem>
              </SelectContent>
            </Select>
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="qty">Cantidad</Label>
            <Input
              id="qty"
              type="number"
              min={0}
              max={9999}
              value={Number.isNaN(quantity) ? "" : quantity}
              onChange={(e) => setQuantity(Number(e.target.value))}
              required
            />
          </div>
          <DialogFooter>
            <Button type="button" variant="outline" onClick={onClose}>
              Cancelar
            </Button>
            <Button type="submit" disabled={adjust.isPending}>
              {adjust.isPending ? "Guardando..." : "Guardar"}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
