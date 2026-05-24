import type { AddressInput, SavedAddress } from "./types";

/**
 * The repository contract. Today the only real implementation is the
 * localStorage repo, keyed BY USER (so two Customers signed-in on the
 * same browser each get their own address book). When no user is
 * logged in, the anonymous repo returns an empty list and refuses
 * writes — the UI uses that to render an empty / hidden state.
 *
 * Methods are SYNC because localStorage is sync. A future Remote impl
 * can return Promises; the Zustand store already wraps writes in
 * imperative-friendly methods so that change is local to this layer.
 */
export interface AddressRepository {
  list(): SavedAddress[];
  getDefault(): SavedAddress | null;
  add(input: AddressInput): SavedAddress;
  update(id: string, patch: Partial<AddressInput>): SavedAddress | null;
  remove(id: string): void;
  setDefault(id: string): void;
}

const STORAGE_PREFIX = "rappix.customer.addresses";
const LEGACY_GLOBAL_KEY = "rappix.customer.addresses";

/** Per-user storage key. Each Customer's address book is isolated. */
export function storageKeyFor(userId: string): string {
  return `${STORAGE_PREFIX}:${encodeURIComponent(userId)}`;
}

/**
 * One-shot cleanup of pre-multitenant data. Earlier versions of this
 * portal wrote addresses to the global `rappix.customer.addresses` key
 * with no user scoping. If that key still exists on hydrate, we drop it
 * — there's no way to attribute it to a user, and keeping it leaks
 * stale data to whoever logs in first. The cleanup is idempotent.
 */
export function cleanupLegacyAddresses(): void {
  if (typeof window === "undefined") return;
  // The legacy key has no ":userId" suffix; the new keys do.
  if (window.localStorage.getItem(LEGACY_GLOBAL_KEY) !== null) {
    window.localStorage.removeItem(LEGACY_GLOBAL_KEY);
  }
}

function readAllFromKey(key: string): SavedAddress[] {
  if (typeof window === "undefined") return [];
  try {
    const raw = window.localStorage.getItem(key);
    if (!raw) return [];
    const parsed = JSON.parse(raw) as unknown;
    if (!Array.isArray(parsed)) return [];
    return parsed.filter(isValidAddress) as SavedAddress[];
  } catch {
    return [];
  }
}

function isValidAddress(value: unknown): value is SavedAddress {
  if (!value || typeof value !== "object") return false;
  const a = value as Record<string, unknown>;
  return (
    typeof a.id === "string" &&
    typeof a.street === "string" &&
    typeof a.latitude === "number" &&
    typeof a.longitude === "number" &&
    typeof a.isDefault === "boolean"
  );
}

function writeAllToKey(key: string, list: SavedAddress[]): void {
  if (typeof window === "undefined") return;
  window.localStorage.setItem(key, JSON.stringify(list));
  // Same-tab listeners (the Zustand store) get this signal.
  window.dispatchEvent(new CustomEvent("rappix:addresses:changed", { detail: { key } }));
}

function uid(): string {
  if (typeof crypto !== "undefined" && "randomUUID" in crypto) return crypto.randomUUID();
  return `addr-${Date.now()}-${Math.random().toString(36).slice(2, 10)}`;
}

/**
 * Build a repository bound to one userId. Each Customer's address book
 * lives under its own `rappix.customer.addresses:<userId>` key.
 */
export function createLocalStorageAddressRepository(userId: string): AddressRepository {
  const key = storageKeyFor(userId);

  return {
    list(): SavedAddress[] {
      const list = readAllFromKey(key);
      // Stable order: default first, then by createdAt ascending.
      return [...list].sort((a, b) => {
        if (a.isDefault && !b.isDefault) return -1;
        if (!a.isDefault && b.isDefault) return 1;
        return (a.createdAtIso ?? "").localeCompare(b.createdAtIso ?? "");
      });
    },

    getDefault(): SavedAddress | null {
      return readAllFromKey(key).find((a) => a.isDefault) ?? null;
    },

    add(input: AddressInput): SavedAddress {
      const list = readAllFromKey(key);
      const isFirst = list.length === 0;
      const next: SavedAddress = {
        id: uid(),
        label: input.label?.trim() || null,
        street: input.street.trim(),
        reference: input.reference?.trim() || null,
        latitude: input.latitude,
        longitude: input.longitude,
        isDefault: isFirst,
        createdAtIso: new Date().toISOString(),
      };
      writeAllToKey(key, [...list, next]);
      return next;
    },

    update(id: string, patch: Partial<AddressInput>): SavedAddress | null {
      const list = readAllFromKey(key);
      const idx = list.findIndex((a) => a.id === id);
      if (idx < 0) return null;
      const merged: SavedAddress = {
        ...list[idx]!,
        label: patch.label !== undefined ? (patch.label?.trim() || null) : list[idx]!.label,
        street: patch.street !== undefined ? patch.street.trim() : list[idx]!.street,
        reference: patch.reference !== undefined ? (patch.reference?.trim() || null) : list[idx]!.reference,
        latitude: patch.latitude ?? list[idx]!.latitude,
        longitude: patch.longitude ?? list[idx]!.longitude,
      };
      const next = [...list];
      next[idx] = merged;
      writeAllToKey(key, next);
      return merged;
    },

    remove(id: string): void {
      const list = readAllFromKey(key);
      const removed = list.find((a) => a.id === id);
      let next = list.filter((a) => a.id !== id);
      // If we removed the default and there are others left, promote the first.
      if (removed?.isDefault && next.length > 0) {
        next = next.map((a, i) => ({ ...a, isDefault: i === 0 }));
      }
      writeAllToKey(key, next);
    },

    setDefault(id: string): void {
      const list = readAllFromKey(key);
      if (!list.some((a) => a.id === id)) return;
      writeAllToKey(key, list.map((a) => ({ ...a, isDefault: a.id === id })));
    },
  };
}

/**
 * Repo used while no Customer is logged in. Reads always return empty;
 * writes refuse so a stray UI cannot accidentally write addresses with
 * no owner.
 */
export const anonymousAddressRepository: AddressRepository = {
  list: () => [],
  getDefault: () => null,
  add: () => {
    throw new Error("Inicia sesión para guardar direcciones");
  },
  update: () => null,
  remove: () => {},
  setDefault: () => {},
};

// Kept for backwards compatibility with any code that imported the
// singleton — it now points at the anonymous repo. New code should use
// `createLocalStorageAddressRepository(userId)`.
export const localStorageAddressRepository: AddressRepository = anonymousAddressRepository;
