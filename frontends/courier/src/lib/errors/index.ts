import { ApiError } from "@/lib/api/errors";
import { WrongPortalError } from "@/lib/auth/store";

/**
 * Convierte cualquier valor lanzado en un mensaje en espanol listo para
 * `toast.error()`. Hace switch por el `code` del backend cuando esta disponible;
 * cae a ProblemDetails detail/title o a un fallback generico si no.
 *
 * Los CODE_MESSAGES de este portal cubren Identity (compartido) y Dispatch
 * (los codigos courier-especificos como BusyCannotGoOffline, NoActiveAssignment,
 * VehicleRequired). NO incluye codigos de Merchants/Catalog/Orders porque el
 * courier no llama a esos servicios.
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
  return "Ocurrio un error inesperado. Intentalo de nuevo.";
}

const CODE_MESSAGES: Record<string, string> = {
  // -------- Identity (compartido con customer/merchant) --------
  "Identity.User.InvalidCredentials": "Email o contrasena incorrectos.",
  "Identity.User.EmailInUse": "Ya existe una cuenta con ese email.",
  "Identity.User.PhoneInUse": "Ya existe una cuenta con ese telefono.",
  "Identity.User.Inactive": "Esta cuenta esta deshabilitada. Contacta a soporte.",
  "Identity.User.EmailNotConfirmed": "Debes confirmar tu email antes de continuar.",
  "Identity.User.NotFound": "Usuario no encontrado.",
  "Identity.Token.ReuseDetected": "Tu sesion se cerro por seguridad. Vuelve a entrar.",
  "Identity.Token.Invalid": "Tu sesion expiro. Vuelve a entrar.",
  "Identity.User.WrongCurrentPassword": "La contrasena actual no es correcta.",

  // -------- Dispatch — courier-especificos --------
  "Dispatch.Courier.NotFound":
    "Tu perfil de repartidor aun no esta listo. Vuelve a intentar en unos segundos.",
  "Dispatch.Courier.VehicleRequired":
    "Configura tu vehiculo antes de conectarte.",
  "Dispatch.Courier.BusyCannotGoOffline":
    "No puedes desconectarte mientras tengas un pedido asignado. Entrega primero.",
  "Dispatch.Courier.InvalidTransition":
    "No puedes cambiar de estado ahora mismo.",
  "Dispatch.Courier.InvalidVehiclePlate":
    "La placa no es valida (maximo 20 caracteres).",
  "Dispatch.Courier.InvalidVehicleCapacity":
    "La capacidad debe ser mayor a 0.",
  "Dispatch.Courier.InvalidLocation":
    "La ubicacion enviada esta fuera de rango.",
  "Dispatch.Assignment.NoActiveAssignment":
    "Ya no tienes una asignacion activa.",
};
