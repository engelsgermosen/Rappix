import { env } from "@/lib/env";
import { ApiError } from "./errors";
import type { AuthResponse, ProblemDetails } from "./types";

/**
 * Centralized fetch wrapper. Single base URL (the API Gateway). Injects the
 * Bearer JWT, parses ProblemDetails, and transparently refreshes the token on
 * 401 (one retry max).
 *
 * Token storage is delegated to the `tokenStore` module so this client doesn't
 * import the auth Zustand store (would create a cycle).
 */

import { tokenStore } from "@/lib/auth/token-store";

export type ApiFetchOptions = Omit<RequestInit, "body"> & {
  /** JSON body — will be JSON.stringify'd and Content-Type set. */
  json?: unknown;
  /** Raw body (multipart, blobs, etc.). Mutually exclusive with `json`. */
  body?: BodyInit | null;
  /** Pass an Idempotency-Key for POST endpoints that support it. */
  idempotencyKey?: string;
  /** Skip the Bearer header even if a token exists (used for /auth endpoints). */
  anonymous?: boolean;
  /** Internal — set by the refresh-retry path to prevent infinite loops. */
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

  // Auth header (unless explicitly anonymous)
  if (!opts.anonymous) {
    const token = tokenStore.getAccessToken();
    if (token) headers.set("Authorization", `Bearer ${token}`);
  }

  // Idempotency-Key support
  if (opts.idempotencyKey) headers.set("Idempotency-Key", opts.idempotencyKey);

  // Body handling
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
    // refresh failed → propagate the original 401 so the UI can redirect
    const problem = await parseProblem(res);
    throw new ApiError("No autenticado", 401, problem);
  }

  if (!res.ok) {
    const problem = await parseProblem(res);
    const msg = problem?.detail || problem?.title || `HTTP ${res.status}`;
    throw new ApiError(msg, res.status, problem);
  }

  // 204 No Content
  if (res.status === 204) return undefined as T;

  const contentType = res.headers.get("content-type") ?? "";
  if (contentType.includes("application/json")) {
    return (await res.json()) as T;
  }
  // Some endpoints return a bare JSON string (e.g. presigned URLs).
  const text = await res.text();
  try {
    return JSON.parse(text) as T;
  } catch {
    return text as unknown as T;
  }
}

/** Build a typed fetch URL with query params, dropping null/undefined. */
export function withQuery(path: string, params: Record<string, string | number | boolean | null | undefined>): string {
  const search = new URLSearchParams();
  for (const [k, v] of Object.entries(params)) {
    if (v === null || v === undefined || v === "") continue;
    search.append(k, String(v));
  }
  const qs = search.toString();
  return qs ? `${path}?${qs}` : path;
}
