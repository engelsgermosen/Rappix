"use client";

import { useState } from "react";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";
import { Boxes, Pencil, Trash2 } from "lucide-react";

import { Button } from "@/components/ui/button";
import { Card } from "@/components/ui/card";
import { Switch } from "@/components/ui/switch";
import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from "@/components/ui/tooltip";
import { ItemPhoto } from "./item-photo";
import { StockDialog } from "./stock-dialog";
import { deleteItem, setItemAvailability } from "@/lib/api/catalog-me";
import { describeError } from "@/lib/errors";
import { formatMoney } from "@/lib/utils";
import type { ItemResponse } from "@/lib/api/types";

export function ItemsTable({ items }: { items: ItemResponse[] }) {
  const queryClient = useQueryClient();
  const [stockTarget, setStockTarget] = useState<ItemResponse | null>(null);

  const toggleAvailability = useMutation({
    mutationFn: ({ itemId, available }: { itemId: string; available: boolean }) =>
      setItemAvailability(itemId, { available }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["catalog", "me", "items"] });
    },
    onError: (err) => toast.error(describeError(err)),
  });

  const remove = useMutation({
    mutationFn: (itemId: string) => deleteItem(itemId),
    onSuccess: () => {
      toast.success("Producto eliminado.");
      queryClient.invalidateQueries({ queryKey: ["catalog", "me", "items"] });
    },
    onError: (err) => toast.error(describeError(err)),
  });

  if (items.length === 0) {
    return (
      <Card className="px-6 py-12 text-center">
        <p className="text-sm font-semibold text-foreground">No hay productos en esta categoría.</p>
        <p className="mt-1 text-xs text-muted-foreground">Crea uno con el botón "Añadir producto".</p>
      </Card>
    );
  }

  return (
    <>
      <Card className="overflow-hidden">
        <table className="w-full text-sm">
          <thead>
            <tr className="border-b border-border bg-muted/30">
              <th className="px-4 py-2.5 text-left text-xs font-semibold uppercase tracking-wider text-muted-foreground">
                Foto
              </th>
              <th className="px-4 py-2.5 text-left text-xs font-semibold uppercase tracking-wider text-muted-foreground">
                Producto
              </th>
              <th className="px-4 py-2.5 text-right text-xs font-semibold uppercase tracking-wider text-muted-foreground">
                Precio
              </th>
              <th className="px-4 py-2.5 text-right text-xs font-semibold uppercase tracking-wider text-muted-foreground">
                Stock
              </th>
              <th className="px-4 py-2.5 text-center text-xs font-semibold uppercase tracking-wider text-muted-foreground">
                Publicado
              </th>
              <th className="px-4 py-2.5 text-right text-xs font-semibold uppercase tracking-wider text-muted-foreground">
                Acciones
              </th>
            </tr>
          </thead>
          <tbody>
            {items.map((item) => (
              <tr key={item.id} className="border-b border-border last:border-0 hover:bg-muted/20">
                <td className="px-4 py-3">
                  <ItemPhoto itemId={item.id} hasPhoto={!!item.photoObjectKey} />
                </td>
                <td className="px-4 py-3">
                  <p className="font-semibold text-foreground">{item.name}</p>
                  {item.description ? (
                    <p className="line-clamp-1 text-xs text-muted-foreground">{item.description}</p>
                  ) : null}
                </td>
                <td className="px-4 py-3 text-right font-mono font-semibold text-brand">
                  {formatMoney(item.priceAmount, item.currency)}
                </td>
                <td className="px-4 py-3 text-right">
                  <TooltipProvider delayDuration={200}>
                    <Tooltip>
                      <TooltipTrigger asChild>
                        <Button
                          type="button"
                          variant="ghost"
                          size="sm"
                          onClick={() => setStockTarget(item)}
                          disabled={!item.tracksInventory}
                        >
                          <Boxes className="h-4 w-4" />
                          Ajustar
                        </Button>
                      </TooltipTrigger>
                      <TooltipContent>
                        {item.tracksInventory
                          ? "El backend no expone GET de stock; abre el modal para ver y ajustar."
                          : "Este item no usa inventario."}
                      </TooltipContent>
                    </Tooltip>
                  </TooltipProvider>
                </td>
                <td className="px-4 py-3 text-center">
                  <Switch
                    checked={item.isAvailable}
                    onCheckedChange={(checked) =>
                      toggleAvailability.mutate({ itemId: item.id, available: checked })
                    }
                    disabled={toggleAvailability.isPending}
                  />
                </td>
                <td className="px-4 py-3 text-right">
                  <div className="inline-flex items-center gap-1">
                    <Button
                      type="button"
                      variant="ghost"
                      size="icon"
                      aria-label={`Editar ${item.name}`}
                      onClick={() => toast.info("La edición inline llega en una próxima iteración.")}
                    >
                      <Pencil className="h-4 w-4" />
                    </Button>
                    <Button
                      type="button"
                      variant="ghost"
                      size="icon"
                      aria-label={`Eliminar ${item.name}`}
                      onClick={() => {
                        if (confirm(`¿Eliminar "${item.name}"?`)) remove.mutate(item.id);
                      }}
                    >
                      <Trash2 className="h-4 w-4 text-destructive" />
                    </Button>
                  </div>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </Card>

      <StockDialog
        open={!!stockTarget}
        itemId={stockTarget?.id ?? null}
        itemName={stockTarget?.name ?? null}
        onClose={() => setStockTarget(null)}
      />
    </>
  );
}
