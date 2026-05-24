# Rappix Merchant Portal — FINDINGS

This document captures every place where the screenshot designs implied UI or
state that the **backend doesn't currently expose**. The agreed rule with the
product owner during planning is: *when the backend can't back it, the UI is
dropped, not faked.* All gaps below are real backend gaps to file with the
service team; the merchant portal works around each of them honestly.

> All references to source files in the backend are read-only — this portal
> never touches `/src`. Endpoint shapes come from
> [`frontends/MERCHANT_API_MAP.md`](../MERCHANT_API_MAP.md).

---

## 1. Teléfono comercial vs. teléfono personal

**Design**: Onboarding paso 1 ("Datos básicos") muestra un campo "Teléfono de contacto" debajo del RNC.

**Backend**: `UpdateMerchantProfileRequest` (`src/Services/Merchants/Rappix.Merchants.Api/Contracts/MerchantRequests.cs:4`) acepta `name`, `slug`, `rnc`, `description`, `verticalType`. **No tiene `contactPhone`**. El teléfono vive en `Identity.User.PhoneNumber`, gestionado via `PUT /api/v1/users/me`.

**Workaround**: el campo se quitó del wizard. El merchant gestiona su teléfono personal en `/console/cuenta`, igual que un Customer haría.

**Backend ask**: añadir `contactPhone` al agregado `Merchant` (separado del usuario) si se quiere mostrarlo al cliente final.

---

## 2. Pickup sin texto descriptivo (Referencia / Instrucciones)

**Design**: Onboarding paso 4 ("Ubicación") muestra "Referencia (opcional)" + "Instrucciones para el repartidor".

**Backend**: `SetPickupLocationRequest` (`src/Services/Merchants/Rappix.Merchants.Api/Contracts/MerchantRequests.cs:29`) solo acepta `{ latitude, longitude }`. No hay tabla para referencia ni instrucciones del pickup.

**Workaround**: ambos campos eliminados del wizard. El input de "Dirección de recogida" se mantiene como display-only en `localStorage.rappix.merchant.pickup-address` y `localStorage.rappix.merchant.pickup-done`. Es solo para que el merchant vea dónde marcó el pin — el backend nunca lo recibe ni lo expone.

**Backend ask**: agregar `pickupAddressText`, `pickupReference`, `pickupInstructions` al merchant (o crear un `PickupDetails` value object) si los repartidores necesitan instrucciones contextuales.

---

## 3. Sin endpoint para pausar/reactivar pedidos del dueño

**Design**: la cabecera de `/pedidos` muestra un botón "Pausar nuevos pedidos".

**Backend**: el estado `MerchantStatus.Paused` existe (`src/Services/Merchants/Rappix.Merchants.Domain/Merchants/MerchantStatus.cs:13`) pero **no hay endpoint merchant-facing** para flipearlo. Solo el admin puede via `POST /api/v1/admin/merchants/{id}/suspend|unsuspend`, lo cual no es para self-service.

**Workaround**: el botón se eliminó. El badge "Comercio activo / pausado" se mantiene en read-only — refleja el `merchant.status` real.

**Backend ask**: `POST /api/v1/merchants/me/pause` y `POST /api/v1/merchants/me/unpause` con permission del owner.

---

## 4. Sin agregados diarios para el comercio (KPIs)

**Design**: los KPI cards "Aceptados hoy", "Rechazados hoy", "Ventas hoy".

**Backend**: `/api/v1/orders/merchant/pending` solo devuelve los `OrderStatus.AwaitingMerchant`. No hay endpoint que agregue por status diario, ni un sum de totales.

**Workaround**: los 3 KPIs descartados. Se mantiene solo "Pendientes ahora" (calculado de la longitud del array de pendientes — dato real).

**Backend ask**: `GET /api/v1/merchants/me/stats?from=...&to=...` que devuelva `{ accepted, rejected, completed, revenueAmount, currency }`.

---

## 5. Nombre del cliente no expuesto al merchant

**Design**: las cards de pedido muestran "Ana M.", "Luis P.", "Carolina V.".

**Backend**: `OrderResponse` (`src/Services/Orders/Rappix.Orders.Application/Responses/OrderResponse.cs`) incluye `customerUserId: string (GUID)` pero **no incluye `customerFirstName` ni `customerInitials`**. Tampoco hay endpoint público para resolver `userId → nombre`.

**Workaround**: cada card muestra `Cliente #ABCD1234` usando `shortId(customerUserId)` (primeros 8 chars de la GUID en mayúsculas).

**Backend ask**: incluir `customerDisplayName` (primer nombre + inicial del apellido) en `OrderResponse` cuando lo lee el merchant, o exponer `GET /api/v1/users/{id}/public-name` con la misma forma.

---

## 6. Sin GET de stock por item

**Design**: la columna "Stock" en la tabla del catálogo muestra "28 u", "18 u", etc.

**Backend**: `GET /api/v1/catalog/me/items` devuelve `ItemResponse[]` que **no incluye el quantity de stock**. La única forma de leer stock es como respuesta de `PUT /items/{id}/stock` (mode Set o Restock).

**Workaround**: la columna numérica se reemplazó por un botón "Ajustar" por fila. El modal `StockDialog` muestra el último valor conocido **solo si se hizo un ajuste en esta sesión** (cache `useStockCache` en memoria); si no, "Sin lectura reciente". Se pierde al recargar — documentado en el copy del modal.

**Backend ask**: incluir `stockQuantity` y `stockIsDepleted` en `ItemResponse` cuando se lista desde `/catalog/me/items` (o exponer `GET /catalog/me/items/{id}/stock` standalone).

---

## 7. Sin reordenar categorías

**Design**: ninguno del screenshot tiene drag-and-drop, pero el orden importa visualmente.

**Backend**: `POST /catalog/me/categories` acepta `sortOrder` al crear. No hay `PUT /categories/{id}/order` ni endpoint de bulk reorder.

**Workaround**: no se implementa drag-to-reorder. El portal respeta el `sortOrder` del backend al renderizar; el merchant lo define al crear cada categoría (auto-incrementado client-side: nueva categoría = sortOrder de la lista * 10 + 10).

**Backend ask**: `PUT /api/v1/catalog/me/categories/{id}/order` con `{ sortOrder: number }`, o `PUT /api/v1/catalog/me/categories/order` con `[{ id, sortOrder }]`.

---

## 8. Sin tracking en vivo para el merchant

**Design**: no aparece en las screenshots del merchant — el customer sí ve un mapa con courier en vivo.

**Backend**: `OrderTracking` (`src/Services/Tracking/Rappix.Tracking.Domain/OrderTrackings/OrderTracking.cs:55`) almacena `CustomerUserId` y `TrackingHub.cs:52` valida ownership comparando `JWT.sub == CustomerUserId`. **El merchant del pedido no es dueño en el modelo de tracking**, así que un GET de tracking le devuelve 404 (indistinguible de 403).

**Workaround**: el portal no tiene pantalla de tracking. El merchant acepta un pedido y la saga se encarga del resto sin visibilidad del progreso.

**Backend ask**: añadir `MerchantOwnerUserId` al `OrderTracking` y validarlo además del Customer. Idealmente, exponer un hub `/hubs/tracking-merchant` o reutilizar el mismo con ownership dual.

---

## 9. Sin SignalR push de pedidos nuevos para el comercio

**Design**: el banner "Auto-actualización cada 10 s · Próxima en 5s" sugiere polling, pero un push sería más fresco.

**Backend**: el único hub SignalR es `/hubs/tracking` y solo lo usa el Customer. No hay hub para que el merchant reciba eventos de `OrderSubmitted` / `OrderAwaitingMerchant` en push.

**Workaround**: polling de `/orders/merchant/pending` cada 10 s (`refetchInterval: 10_000` en TanStack Query, `refetchOnWindowFocus: true`). El banner muestra el countdown.

**Backend ask**: hub `/hubs/merchant-orders` con `Subscribe(merchantId)` que emita `OrderArrived(order)` y `OrderRemoved(orderId, reason)` para los eventos del bus relevantes (`OrderAwaitingMerchant`, `OrderTimedOut`, `OrderCancelled`).

---

## 10. Sin listar pedidos en estados distintos a AwaitingMerchant

**Design**: implícito — el dashboard solo muestra pendientes, no historial.

**Backend**: `ListByMerchantOwnerAndStatusAsync(merchantOwnerUserId, OrderStatus.AwaitingMerchant, ...)` está hardcoded a un solo status (`MerchantOrderEndpoints.cs:25`). No hay `/orders/merchant?status=...` para otros estados.

**Workaround**: el portal no tiene historial. Después de accept/reject el pedido desaparece y el merchant pierde visibilidad.

**Backend ask**: `GET /api/v1/orders/merchant?status=...&page=...&pageSize=...` aceptando cualquier `OrderStatus` enum (Active, Completed, Cancelled, etc.).

---

## 11. Sin GET de detalle individual de pedido para el merchant

**Design**: implícito — las cards no tienen "Ver detalle".

**Backend**: `GET /api/v1/orders/{id}` está bajo `RequireCustomer` y el handler valida `CustomerUserId == JWT.sub` (`GetOrderQueryHandler.cs:22`). Un merchant con un pedido suyo recibe 404 incluso si pudiera llamarlo.

**Workaround**: el portal expone toda la información del pedido en la card (líneas, totales, nota, dirección). No hay pantalla de detalle separada.

**Backend ask**: relajar la policy a `RequireAuthorization()` y agregar `order.MerchantOwnerUserId == JWT.sub` como segundo ownership válido en `GetOrderQueryHandler`.

---

## 12. Sin "marcar preparado / listo para entrega"

**Design**: el footer de la pantalla de pedidos dice "Tú solo prepara el pedido" — confirmado.

**Backend**: no existe endpoint `POST /orders/{id}/mark-ready`. La saga avanza sola: `MerchantAccepted` → cobro → courier asignado → entrega.

**Workaround**: el portal documenta esto en el footer informativo. No hay UI para marcar preparado.

**Backend ask**: si el negocio quiere que el merchant marque "listo para que el courier venga a recoger", agregar `POST /api/v1/orders/{id}/mark-ready` que emita `OrderReadyForPickup` y que la saga consuma como señal explícita (hoy es implícito por timeout o por confirmación de stock).

---

## 13. Editor de polígono pendiente (frontend-only)

**Design**: el select "Tipo de zona" tiene dos opciones: Círculo y Polígono.

**Backend**: `AddServiceAreaRequest` soporta ambos tipos (`MerchantRequests.cs:21`). El editor de polígono es solo trabajo de FE.

**Workaround**: MVP solo Círculo. El select se oculta y se muestra "Círculo (radio fijo)" fijo. El editor de polígono (Leaflet polyline editable o leaflet-draw) queda como follow-up.

**Backend ask**: ninguno (el backend ya está listo).

---

## 14. Sin servicio de reverse-geocoding

**Design**: paso 4 del wizard tiene un input "Dirección de recogida".

**Backend**: no hay servicio de geocoding (ni Mapbox ni Nominatim ni Google integrado). El backend solo almacena lat/lng.

**Workaround**: el merchant escribe la dirección a mano (texto libre). Se guarda en `localStorage.rappix.merchant.pickup-address` solo para mostrársela a él. El backend recibe únicamente lat/lng.

**Backend ask**: si en el futuro se quiere autocompletar direcciones desde el lat/lng, integrar un proveedor (Mapbox geocoding API, OpenCage, etc.) en el portal o en un microservicio Rappix.

---

## Notas operativas

- **Idempotency-Key**: el cliente envía uno generado client-side (`newIdempotencyKey()`) en todo POST/PUT/DELETE. El gateway lo procesa via su middleware estándar.
- **JWT refresh**: el `apiFetch` cliente intercepta 401 y reintenta una vez con un refresh transparente. Si el refresh falla (token reuse detected, etc.), limpia el localStorage y propaga el 401 para que el portal redirija a `/login`.
- **El comercio en `Draft`**: el `Merchant` se crea automáticamente cuando alguien se registra con `accountType: "Merchant"` via el `UserRegisteredConsumer` del servicio Merchants. Como es async, el wizard tolera hasta 5 retries con 800ms de delay en su primera carga de `/merchants/me`.
- **Reordenar la zona Circle**: como no hay PUT en areas individuales, "editar la zona" en `/configuracion` borra todas las áreas Circle existentes y crea una nueva. Funciona bien para el MVP (un comercio típico tiene 1-2 zonas).
