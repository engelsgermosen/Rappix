/**
 * Type-safe access to NEXT_PUBLIC_* env vars. Throws at import time if the API
 * URL is missing — we never want to start without knowing where the gateway is.
 * Google Maps keys are optional: without them the map components render a
 * "Mapa no disponible" state and the rest of the app keeps working.
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

function optionalString(value: string | undefined): string {
  return value ?? "";
}

export const env = {
  apiUrl: required("NEXT_PUBLIC_API_URL", process.env.NEXT_PUBLIC_API_URL),
  googleMapsApiKey: optionalString(process.env.NEXT_PUBLIC_GOOGLE_MAPS_API_KEY),
  googleMapsMapId: optionalString(process.env.NEXT_PUBLIC_GOOGLE_MAPS_MAP_ID),
  defaultLat: optionalNumber(process.env.NEXT_PUBLIC_DEFAULT_LAT, 18.4719),
  defaultLng: optionalNumber(process.env.NEXT_PUBLIC_DEFAULT_LNG, -69.9),
};
