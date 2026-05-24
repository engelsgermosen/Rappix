import { type ClassValue, clsx } from "clsx";
import { twMerge } from "tailwind-merge";

export function cn(...inputs: ClassValue[]) {
  return twMerge(clsx(inputs));
}

/**
 * Format a decimal monetary value in Dominican locale. Backend sends `decimal`
 * as JSON numbers (e.g. `1125.00`) and a separate `currency` field. DOP is
 * rendered as "RD$" — match the design.
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

/** Initials from a name for placeholder avatars. */
export function initials(name: string | undefined | null) {
  if (!name) return "??";
  const parts = name.trim().split(/\s+/);
  if (parts.length === 1) return parts[0]!.slice(0, 2).toUpperCase();
  return (parts[0]![0] + parts[parts.length - 1]![0]).toUpperCase();
}

/** Random idempotency key for POST/PUT/DELETE endpoints that accept it. */
export function newIdempotencyKey(): string {
  if (typeof crypto !== "undefined" && "randomUUID" in crypto) {
    return crypto.randomUUID();
  }
  return `idem-${Date.now()}-${Math.random().toString(36).slice(2, 10)}`;
}

/** First 8 chars of a GUID uppercased — used to display order/customer ids without leaking the full id. */
export function shortId(id: string | null | undefined, prefix = "") {
  if (!id) return "—";
  return `${prefix}${id.split("-")[0]!.toUpperCase()}`;
}

/** Spanish relative time for "hace X" labels in order cards. */
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

/** Spanish day-of-week labels keyed by the .NET DayOfWeek enum name. */
export const DAY_LABEL: Record<string, string> = {
  Sunday: "Domingo",
  Monday: "Lunes",
  Tuesday: "Martes",
  Wednesday: "Miércoles",
  Thursday: "Jueves",
  Friday: "Viernes",
  Saturday: "Sábado",
};

export const DAYS_ORDERED = ["Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday"] as const;

/** Format kilometers in es-DO with one decimal — e.g. 2.4 km. */
export function formatDistanceKm(km: number): string {
  return `${km.toLocaleString("es-DO", { minimumFractionDigits: 1, maximumFractionDigits: 1 })} km`;
}
