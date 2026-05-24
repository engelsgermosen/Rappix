"use client";

import { create } from "zustand";

import { apiFetch } from "@/lib/api/client";
import type {
  AccountType,
  AuthResponse,
  LoginRequest,
  RegisterRequest,
  UpdateProfileRequest,
  UserResponse,
} from "@/lib/api/types";

import { tokenStore } from "./token-store";

type AuthState = {
  user: UserResponse | null;
  hydrated: boolean;
  hydrate: () => void;
  login: (req: LoginRequest) => Promise<UserResponse>;
  register: (req: RegisterRequest) => Promise<UserResponse>;
  logout: () => Promise<void>;
  refreshMe: () => Promise<UserResponse | null>;
  updateProfile: (req: UpdateProfileRequest) => Promise<UserResponse>;
};

/**
 * This portal is for CUSTOMERS only. Any other `userType` (Merchant,
 * Courier, Admin) is rejected at:
 *  - login time (no token gets stored, the user sees a clear message)
 *  - session restore (hydrate clears whatever was in localStorage)
 *
 * Throwing this specific error class lets the /login form distinguish
 * "wrong portal" from a generic API failure and surface a tailored toast.
 */
export class WrongPortalError extends Error {
  readonly userType: AccountType;
  constructor(userType: AccountType) {
    super(WrongPortalError.messageFor(userType));
    this.name = "WrongPortalError";
    this.userType = userType;
  }
  static messageFor(userType: AccountType): string {
    switch (userType) {
      case "Merchant":
        return "Esta cuenta es de comercio. Usa el portal de comercios para entrar.";
      case "Courier":
        return "Esta cuenta es de repartidor. Usa la app de repartidores para entrar.";
      case "Admin":
        return "Esta cuenta es administrativa. Usa el portal interno para entrar.";
      default:
        return "Esta cuenta no es de cliente. Si eres comercio, repartidor o admin, usa el portal correspondiente.";
    }
  }
}

function isCustomer(user: { userType: AccountType }): boolean {
  return user.userType === "Customer";
}

export const useAuthStore = create<AuthState>((set, get) => ({
  user: null,
  hydrated: false,

  hydrate: () => {
    if (get().hydrated) return;
    const stored = tokenStore.get();
    // SECURITY: if a non-Customer token is in localStorage (maybe leftover
    // from another portal sharing the same domain), purge it on hydrate so
    // the rest of the app never sees a non-Customer session.
    if (stored && !isCustomer(stored.user)) {
      tokenStore.clear();
      set({ user: null, hydrated: true });
    } else {
      set({ user: stored?.user ?? null, hydrated: true });
    }

    if (typeof window !== "undefined") {
      window.addEventListener("rappix:auth:changed", () => {
        const next = tokenStore.get();
        // Same guard on cross-component token writes.
        if (next && !isCustomer(next.user)) {
          tokenStore.clear();
          set({ user: null });
        } else {
          set({ user: next?.user ?? null });
        }
      });
    }
  },

  async login(req) {
    const auth = await apiFetch<AuthResponse>("/api/v1/auth/login", {
      method: "POST",
      anonymous: true,
      json: req,
    });
    if (!isCustomer(auth.user)) {
      // Do NOT persist the token. Do NOT update the UI session. The form
      // will catch this and surface the message.
      throw new WrongPortalError(auth.user.userType);
    }
    tokenStore.set(auth);
    set({ user: auth.user });
    return auth.user;
  },

  async register(req) {
    // We always send accountType: Customer to /register from this portal.
    await apiFetch<UserResponse>("/api/v1/auth/register", {
      method: "POST",
      anonymous: true,
      json: { ...req, accountType: "Customer" },
    });
    return get().login({ identifier: req.email, password: req.password });
  },

  async logout() {
    const refreshToken = tokenStore.getRefreshToken();
    if (refreshToken) {
      try {
        await apiFetch("/api/v1/auth/logout", {
          method: "POST",
          anonymous: true,
          json: { refreshToken },
        });
      } catch {
        // Logout is best-effort — never block the UI.
      }
    }
    tokenStore.clear();
    set({ user: null });
  },

  async refreshMe() {
    if (!tokenStore.getAccessToken()) return null;
    try {
      const user = await apiFetch<UserResponse>("/api/v1/auth/me");
      if (!isCustomer(user)) {
        // Token still valid but role changed (or wrong portal). Clear.
        tokenStore.clear();
        set({ user: null });
        return null;
      }
      const stored = tokenStore.get();
      if (stored) tokenStore.set({ ...stored, user });
      set({ user });
      return user;
    } catch {
      return null;
    }
  },

  async updateProfile(req) {
    const user = await apiFetch<UserResponse>("/api/v1/users/me", {
      method: "PUT",
      json: req,
    });
    const stored = tokenStore.get();
    if (stored) tokenStore.set({ ...stored, user });
    set({ user });
    return user;
  },
}));
