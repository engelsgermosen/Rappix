import Link from "next/link";

import { RappixMark } from "./rappix-logo";

export function AppFooter() {
  return (
    <footer className="border-t border-border bg-white">
      <div className="container py-10 grid gap-8 md:grid-cols-4">
        <div>
          <div className="flex items-center gap-3 mb-4">
            <RappixMark size={32} />
            <div className="font-bold tracking-tight text-lg">Rappix</div>
          </div>
          <p className="text-sm text-muted-foreground max-w-xs">
            La forma más rápida de pedir comida, farmacia, súper y envíos en República Dominicana.
          </p>
        </div>
        <div>
          <div className="font-semibold mb-3">Cliente</div>
          <ul className="space-y-2 text-sm text-muted-foreground">
            <li><Link href="/" className="hover:text-brand">Comercios cercanos</Link></li>
            <li><Link href="/pedidos" className="hover:text-brand">Mis pedidos</Link></li>
            <li><Link href="/cuenta" className="hover:text-brand">Mi cuenta</Link></li>
          </ul>
        </div>
        <div>
          <div className="font-semibold mb-3">Compañía</div>
          <ul className="space-y-2 text-sm text-muted-foreground">
            <li><a className="hover:text-brand" href="#">Acerca de Rappix</a></li>
            <li><a className="hover:text-brand" href="#">Únete como repartidor</a></li>
            <li><a className="hover:text-brand" href="#">Registra tu comercio</a></li>
          </ul>
        </div>
        <div>
          <div className="font-semibold mb-3">Legal</div>
          <ul className="space-y-2 text-sm text-muted-foreground">
            <li><a className="hover:text-brand" href="#">Términos</a></li>
            <li><a className="hover:text-brand" href="#">Privacidad</a></li>
            <li><a className="hover:text-brand" href="#">Soporte</a></li>
          </ul>
        </div>
      </div>
      <div className="border-t border-border">
        <div className="container py-5 text-xs text-muted-foreground flex flex-col sm:flex-row gap-2 sm:items-center sm:justify-between">
          <div>© {new Date().getFullYear()} Rappix. Hecho en RD.</div>
          <div>Pagos seguros · Soporte 24/7</div>
        </div>
      </div>
    </footer>
  );
}
