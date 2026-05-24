import { ApiError } from "@/lib/api/errors";
import { WrongPortalError } from "@/lib/auth/store";

/**
 * Turn any unknown thrown value into a Spanish error message ready for a
 * `toast.error()` call. Switches on the backend's `code` field when available;
 * falls back to ProblemDetails detail/title or a generic message otherwise.
 */
export function describeError(err: unknown): string {
  if (err instanceof WrongPortalError) {
    return err.message;
  }
  if (err instanceof ApiError) {
    if (err.code && CODE_MESSAGES[err.code]) return CODE_MESSAGES[err.code]!;
    return err.display();
  }
  if (err instanceof Error) return err.message;
  return "Ocurrió un error inesperado. Inténtalo de nuevo.";
}

const CODE_MESSAGES: Record<string, string> = {
  // Identity
  "Identity.User.InvalidCredentials": "Email o contraseña incorrectos.",
  "Identity.User.EmailInUse": "Ya existe una cuenta con ese email.",
  "Identity.User.PhoneInUse": "Ya existe una cuenta con ese teléfono.",
  "Identity.User.Inactive": "Esta cuenta está deshabilitada. Contacta a soporte.",
  "Identity.User.EmailNotConfirmed": "Debes confirmar tu email antes de continuar.",
  "Identity.User.NotFound": "Usuario no encontrado.",
  "Identity.Token.ReuseDetected": "Tu sesión se cerró por seguridad. Vuelve a entrar.",
  "Identity.Token.Invalid": "Tu sesión expiró. Vuelve a entrar.",
  "Identity.User.WrongCurrentPassword": "La contraseña actual no es correcta.",

  // Merchants
  "Merchants.Merchant.NotFound":
    "Tu comercio aún no está disponible. Vuelve a intentar en unos segundos.",
  "Merchants.Merchant.IncompleteForSubmission":
    "Completa todos los pasos del onboarding antes de enviar a revisión.",
  "Merchants.Merchant.InvalidTransition": "El comercio no está en un estado válido para esa acción.",
  "Merchants.Slug.Taken": "Esa URL pública ya está en uso. Prueba otra.",
  "Merchants.Slug.Invalid":
    "El slug solo admite minúsculas, números y guiones (sin espacios ni acentos).",
  "Merchants.ServiceArea.NotFound": "Esa zona de servicio ya no existe.",
  "Merchants.ServiceArea.Invalid": "La zona de servicio no es válida.",
  "Merchants.OperatingHours.Invalid": "Los horarios tienen un rango inválido.",
  "Merchants.Logo.TooLarge": "El logo supera el tamaño máximo de 2 MB.",
  "Merchants.PickupLocation.Invalid": "La ubicación de recogida no es válida.",

  // Catalog
  "Catalog.Catalog.NotFound": "El catálogo se crea cuando tu comercio sea aprobado.",
  "Catalog.Category.NotFound": "Esa categoría ya no existe.",
  "Catalog.Category.HasItems": "Mueve o borra los productos antes de eliminar la categoría.",
  "Catalog.Item.NotFound": "Ese producto ya no existe.",
  "Catalog.Item.NotOwned": "Ese producto no pertenece a tu comercio.",
  "Catalog.Item.InvalidPrice": "El precio debe ser mayor que cero.",
  "Catalog.Stock.Invalid": "La cantidad de stock no es válida.",
  "Catalog.Photo.TooLarge": "La foto supera el tamaño máximo de 4 MB.",

  // Orders
  "Orders.Order.NotFound": "Ese pedido ya no existe.",
  "Orders.Order.NotForMerchant": "Ese pedido no es de tu comercio.",
  "Orders.Order.InvalidState":
    "El pedido ya cambió de estado. Recargamos la lista.",
  "Orders.Order.NotOwnedByCustomer": "El pedido no es del cliente.",
};
