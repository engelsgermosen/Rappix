"use client";

import { create } from "zustand";

import { tokenStore } from "@/lib/auth/token-store";

import {
  anonymousAddressRepository,
  cleanupLegacyAddresses,
  createLocalStorageAddressRepository,
  storageKeyFor,
  type AddressRepository,
} from "./repository";
import type { AddressInput, SavedAddress } from "./types";

/**
 * Address store — auth-aware.
 *
 * The repository is rebuilt whenever the active userId changes:
 *   - login/refresh that produces a new user → bind repo to user.id
 *   - logout / non-Customer purge → fall back to the anonymous repo
 *
 * Components never know about the userId; they just consume
 * `addresses` / `defaultAddress` and call `add` / `update` / etc.
 * The store keeps the right repo wired internally.
 *
 * The store listens to `rappix:auth:changed` (already fired by the
 * tokenStore on login/logout/refresh) so we don't need an extra
 * subscription against the Zustand auth store — keeps stores
 * dependency-clean.
 */
type AddressState = {
  hydrated: boolean;
  userId: string | null;
  repository: AddressRepository;
  addresses: SavedAddress[];
  defaultAddress: SavedAddress | null;

  hydrate: () => void;
  setUserId: (userId: string | null) => void;
  setRepository: (repo: AddressRepository) => void;

  add: (input: AddressInput) => SavedAddress;
  update: (id: string, patch: Partial<AddressInput>) => SavedAddress | null;
  remove: (id: string) => void;
  setDefault: (id: string) => void;
};

function snapshot(repo: AddressRepository): { addresses: SavedAddress[]; defaultAddress: SavedAddress | null } {
  const addresses = repo.list();
  return {
    addresses,
    defaultAddress: addresses.find((a) => a.isDefault) ?? null,
  };
}

function readUserIdFromToken(): string | null {
  const stored = tokenStore.get();
  if (!stored) return null;
  if (stored.user.userType !== "Customer") return null;
  return stored.user.id;
}

export const useAddressStore = create<AddressState>((set, get) => ({
  hydrated: false,
  userId: null,
  repository: anonymousAddressRepository,
  addresses: [],
  defaultAddress: null,

  hydrate: () => {
    if (get().hydrated) return;

    // One-shot cleanup of the pre-multitenant global key.
    cleanupLegacyAddresses();

    const initialUserId = readUserIdFromToken();
    const repo = initialUserId
      ? createLocalStorageAddressRepository(initialUserId)
      : anonymousAddressRepository;
    set({
      hydrated: true,
      userId: initialUserId,
      repository: repo,
      ...snapshot(repo),
    });

    if (typeof window !== "undefined") {
      // React to login / logout / refresh: rebuild the repo for the
      // current user id.
      window.addEventListener("rappix:auth:changed", () => {
        const nextUserId = readUserIdFromToken();
        get().setUserId(nextUserId);
      });

      // Same-tab writes from the repository.
      window.addEventListener("rappix:addresses:changed", () => {
        set(snapshot(get().repository));
      });

      // Cross-tab writes — only react if the change targets the active
      // user's key (or a legacy/anonymous key we don't care about).
      window.addEventListener("storage", (e) => {
        if (!e.key) return;
        const uid = get().userId;
        if (uid && e.key === storageKeyFor(uid)) {
          set(snapshot(get().repository));
        }
      });
    }
  },

  setUserId: (userId) => {
    if (userId === get().userId) return;
    const repo = userId
      ? createLocalStorageAddressRepository(userId)
      : anonymousAddressRepository;
    set({ userId, repository: repo, ...snapshot(repo) });
  },

  setRepository: (repo) => {
    // Escape hatch — used by a future RemoteAddressRepository swap.
    set({ repository: repo, ...snapshot(repo) });
  },

  add: (input) => {
    const created = get().repository.add(input);
    set(snapshot(get().repository));
    return created;
  },

  update: (id, patch) => {
    const updated = get().repository.update(id, patch);
    set(snapshot(get().repository));
    return updated;
  },

  remove: (id) => {
    get().repository.remove(id);
    set(snapshot(get().repository));
  },

  setDefault: (id) => {
    get().repository.setDefault(id);
    set(snapshot(get().repository));
  },
}));
