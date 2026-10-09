import { Loader2 } from "lucide-react";

/**
 * Tarjeta que se muestra entre el poll y la asignacion: el courier esta Online
 * (o Busy esperando que el read model actualice), no hay pedido renderizable
 * todavia. Pequeno spinner para no dejar la pantalla "muerta".
 */
export function WaitingCard() {
  return (
    <div className="w-full max-w-[600px] mx-auto rounded-2xl border border-dashed border-border bg-white p-8 text-center">
      <Loader2 className="mx-auto h-8 w-8 animate-spin text-brand" />
      <p className="mt-3 text-sm font-medium text-foreground">Buscando tu primer pedido</p>
      <p className="text-xs text-muted-foreground">Te avisamos en cuanto tengas una asignacion.</p>
    </div>
  );
}
