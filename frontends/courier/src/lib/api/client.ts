import { env } from "@/lib/env";
import { tokenStore } from "@/lib/auth/token-store";

import { ApiError } from "./errors";
import type { AuthResponse, ProblemDetails } from "./types";

/**
 * Fetch wrapper centralizado. Single base URL (API Gateway). Inyecta el Bearer
 * JWT, parsea ProblemDetails, y refresca transparentemente el token en 401
 * (un reintento maximo).
 *
 * El token vive en `tokenStore` (vanilla, sin React) — este modulo NO importa la
 * store de Zustand para evitar el ciclo client.ts <-> store.ts.
 */

export type ApiFetchOptions = Omit<RequestInit, "body"> & {
  /** Body JSON — se JSON.stringify'a y se setea Content-Type. */
  json?: unknown;
  /** Body raw (multipart, blobs). Mutuamente exclusivo con `json`. */
  body?: BodyInit | null;
  /** Idempotency-Key para POST/PUT/DELETE — el middleware del gateway lo respeta 24h. */
  idempotencyKey?: string;
  /** No mandar Bearer aunque haya token (para /auth/* anonimo). */
  anonymous?: boolean;
  /** Interno: marca que ya estamos en el reintento post-refresh, evita loop. */
  _retrying?: boolean;
};

const JSON_HEADERS = { "Content-Type": "application/json" };

async function parseProblem(res: Response): Promise<ProblemDetails | undefined> {
  const contentType = res.headers.get("content-type") ?? "";
  if (!contentType.includes("json")) return undefined;
  try {
    return (await res.json()) as ProblemDetails;
  } catch {
    return undefined;
  }
}

// Coalesce de refresh: si N requests fallan con 401 simultaneamente, hacemos UN
// solo refresh y todas reusan el resultado.
let refreshing: Promise<AuthResponse | null> | null = null;

async function refreshOnce(): Promise<AuthResponse | null> {
  if (refreshing) return refreshing;
  const refreshToken = tokenStore.getRefreshToken();
  if (!refreshToken) return null;

  refreshing = (async () => {
    try {
      const res = await fetch(`${env.apiUrl}/api/v1/auth/refresh`, {
        method: "POST",
        headers: JSON_HEADERS,
        body: JSON.stringify({ refreshToken }),
      });
      if (!res.ok) {
        tokenStore.clear();
        return null;
      }
      const body = (await res.json()) as AuthResponse;
      tokenStore.set(body);
      return body;
    } catch {
      tokenStore.clear();
      return null;
    } finally {
      refreshing = null;
    }
  })();

  return refreshing;
}

export async function apiFetch<T = unknown>(path: string, opts: ApiFetchOptions = {}): Promise<T> {
  const url = path.startsWith("http") ? path : `${env.apiUrl}${path}`;
  const headers = new Headers(opts.headers ?? {});

  if (!opts.anonymous) {
    const token = tokenStore.getAccessToken();
    if (token) headers.set("Authorization", `Bearer ${token}`);
  }

  if (opts.idempotencyKey) headers.set("Idempotency-Key", opts.idempotencyKey);

  let body: BodyInit | null | undefined = opts.body;
  if (opts.json !== undefined) {
    body = JSON.stringify(opts.json);
    if (!headers.has("Content-Type")) headers.set("Content-Type", "application/json");
  }

  const res = await fetch(url, {
    ...opts,
    headers,
    body,
    cache: opts.cache ?? "no-store",
  });

  if (res.status === 401 && !opts.anonymous && !opts._retrying) {
    const refreshed = await refreshOnce();
    if (refreshed) {
      return apiFetch<T>(path, { ...opts, _retrying: true });
    }
    const problem = await parseProblem(res);
    throw new ApiError("No autenticado", 401, problem);
  }

  if (!res.ok) {
    const problem = await parseProblem(res);
    const msg = problem?.detail || problem?.title || `HTTP ${res.status}`;
    throw new ApiError(msg, res.status, problem);
  }

  if (res.status === 204) return undefined as T;

  const contentType = res.headers.get("content-type") ?? "";
  if (contentType.includes("application/json")) {
    return (await res.json()) as T;
  }
  const text = await res.text();
  try {
    return JSON.parse(text) as T;
  } catch {
    return text as unknown as T;
  }
}

/** Arma una URL con query params, descartando null/undefined/"" para no mandar basura. */
export function withQuery(path: string, params: Record<string, string | number | boolean | null | undefined>): string {
  const search = new URLSearchParams();
  for (const [k, v] of Object.entries(params)) {
    if (v === null || v === undefined || v === "") continue;
    search.append(k, String(v));
  }
  const qs = search.toString();
  return qs ? `${path}?${qs}` : path;
}
