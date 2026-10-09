import type { CourierStatus, VehicleType } from "@/lib/api/types";
import { shortId } from "@/lib/utils";

/** Etiqueta humana del estado del courier (espanol, oracion). */
export function formatStatus(status: CourierStatus): string {
  switch (status) {
    case "Offline":
      return "Desconectado";
    case "Online":
      return "Conectado";
    case "Busy":
      return "En pedido";
  }
}

/** Etiqueta humana del vehiculo. */
export function formatVehicleType(type: VehicleType): string {
  switch (type) {
    case "Moto":
      return "Moto";
    case "Bici":
      return "Bicicleta";
    case "Carro":
      return "Carro";
  }
}

/**
 * Identificador corto del cliente para la pantalla de pedido activo. El backend
 * NO comparte el nombre del cliente al courier (decision de privacidad — solo
 * se expone customerUserId plano). Mostramos "Cliente #XXXXXXXX" donde la X son
 * los primeros 8 chars del GUID en uppercase.
 */
export function formatCustomerShortId(customerUserId: string): string {
  return shortId(customerUserId, "Cliente #");
}
