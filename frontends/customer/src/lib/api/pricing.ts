import { apiFetch } from "./client";
import type { CreateQuoteRequest, QuoteResponse } from "./types";

export const pricingApi = {
  createQuote: (req: CreateQuoteRequest, idempotencyKey?: string) =>
    apiFetch<QuoteResponse>("/api/v1/pricing/quotes", {
      method: "POST",
      json: req,
      idempotencyKey,
    }),

  getQuote: (quoteId: string) =>
    apiFetch<QuoteResponse>(`/api/v1/pricing/quotes/${encodeURIComponent(quoteId)}`),
};
