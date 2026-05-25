import { AlertTriangle } from "lucide-react";

import type { WrongPortalError } from "@/lib/auth/store";

/**
 * Banner persistente (NO toast) en /login o /register cuando el usuario intenta
 * entrar con una cuenta non-Courier. Lo mantenemos persistente para que la
 * persona alcance a leer la explicacion y el portal correcto al que ir.
 */
export function WrongPortalBanner({ error }: { error: WrongPortalError }) {
  return (
    <div
      role="alert"
      className="flex gap-3 rounded-lg border border-amber-200 bg-amber-50 p-4 text-sm text-amber-900"
    >
      <AlertTriangle className="mt-0.5 h-5 w-5 shrink-0 text-amber-600" />
      <div className="space-y-1">
        <p className="font-semibold">Cuenta de otro tipo</p>
        <p>{error.message}</p>
      </div>
    </div>
  );
}
