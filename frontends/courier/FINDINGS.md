# Frontends/courier — FINDINGS

Catálogo de hallazgos al construir el portal Courier en Fase 14. Tres
categorías:

1. **Gaps del backend** que el portal trabajó alrededor.
2. **Features del diseño visibles pero no funcionales** (renderizadas con tag
   "FUTURO" para no perder fidelidad visual, pero sin handler).
3. **Notas operativas** (decisiones de implementación que no son gaps —
   contexto para quien venga después).

> **Backend de referencia**: Fase 13.6, ya en `main`. Ver
> [`frontends/COURIER_API_MAP.md`](../COURIER_API_MAP.md) para la lista
> completa de endpoints y los 10 gaps que el mapa identifica.

---

## 1. Gaps del backend

### 1.1 Pickup sin texto de dirección — solo coordenadas + nombre del comercio

`Merchants.PickupLocation` es `Point` PostGIS puro: lat/lng. El backend
**no modela** un `pickupAddressText` ni `pickupReference`. El courier no
tiene cómo saber la calle o número del comercio al que va — solo "Recoger en
**Tienda Smoke**, lat/lng".

**Cómo lo manejamos**: en `<DeliveryInfoPanel>` la card "Recoger en" muestra
`pickup.merchantName` en grande + "Solo ubicación en mapa" en cursiva debajo
+ los lat/lng a 5 decimales como fallback informativo. El marcador morado del
mapa es lo único que indica el punto exacto.

**Follow-up backend**: añadir `Merchant.PickupAddress` (opcional, ≤200) +
exponerlo en el gRPC + propagarlo al snapshot `AssignmentSnapshot.PickupAddress`
en Dispatch. Ya documentado en `docs/adr/0003-merchants-design.md` ("Follow-up
Fase 13.6 — texto de pickup address").

### 1.2 Nombre del cliente no expuesto — "Cliente #XXXXXXXX"

`CurrentAssignmentResponse` expone `customerUserId` como GUID plano y nada
más sobre el cliente. Decisión de privacidad — el courier solo necesita un
identificador humano breve para confirmar la entrega correcta.

**Cómo lo manejamos**: `formatCustomerShortId(customerUserId)` → "Cliente #" +
primeros 8 chars del GUID en uppercase. Patrón espejo del `shortId()` del
merchant. La card "Entregar en" lo muestra debajo de la dirección.

### 1.3 Sin push de nuevas asignaciones — es polling

No existe `/hubs/courier` ni equivalente. El courier descubre asignaciones
poleando `GET /me/current-assignment` cada 5s (Online) o 3s (Busy).

**Cómo lo manejamos**: `useCurrentAssignment(status)` usa
`refetchInterval` adaptativo + `refetchOnWindowFocus: true` (volver al tab
trae el estado real inmediato). No hay loading spinner pesado entre ticks —
el cambio sólo aparece cuando el snapshot llega.

**Follow-up backend**: hub SignalR `/hubs/courier` con `Subscribe(courierId)`
ownership-checked + push de `AssignmentReceived` / `AssignmentReleased`.
Sería un cambio aditivo significativo en Dispatch + Gateway.

### 1.4 Sin historial de entregas / earnings / ratings

No hay `GET /me/assignments` ni `GET /me/earnings` ni `GET /me/ratings` en
el backend (grep confirmado). Los datos existen parcialmente en
`CourierAssignment` con `ReleasedAtUtc` y `ReleaseReason`, pero no se
proyectan.

**Cómo lo manejamos**: el portal **no tiene** esas pantallas. El menú del
header solo expone "Mi vehículo", "Mi cuenta" y "Cerrar sesión". El diseño
aprobado tampoco las incluye, así que no hay deuda visual.

### 1.5 Sin push de razón al liberar la asignación

Si el cliente cancela mientras el courier está Busy, el endpoint
`/current-assignment` simplemente flippea de 200 → 204 en el siguiente
poll. No hay payload de "te liberaron porque X".

**Cómo lo manejamos**: cuando el poll transiciona de "tenía asignación" →
"ya no tiene" sin que el courier haya tocado "Entregué", asumimos un
release externo y no mostramos ningún toast adicional (es ruido — el
courier ve que la pantalla volvió a "Esperando pedido" y eso comunica el
estado). Mejorar requiere un campo `lastReleaseReason` en el response del
`/me` o un mini-evento en `/current-assignment`.

### 1.6 Sin endpoint para rechazar / devolver una asignación

El claim atómico de Dispatch es one-shot. No existe `POST
/me/current-assignment/decline`. El courier solo puede entregar o esperar.

**Cómo lo manejamos**: la pantalla activa NO muestra botón de rechazo. La
única salida es entregar (Gap #4 cerrado) o que el sistema libere
externamente.

---

## 2. Features renderizadas pero no funcionales (FUTURO)

### 2.1 Botón "Recogí en el comercio" (estado 3 → 3b del diseño)

**Renderizado**: tal como el diseño, con styling de botón outline grande,
icono `PackageCheck`, badge ámbar "FUTURO" en la esquina, tooltip con la
razón al hover.

**No funcional**: sin `onClick`, sin estado local, sin guardar nada — no
hay handler. Click directo no hace nada (el `disabled` lo bloquea
visualmente, pero aunque se quitara, no pasa nada).

**Por qué**: el flujo de v1 es "tengo asignación → entregué". Los estados
intermedios `PickedUp` / `InTransit` son el Gap #3 del COURIER_API_MAP
([§D.5.3](../COURIER_API_MAP.md)) — están explícitamente listados como
follow-up de ADR-0008 en `Tracking.TrackingStatus`. Inventar estado local
en el front sería deuda — al mover lo real al backend habría que arrancar
una refactorización.

**Cuando exista**: el flujo se desbloquea agregando un PUT/POST courier
para marcar pickup + status intermedio en `Tracking.TrackingStatus` +
eventos `OrderPickedUp` / `OrderInTransit` consumidos por Tracking. El
front solo necesita habilitar el `onClick` y mover una flag local
"recogido" para grayear ese botón después.

### 2.2 Ruta trazada en el mapa (polyline)

**Renderizado**: 2 marcadores (pickup morado + delivery coral) con
zoom-to-fit a ambos.

**No funcional**: sin polyline / sin ruta trazada entre los dos puntos.
Aprobado por el equipo en el plan original.

**Por qué**: la Google Routes API es **paid** ($5 USD por 1000 requests).
El zoom-to-fit + los 2 pines comunican el viaje suficientemente para v1.

**Cuando exista**: agregar Routes API + render de polyline cuando existan
los estados intermedios `PickedUp` / `InTransit` que justifiquen la
diferenciación visual (segmento "courier → comercio" vs "comercio →
cliente"). Hoy con un solo estado activo el polyline aporta poco.

---

## 3. Notas operativas

### 3.1 Map ID estilizado — opcional, default de Google si vacío

`NEXT_PUBLIC_GOOGLE_MAPS_MAP_ID` es opcional. Sin él, `@vis.gl/react-google-maps`
cae al estilo default de Google (raster en algunos browsers, vector en
otros). Con un Map ID configurado en Google Cloud Console (preset Light /
Minimal) la experiencia es mejor — vector maps + estilo subtle (baja
saturación) que destaca los pines del brand.

Documentado en `README.md` + `.env.local.example`. El portal no falla si
falta.

### 3.2 Resize del mapa con vector maps + `useMap`

El bug clásico de Google Maps en contenedor que cambia de tamaño
(typically "mapa gris" o pines en posición incorrecta) **no se arregla
con `google.maps.event.trigger(map, "resize")`** en vector maps — ese es
el patrón viejo de raster maps. Con `@vis.gl/react-google-maps` + mapId,
el `ResizeObserver` interno del componente se encarga, **siempre que el
contenedor tenga altura explícita ANTES del mount**.

Por eso el contenedor del mapa en `<ActiveDelivery>` lleva `h-[40vh]`
(móvil) o `h-full` con padre `h-[calc(100vh-72px)]` (desktop). Si en el
futuro el layout cambia, mantener esa garantía o ver pines en (0,0) /
mapa colapsado.

Para forzar fit a los markers usamos `useMap()` + `map.fitBounds(bounds,
padding)` en un `useEffect` que escucha cambios en pickup/delivery.
Primer fit con padding 120px (no quedar muy zoom-in), siguientes con 80px.

### 3.3 Idempotency-Key — en mutations, **no** en `/me/location`

Todos los `POST/PUT/DELETE` mutantes (vehicle, online, offline, delivered,
register, updateProfile, changePassword) mandan `Idempotency-Key` (UUID
generado en `newIdempotencyKey`).

`POST /me/location` **no** lo manda — los heartbeats son replay-safe por
diseño (Tracking dedupes por timestamp en `ApplyLocation`). Agregarlo
sería ruido en Redis del middleware sin ganancia.

### 3.4 401 transparente + refresh coalescente

`apiFetch` cubre el flujo: si una request devuelve 401 y no estábamos
reintentando, intenta `POST /auth/refresh` (una sola vez, coalescente
entre N requests concurrentes que también fallaron) y reintenta. Si el
refresh falla → limpia el token y propaga 401 al caller. El caller típico
es la query → ProblemDetails parsed → `describeError` → toast →
`RequireAuth` redirige.

### 3.5 Permisos de geolocalización — degradación graceful

`useLocationHeartbeat` lanza `navigator.geolocation.watchPosition`. Si el
usuario deniega permisos, mostramos UN solo toast persistente (10s) con
"Activa la ubicación..." y seguimos. El courier puede estar Online sin
location updates — Dispatch lo descartará del GEOSEARCH al siguiente
claim porque su `LastLocation` está vieja (o nunca seteada).

**No bloqueamos el toggle Online** — la decisión es del usuario. Si más
adelante se quiere forzar (no se puede ir Online sin permitir
geolocalización) basta con verificar `navigator.permissions.query` en el
handler del toggle.

### 3.6 Tres portales, tres localStorage keys distintas

`tokenStore` usa `rappix.courier.auth` (vs `rappix.customer.auth`
y `rappix.merchant.auth`). Tres portales pueden coexistir en el mismo
dominio (`localhost`) sin pisarse. La hidratación de cada Zustand
purga tokens que no son del rol del portal (defensa en profundidad).

### 3.7 Sin tests automatizados — verificación manual

Esta fase no incluye Jest / Playwright. La verificación es manual: el
proceso E2E está documentado en el plan original y se ejecuta tras el
último commit. La suite de tests automatizados es follow-up.

---

## Cómo agregar un nuevo hallazgo

1. Identificar si es **Gap del backend** (1), **feature visible no
   funcional** (2) o **nota operativa** (3).
2. Agregar sub-sección numerada con título acción ("Sin X — solucion Y").
3. Incluir: qué hace el backend hoy, cómo lo manejamos en el FE, qué
   habría que hacer para cerrarlo.
4. Si toca un código específico, linkearlo a `path:line`.
