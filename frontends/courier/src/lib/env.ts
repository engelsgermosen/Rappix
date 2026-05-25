/**
 * Acceso type-safe a las variables NEXT_PUBLIC_*. Lanza al import si falta el
 * API URL — no queremos arrancar sin saber donde esta el gateway. Las claves de
 * Google Maps son opcionales: sin ellas el componente del mapa renderiza un
 * placeholder y el resto del portal sigue funcionando.
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
