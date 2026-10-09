import type { ProblemDetails } from "./types";

/** Lanzada por `apiFetch` para cualquier respuesta non-2xx. Lleva el body parseado. */
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

  /** Mensaje humano: detail > title > fallback HTTP. */
  display(): string {
    return this.problem?.detail || this.problem?.title || this.message;
  }
}

/** Helper para hacer switch por codigo de dominio del backend. */
export function isApiCode(err: unknown, code: string): boolean {
  return err instanceof ApiError && err.code === code;
}
