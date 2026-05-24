import type { ProblemDetails } from "./types";

/** Thrown by `apiFetch` for any non-2xx response. Carries the parsed body. */
export class ApiError extends Error {
  readonly status: number;
  readonly code?: string;
  readonly problem?: ProblemDetails;

  constructor(message: string, status: number, problem?: ProblemDetails) {
    super(message);
    this.name = "ApiError";
    this.status = status;
    this.code = problem?.code;
    this.problem = problem;
  }

  /** Human message picked from the ProblemDetails or HTTP fallback. */
  display(): string {
    return this.problem?.detail || this.problem?.title || this.message;
  }
}

/** Convenience matcher for specific backend error codes (e.g. `Pricing.Quote.Expired`). */
export function isApiCode(err: unknown, code: string): boolean {
  return err instanceof ApiError && err.code === code;
}
