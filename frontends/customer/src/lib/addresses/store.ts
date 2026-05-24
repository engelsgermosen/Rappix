"use client";

import { create } from "zustand";

import { localStorageAddressRepository, type AddressRepository } from "./repository";
import type { AddressInput, SavedAddress } from "./types";

/**
 * Zustand mirror of the address repository. Components subscribe to this
 * store; the store delegates persistence to whatever `repository` is wired.
 *
 * Future backend swap: build a `RemoteAddressRepository` that hits the new
 * /api/v1/addresses endpoints with the same interface, then call
 * `useAddressStore.getState().setRepository(remoteRepo)` once at boot.
 * Every component that consumes `addresses` / `defaultAddress` / `add` /
 * etc. keeps working unchanged.
 */
type AddressState = {
  hydrated: boolean;
  repository: AddressRepository;
  addresses: SavedAddress[];
  defaultAddress: SavedAddress | null;

  /** Read once from the repo + subscribe to cross-component changes. */
  hydrate: () => void;
  /** Swap the underlying repository (e.g. when a backend lands). */
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

export const useAddressStore = create<AddressState>((set, get) => ({
  hydrated: false,
  repository: localStorageAddressRepository,
  addresses: [],
  defaultAddress: null,

  hydrate: () => {
    if (get().hydrated) return;
    set({ ...snapshot(get().repository), hydrated: true });

    if (typeof window !== "undefined") {
      const refresh = () => set(snapshot(get().repository));
      window.addEventListener("rappix:addresses:changed", refresh);
      // Cross-tab — localStorage `storage` event fires in OTHER tabs.
      window.addEventListener("storage", (e) => {
        if (e.key === "rappix.customer.addresses") refresh();
      });
    }
  },

  setRepository: (repo) => {
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
