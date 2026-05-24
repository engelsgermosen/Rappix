import { type ClassValue, clsx } from "clsx";
import { twMerge } from "tailwind-merge";

export function cn(...inputs: ClassValue[]) {
  return twMerge(clsx(inputs));
}

/**
 * Format a decimal monetary value in Dominican locale. Backend sends `decimal`
 * as JSON numbers (e.g. `1125.00`) and a separate `currency` field.
 */
export function formatMoney(amount: number, currency = "DOP") {
  // DOP is rendered as "RD$" in the design — match it exactly when currency is DOP.
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

export function formatDistanceKm(km: number) {
  return `${km.toLocaleString("es-DO", { minimumFractionDigits: 1, maximumFractionDigits: 1 })} km`;
}

export function formatEtaMinutes(minutes: number) {
  if (minutes < 1) return "<1 min";
  if (minutes < 60) return `${Math.round(minutes)} min`;
  const h = Math.floor(minutes / 60);
  const m = Math.round(minutes % 60);
  return `${h}h ${m}m`;
}

export function initials(name: string | undefined | null) {
  if (!name) return "??";
  const parts = name.trim().split(/\s+/);
  if (parts.length === 1) return parts[0]!.slice(0, 2).toUpperCase();
  return (parts[0]![0] + parts[parts.length - 1]![0]).toUpperCase();
}

/** Stable color picker for placeholder avatars / hero tiles. */
export function pickBrandShade(seed: string) {
  const palette = [
    "hero-food",
    "hero-pharmacy",
    "hero-grocery",
    "hero-parcel",
    "hero-brand",
  ];
  let h = 0;
  for (let i = 0; i < seed.length; i++) h = (h * 31 + seed.charCodeAt(i)) >>> 0;
  return palette[h % palette.length]!;
}

/** Generate a random idempotency key for POST endpoints that accept it. */
export function newIdempotencyKey(): string {
  if (typeof crypto !== "undefined" && "randomUUID" in crypto) {
    return crypto.randomUUID();
  }
  // Fallback for old browsers — not cryptographically strong, but unique enough.
  return `idem-${Date.now()}-${Math.random().toString(36).slice(2, 10)}`;
}

/** Short id for display (first 8 chars of a GUID). */
export function shortId(id: string | null | undefined, prefix = "") {
  if (!id) return "—";
  return `${prefix}${id.split("-")[0]!.toUpperCase()}`;
}
