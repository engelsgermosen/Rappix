"use client";

import { create } from "zustand";

import { apiFetch } from "@/lib/api/client";
import type {
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

export const useAuthStore = create<AuthState>((set, get) => ({
  user: null,
  hydrated: false,

  hydrate: () => {
    if (get().hydrated) return;
    const stored = tokenStore.get();
    set({ user: stored?.user ?? null, hydrated: true });

    // React to cross-component token changes (login / logout / refresh).
    if (typeof window !== "undefined") {
      window.addEventListener("rappix:auth:changed", () => {
        const next = tokenStore.get();
        set({ user: next?.user ?? null });
      });
    }
  },

  async login(req) {
    const auth = await apiFetch<AuthResponse>("/api/v1/auth/login", {
      method: "POST",
      anonymous: true,
      json: req,
    });
    tokenStore.set(auth);
    set({ user: auth.user });
    return auth.user;
  },

  async register(req) {
    // Identity returns UserResponse on register (no JWT). We auto-login afterwards.
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
