import { type ClassValue, clsx } from "clsx";
import { twMerge } from "tailwind-merge";

export function cn(...inputs: ClassValue[]) {
  return twMerge(clsx(inputs));
}

/**
 * Formatea un decimal monetario en formato dominicano. El backend envia decimal
 * como JSON number (e.g. 1125.00) y el campo currency aparte. DOP se renderiza
 * como "RD$" — espeja el diseno aprobado.
 */
export function formatMoney(amount: number, currency = "DOP") {
  if (currency === "DOP") {
    return `RD$ ${amount.toLocaleString("es-DO", {
      minimumFractionDigits: amount % 1 === 0 ? 0 : 2,
      maximumFractionDigits: 2,
    })}`;
  }
  return new Intl.NumberFormat("es-DO", {
    style: "currency",
    currency,
    minimumFractionDigits: 2,
  }).format(amount);
}

/** Iniciales para avatares placeholder. */
export function initials(name: string | undefined | null) {
  if (!name) return "??";
  const parts = name.trim().split(/\s+/);
  if (parts.length === 1) return parts[0]!.slice(0, 2).toUpperCase();
  return (parts[0]![0] + parts[parts.length - 1]![0]).toUpperCase();
}

/** UUID v4 (o fallback) para el header Idempotency-Key en POST/PUT/DELETE. */
export function newIdempotencyKey(): string {
  if (typeof crypto !== "undefined" && "randomUUID" in crypto) {
    return crypto.randomUUID();
  }
  return `idem-${Date.now()}-${Math.random().toString(36).slice(2, 10)}`;
}

/**
 * Primeros 8 chars de un GUID, uppercase. Para "Cliente #XXXXXXXX" en la pantalla
 * de pedido activo — el backend nunca expone el nombre del cliente al courier
 * (decision de privacidad), asi que usamos el short id como referencia humana.
 */
export function shortId(id: string | null | undefined, prefix = "") {
  if (!id) return "—";
  return `${prefix}${id.split("-")[0]!.toUpperCase()}`;
}

/** Tiempo relativo en espanol para timestamps como "hace 5 min". */
export function relativeTime(iso: string): string {
  const then = new Date(iso).getTime();
  if (Number.isNaN(then)) return "";
  const diffMs = Date.now() - then;
  const sec = Math.max(0, Math.round(diffMs / 1000));
  if (sec < 60) return "hace unos segundos";
  const min = Math.round(sec / 60);
  if (min < 60) return `hace ${min} min`;
  const hr = Math.round(min / 60);
  if (hr < 24) return `hace ${hr} h`;
  const d = Math.round(hr / 24);
  return `hace ${d} d`;
}
