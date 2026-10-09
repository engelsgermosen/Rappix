/**
 * Lectura/escritura del JWT + refresh token en localStorage. Vanilla (sin React)
 * para que apiFetch pueda usarlo sin crear un ciclo Zustand <-> apiFetch.
 *
 * NOTA: usamos una key DISTINTA por portal (rappix.courier.auth) para que los
 * tres portales (customer 3000, merchant 3001, courier 3002) puedan coexistir
 * en el mismo dominio en dev sin pisarse el token.
 */

import type { AuthResponse, UserResponse } from "@/lib/api/types";

const KEY = "rappix.courier.auth";

export type StoredAuth = {
  accessToken: string;
  accessTokenExpiresAtUtc: string;
  refreshToken: string;
  user: UserResponse;
};

function isBrowser(): boolean {
  return typeof window !== "undefined";
}

function readRaw(): StoredAuth | null {
  if (!isBrowser()) return null;
  try {
    const raw = window.localStorage.getItem(KEY);
    if (!raw) return null;
    return JSON.parse(raw) as StoredAuth;
  } catch {
    return null;
  }
}

function writeRaw(value: StoredAuth | null): void {
  if (!isBrowser()) return;
  if (value === null) {
    window.localStorage.removeItem(KEY);
  } else {
    window.localStorage.setItem(KEY, JSON.stringify(value));
  }
  window.dispatchEvent(new CustomEvent("rappix:auth:changed"));
}

export const tokenStore = {
  get(): StoredAuth | null {
    return readRaw();
  },
  getAccessToken(): string | null {
    return readRaw()?.accessToken ?? null;
  },
  getRefreshToken(): string | null {
    return readRaw()?.refreshToken ?? null;
  },
  set(auth: AuthResponse): void {
    writeRaw({
      accessToken: auth.accessToken,
      accessTokenExpiresAtUtc: auth.accessTokenExpiresAtUtc,
      refreshToken: auth.refreshToken,
      user: auth.user,
    });
  },
  clear(): void {
    writeRaw(null);
  },
};
