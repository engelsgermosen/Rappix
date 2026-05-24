import { AlertTriangle } from "lucide-react";

import type { WrongPortalError } from "@/lib/auth/store";

/**
 * Persistent (non-toast) banner shown on /login or /register when the user
 * tries to enter with a non-Merchant account. We keep it persistent so the
 * person has time to read the explanation and the redirect target.
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
