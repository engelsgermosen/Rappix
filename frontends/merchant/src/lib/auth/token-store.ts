/**
 * Plain object that reads/writes JWT + refresh token from localStorage. Lives
 * outside the React tree so the api client can use it without creating a
 * Zustand→React cycle. The Zustand store wraps this for the UI.
 *
 * NOTE: separate localStorage key from the Customer portal so both portals
 * can coexist on the same domain in development without clobbering each other.
 */

import type { AuthResponse, UserResponse } from "@/lib/api/types";

const KEY = "rappix.merchant.auth";

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
