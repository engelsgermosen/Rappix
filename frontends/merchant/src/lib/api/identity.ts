/**
 * Identity endpoints — thin wrappers over `apiFetch`. Login/register are
 * driven from the Zustand auth store; these are for /me, profile updates and
 * password changes.
 */
import { apiFetch } from "./client";
import type {
  AuthResponse,
  ChangePasswordRequest,
  UpdateProfileRequest,
  UserResponse,
} from "./types";

/** GET /api/v1/auth/me — current user. */
export function getMe(): Promise<UserResponse> {
  return apiFetch<UserResponse>("/api/v1/auth/me");
}

/** POST /api/v1/auth/resend-confirmation — resend email confirmation link. */
export function resendConfirmation(email: string): Promise<void> {
  return apiFetch<void>("/api/v1/auth/resend-confirmation", {
    method: "POST",
    anonymous: true,
    json: { email },
  });
}

/** POST /api/v1/auth/refresh — manually rotate tokens. */
export function refreshTokens(refreshToken: string): Promise<AuthResponse> {
  return apiFetch<AuthResponse>("/api/v1/auth/refresh", {
    method: "POST",
    anonymous: true,
    json: { refreshToken },
  });
}

/** PUT /api/v1/users/me — update personal profile (name, phone). */
export function updateProfile(req: UpdateProfileRequest): Promise<UserResponse> {
  return apiFetch<UserResponse>("/api/v1/users/me", {
    method: "PUT",
    json: req,
  });
}

/** POST /api/v1/users/me/change-password — revokes all refresh tokens on success. */
export function changePassword(req: ChangePasswordRequest): Promise<void> {
  return apiFetch<void>("/api/v1/users/me/change-password", {
    method: "POST",
    json: req,
  });
}
