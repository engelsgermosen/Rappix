"use client";

import { create } from "zustand";

import { apiFetch } from "@/lib/api/client";
import { newIdempotencyKey } from "@/lib/utils";
import type {
  AccountType,
  AuthResponse,
  ChangePasswordRequest,
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
  register: (req: Omit<RegisterRequest, "accountType">) => Promise<UserResponse>;
  logout: () => Promise<void>;
  refreshMe: () => Promise<UserResponse | null>;
  updateProfile: (req: UpdateProfileRequest) => Promise<UserResponse>;
  changePassword: (req: ChangePasswordRequest) => Promise<void>;
};

/**
 * Este portal es SOLO para repartidores. Cualquier otro `userType` (Customer,
 * Merchant, Admin) es rechazado en:
 *  - login (no se guarda token, el form muestra banner persistente)
 *  - hidratacion de sesion (purga lo que haya en localStorage)
 *  - cualquier callback del evento rappix:auth:changed (por si otro tab cambia)
 *
 * Lanzar esta clase de error permite al form distinguir "wrong portal" de un
 * fallo generico de API y mostrar el mensaje preciso.
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
      case "Customer":
        return "Esta cuenta es de cliente. Para hacer pedidos, visita rappix.com.";
      case "Merchant":
        return "Esta cuenta es de comercio. Usa el panel de comercio.";
      case "Admin":
        return "Esta cuenta es interna. Usa el portal administrativo.";
      default:
        return "Esta cuenta no es de repartidor. Usa el portal que corresponde a tu tipo de cuenta.";
    }
  }
}

function isCourier(user: { userType: AccountType }): boolean {
  return user.userType === "Courier";
}

export const useAuthStore = create<AuthState>((set, get) => ({
  user: null,
  hydrated: false,

  hydrate: () => {
    if (get().hydrated) return;
    const stored = tokenStore.get();
    if (stored && !isCourier(stored.user)) {
      // Token de otro portal sobreviviendo en el mismo dominio — purga limpia.
      tokenStore.clear();
      set({ user: null, hydrated: true });
    } else {
      set({ user: stored?.user ?? null, hydrated: true });
    }

    if (typeof window !== "undefined") {
      window.addEventListener("rappix:auth:changed", () => {
        const next = tokenStore.get();
        if (next && !isCourier(next.user)) {
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
    if (!isCourier(auth.user)) {
      // NO persistir el token. El form atrapa WrongPortalError y muestra el banner.
      throw new WrongPortalError(auth.user.userType);
    }
    tokenStore.set(auth);
    set({ user: auth.user });
    return auth.user;
  },

  async register(req) {
    // Desde este portal siempre se registra como Courier.
    await apiFetch<UserResponse>("/api/v1/auth/register", {
      method: "POST",
      anonymous: true,
      json: { ...req, accountType: "Courier" },
      idempotencyKey: newIdempotencyKey(),
    });
    // Auto-login: el backend permite login sin email confirmed (la confirmacion
    // es solo para acciones que la requieran — el courier no tiene ninguna hoy).
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
        // Logout es best-effort — nunca bloquea la UI.
      }
    }
    tokenStore.clear();
    set({ user: null });
  },

  async refreshMe() {
    if (!tokenStore.getAccessToken()) return null;
    try {
      const user = await apiFetch<UserResponse>("/api/v1/auth/me");
      if (!isCourier(user)) {
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
      idempotencyKey: newIdempotencyKey(),
    });
    const stored = tokenStore.get();
    if (stored) tokenStore.set({ ...stored, user });
    set({ user });
    return user;
  },

  async changePassword(req) {
    await apiFetch<void>("/api/v1/users/me/change-password", {
      method: "POST",
      json: req,
      idempotencyKey: newIdempotencyKey(),
    });
    // El backend revoca todos los refresh tokens — el siguiente 401 boota al login.
  },
}));
