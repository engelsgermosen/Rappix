import type { AddressInput, SavedAddress } from "./types";

/**
 * The repository contract. Today the only implementation is
 * `LocalStorageAddressRepository`, but the moment the backend ships an
 * addresses service we can write `RemoteAddressRepository` against this
 * exact interface and swap it via the Zustand store's `setRepository` —
 * NO COMPONENT needs to change.
 *
 * Methods are SYNC because localStorage is sync. The Zustand store wraps
 * them in async hooks anyway, so a future Remote impl returning Promises
 * just requires the store to `await` instead of return-direct. Keep this
 * interface small.
 */
export interface AddressRepository {
  list(): SavedAddress[];
  getDefault(): SavedAddress | null;
  add(input: AddressInput): SavedAddress;
  update(id: string, patch: Partial<AddressInput>): SavedAddress | null;
  remove(id: string): void;
  setDefault(id: string): void;
}

const STORAGE_KEY = "rappix.customer.addresses";

function readAll(): SavedAddress[] {
  if (typeof window === "undefined") return [];
  try {
    const raw = window.localStorage.getItem(STORAGE_KEY);
    if (!raw) return [];
    const parsed = JSON.parse(raw) as unknown;
    if (!Array.isArray(parsed)) return [];
    // Trust-but-verify each row: drop anything missing required fields.
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

function writeAll(list: SavedAddress[]): void {
  if (typeof window === "undefined") return;
  window.localStorage.setItem(STORAGE_KEY, JSON.stringify(list));
  // Cross-tab + cross-component notification so the Zustand mirror re-reads.
  window.dispatchEvent(new CustomEvent("rappix:addresses:changed"));
}

function uid(): string {
  if (typeof crypto !== "undefined" && "randomUUID" in crypto) return crypto.randomUUID();
  return `addr-${Date.now()}-${Math.random().toString(36).slice(2, 10)}`;
}

/**
 * localStorage-backed repository. Single source of truth: the JSON array
 * under `rappix.customer.addresses`. Default-flag invariant is enforced on
 * every write (exactly one row has `isDefault=true` when the list is
 * non-empty; an empty list has none).
 */
export const localStorageAddressRepository: AddressRepository = {
  list(): SavedAddress[] {
    const list = readAll();
    // Stable order: default first, then by createdAt ascending.
    return [...list].sort((a, b) => {
      if (a.isDefault && !b.isDefault) return -1;
      if (!a.isDefault && b.isDefault) return 1;
      return (a.createdAtIso ?? "").localeCompare(b.createdAtIso ?? "");
    });
  },

  getDefault(): SavedAddress | null {
    const list = readAll();
    return list.find((a) => a.isDefault) ?? null;
  },

  add(input: AddressInput): SavedAddress {
    const list = readAll();
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
    writeAll([...list, next]);
    return next;
  },

  update(id: string, patch: Partial<AddressInput>): SavedAddress | null {
    const list = readAll();
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
    writeAll(next);
    return merged;
  },

  remove(id: string): void {
    const list = readAll();
    const removed = list.find((a) => a.id === id);
    let next = list.filter((a) => a.id !== id);
    // If we removed the default and there are others left, promote the first.
    if (removed?.isDefault && next.length > 0) {
      next = next.map((a, i) => ({ ...a, isDefault: i === 0 }));
    }
    writeAll(next);
  },

  setDefault(id: string): void {
    const list = readAll();
    if (!list.some((a) => a.id === id)) return;
    writeAll(list.map((a) => ({ ...a, isDefault: a.id === id })));
  },
};
