/**
 * SavedAddress — the customer's stored delivery address.
 *
 * `id`, `label`, and `isDefault` are FE-only metadata: never sent to the
 * backend. The order body only consumes { street, reference, latitude,
 * longitude } via `toOrderAddress`. This separation is intentional so a
 * future backend address service can adopt the same shape without breaking
 * the order endpoint contract.
 */
export type SavedAddress = {
  /** Local UUID (crypto.randomUUID). Not a backend resource id. */
  id: string;
  /** Human label like "Casa", "Oficina". `null` if user didn't set one. */
  label: string | null;
  street: string;
  reference: string | null;
  latitude: number;
  longitude: number;
  /** Exactly one row should have `isDefault = true` if the list is non-empty. */
  isDefault: boolean;
  /** ISO string — used for stable list ordering. */
  createdAtIso: string;
};

/**
 * The exact shape `POST /api/v1/orders` expects in its `deliveryAddress`
 * payload (street + reference + latitude + longitude). FE-only fields are
 * stripped here on purpose.
 */
export type OrderAddress = {
  street: string;
  reference: string | null;
  latitude: number;
  longitude: number;
};

export function toOrderAddress(a: SavedAddress): OrderAddress {
  return {
    street: a.street,
    reference: a.reference,
    latitude: a.latitude,
    longitude: a.longitude,
  };
}

/** Patch the FE may pass to `add`/`update` — id/createdAt are managed internally. */
export type AddressInput = {
  label?: string | null;
  street: string;
  reference?: string | null;
  latitude: number;
  longitude: number;
};
