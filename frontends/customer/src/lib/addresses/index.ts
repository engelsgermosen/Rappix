export type { AddressInput, OrderAddress, SavedAddress } from "./types";
export { toOrderAddress } from "./types";
export type { AddressRepository } from "./repository";
export {
  anonymousAddressRepository,
  createLocalStorageAddressRepository,
  // Legacy alias, kept for backwards-compat. Equals anonymousAddressRepository.
  localStorageAddressRepository,
} from "./repository";
export { useAddressStore } from "./store";
