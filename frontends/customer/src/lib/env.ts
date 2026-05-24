/**
 * Type-safe access to NEXT_PUBLIC_* env vars. Throws at import time if the API
 * URL is missing — we never want to start without knowing where the gateway is.
 */
function required(name: string, value: string | undefined): string {
  if (!value || value.length === 0) {
    throw new Error(`Missing required env var ${name}. Did you copy .env.local.example?`);
  }
  return value;
}

function optionalNumber(value: string | undefined, fallback: number): number {
  if (!value) return fallback;
  const n = Number(value);
  return Number.isFinite(n) ? n : fallback;
}

export const env = {
  apiUrl: required("NEXT_PUBLIC_API_URL", process.env.NEXT_PUBLIC_API_URL),
  stripePublishableKey: process.env.NEXT_PUBLIC_STRIPE_PUBLISHABLE_KEY ?? "",
  defaultLat: optionalNumber(process.env.NEXT_PUBLIC_DEFAULT_LAT, 18.4719),
  defaultLng: optionalNumber(process.env.NEXT_PUBLIC_DEFAULT_LNG, -69.9),
  defaultDistanceKm: optionalNumber(process.env.NEXT_PUBLIC_DEFAULT_DISTANCE_KM, 2.5),
};
