import { Button } from "@/components/ui/button";

export function PromoBanner() {
  return (
    <div className="hero-brand stripes rounded-xl px-5 md:px-8 py-5 md:py-6 text-white flex flex-col md:flex-row md:items-center gap-4 md:gap-6 shadow-sm">
      <div className="flex-1">
        <div className="text-xs font-semibold uppercase tracking-wider opacity-80">Hoy en Rappix</div>
        <div className="mt-1 font-bold text-lg md:text-xl">
          Envío gratis en tu primer pedido sobre RD$ 500
        </div>
      </div>
      <Button variant="outline" className="bg-white text-foreground border-transparent hover:bg-white/90 self-start md:self-auto">
        Ver detalles
      </Button>
    </div>
  );
}
