import { ShoppingBag } from "lucide-react";

import { Card } from "@/components/ui/card";

export function PedidosEmptyState() {
  return (
    <Card className="px-6 py-16 text-center">
      <div className="mx-auto mb-4 flex h-14 w-14 items-center justify-center rounded-full bg-muted">
        <ShoppingBag className="h-7 w-7 text-muted-foreground" />
      </div>
      <h2 className="text-lg font-semibold text-foreground">No hay pedidos entrantes</h2>
      <p className="mx-auto mt-1 max-w-md text-sm text-muted-foreground">
        Cuando un cliente haga un pedido, lo verás aparecer aquí en tiempo real.
      </p>
    </Card>
  );
}
