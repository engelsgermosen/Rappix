// Liveness para el healthcheck de Docker: no toca el backend.
export const dynamic = "force-dynamic";

export function GET() {
  return Response.json({ status: "ok" });
}
