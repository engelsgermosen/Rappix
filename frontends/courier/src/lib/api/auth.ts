import { apiFetch } from "./client";
import type {
  AuthResponse,
  ChangePasswordRequest,
  LoginRequest,
  LogoutRequest,
  RefreshRequest,
  RegisterRequest,
  UpdateProfileRequest,
  UserResponse,
} from "./types";

/**
 * Wrappers tipados de los endpoints de Identity. El portal courier los usa
 * principalmente desde la store de Zustand (`useAuthStore.login/register/etc.`),
 * pero algunos (getMe) tambien se llaman directamente desde la pantalla /cuenta.
 *
 * NO incluye el accountType en el register — eso lo agrega la store al construir
 * el body (siempre "Courier" desde este portal).
 */
export const authApi = {
  login: (req: LoginRequest) =>
    apiFetch<AuthResponse>("/api/v1/auth/login", { method: "POST", anonymous: true, json: req }),
  register: (req: RegisterRequest) =>
    apiFetch<UserResponse>("/api/v1/auth/register", { method: "POST", anonymous: true, json: req }),
  refresh: (req: RefreshRequest) =>
    apiFetch<AuthResponse>("/api/v1/auth/refresh", { method: "POST", anonymous: true, json: req }),
  logout: (req: LogoutRequest) =>
    apiFetch<void>("/api/v1/auth/logout", { method: "POST", anonymous: true, json: req }),
  me: () => apiFetch<UserResponse>("/api/v1/auth/me"),
  updateProfile: (req: UpdateProfileRequest) =>
    apiFetch<UserResponse>("/api/v1/users/me", { method: "PUT", json: req }),
  changePassword: (req: ChangePasswordRequest) =>
    apiFetch<void>("/api/v1/users/me/change-password", { method: "POST", json: req }),
};
