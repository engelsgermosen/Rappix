#!/usr/bin/env bash
# Rappix - Flujo E2E de un pedido via gateway hasta Completed (bash + curl + jq).
#
# Usa los datos de tools/seed-smoke.sh (correlo antes al menos una vez; no se repite aqui).
# Cada ejecucion crea una cotizacion y un pedido nuevos; no deja estado a medias que impida
# re-ejecutarlo (si el courier ya tiene una asignacion activa de un pedido anterior, aborta).
#
# Transiciones (OrderStateMachine + Dispatch):
#   Submitted -> AwaitingMerchant            (saga: quote consumido + stock reservado)
#   -> AwaitingPayment -> AwaitingCourier    (merchant acepta; Payments autoriza el cobro)
#   -> InProgress                            (Dispatch asigna courier; Catalog confirma stock)
#   -> Completed                             (courier marca entregado; Payments captura)
# No existen estados de "preparacion" ni "recogida": la saga no los modela.
#
# Uso:  ./tools/e2e-order-flow.sh            (GATEWAY=http://localhost:5100 para otro puerto)
set -euo pipefail

GATEWAY=${GATEWAY:-http://localhost:5000}
PASSWORD='Sup3rSecret!'
DELIVERY_LAT=18.4861
DELIVERY_LNG=-69.9312
TIMEOUT_SECONDS=${TIMEOUT_SECONDS:-60}

step() { printf '\n=== %s ===\n' "$1"; }
ok()   { printf '  [OK] %s\n' "$1"; }
fail() { printf '  [FAIL] %s\n' "$1" >&2; exit 1; }

# req METHOD PATH [TOKEN] [JSON] [EXTRA_HEADER] -> imprime el body; falla si no es 2xx.
req() {
  local method=$1 path=$2 token=${3:-} body=${4:-} extra=${5:-}
  local args=(-sS -X "$method" "$GATEWAY$path" -H 'Content-Type: application/json' -w '\n%{http_code}')
  [[ -n $token ]] && args+=(-H "Authorization: Bearer $token")
  [[ -n $extra ]] && args+=(-H "$extra")
  [[ -n $body ]] && args+=(--data "$body")
  local out code
  out=$(curl "${args[@]}")
  code=${out##*$'\n'}
  out=${out%$'\n'*}
  if [[ $code != 2* ]]; then
    echo "HTTP $code $method $path: $out" >&2
    return 1
  fi
  printf '%s' "$out"
}

login() {
  req POST /api/v1/auth/login "" "$(jq -nc --arg e "$1" --arg p "$PASSWORD" '{identifier:$e, password:$p}')" \
    | jq -er '.accessToken // .token'
}

# wait_order_status ESTADO: sondea GET /orders/{id} (cliente) hasta el estado o timeout.
wait_order_status() {
  local expected=$1 status=""
  for ((i = 0; i < TIMEOUT_SECONDS; i++)); do
    status=$(req GET "/api/v1/orders/$ORDER_ID" "$CUSTOMER" | jq -r .status)
    if [[ $status == "$expected" ]]; then
      ok "Pedido en $expected"
      return 0
    fi
    case $status in Cancelled|Failed|NeedsReview) fail "Pedido en estado terminal $status esperando $expected" ;; esac
    sleep 1
  done
  fail "Timeout esperando $expected (estado actual: $status)"
}

step "1. Login (cliente, comercio, courier)"
CUSTOMER=$(login cliente@rappix.test);  ok "Cliente"
MERCHANT=$(login comercio@rappix.test); ok "Comercio"
COURIER=$(login courier@rappix.test);   ok "Courier"

step "2. Precondiciones (datos de tools/seed-smoke.sh)"
MERCHANT_ID=$(req GET /api/v1/merchants/me "$MERCHANT" | jq -er 'select(.status=="Active") | .id') \
  || fail "El comercio no esta Active: corre tools/seed-smoke.sh"
ITEM_ID=$(req GET /api/v1/catalog/me/items "$MERCHANT" | jq -er '[.items[] | select(.name=="Pizza Margarita")][0].id') \
  || fail "No existe el item Pizza Margarita: corre tools/seed-smoke.sh"
COURIER_ME=$(req GET /api/v1/couriers/me "$COURIER")
COURIER_STATUS=$(jq -r .status <<<"$COURIER_ME")
[[ $COURIER_STATUS == Busy ]] && fail "El courier ya esta Busy con otra asignacion (GET /api/v1/couriers/me/current-assignment)"
if [[ $COURIER_STATUS != Online ]]; then
  req POST /api/v1/couriers/me/location "$COURIER" "{\"latitude\":18.4862,\"longitude\":-69.9311}" >/dev/null
  req POST /api/v1/couriers/me/online "$COURIER" >/dev/null
fi
ok "Comercio Active, item $ITEM_ID, courier Online"

step "3. Cotizacion + pedido (cliente)"
QUOTE_ID=$(req POST /api/v1/pricing/quotes "$CUSTOMER" \
  "$(jq -nc --arg m "$MERCHANT_ID" --arg i "$ITEM_ID" '{merchantId:$m, vertical:"Food", distanceKm:3, tip:50, isFirstOrder:true, lines:[{itemId:$i, quantity:1, modifierTotal:0}]}')" \
  | jq -er .quoteId)
ok "Cotizacion $QUOTE_ID"
ORDER=$(req POST /api/v1/orders "$CUSTOMER" \
  "{\"quoteId\":\"$QUOTE_ID\",\"street\":\"Calle Test 123\",\"reference\":\"Apto 1\",\"latitude\":$DELIVERY_LAT,\"longitude\":$DELIVERY_LNG}" \
  "Idempotency-Key: e2e-$QUOTE_ID")
ORDER_ID=$(jq -er .orderId <<<"$ORDER")
ok "Pedido $ORDER_ID ($(jq -r .status <<<"$ORDER"))"
wait_order_status AwaitingMerchant

step "4. El comercio acepta"
req GET "/api/v1/orders/merchant/pending" "$MERCHANT" | jq -e --arg o "$ORDER_ID" '[(if type == "array" then . else .items end)[] | select(.orderId == $o or .id == $o)] | length > 0' >/dev/null \
  || fail "El pedido no aparece en /orders/merchant/pending"
req POST "/api/v1/orders/$ORDER_ID/accept" "$MERCHANT" >/dev/null
ok "Aceptado"
wait_order_status InProgress

step "5. Dispatch: asignacion del courier"
ASSIGNMENT=$(req GET /api/v1/couriers/me/current-assignment "$COURIER")
[[ $(jq -r .orderId <<<"$ASSIGNMENT") == "$ORDER_ID" ]] || fail "La asignacion activa no es de este pedido: $ASSIGNMENT"
ok "Asignacion $(jq -r .assignmentId <<<"$ASSIGNMENT") al courier"

step "6. Tracking: ubicacion del courier"
req POST /api/v1/couriers/me/location "$COURIER" '{"latitude":18.4855,"longitude":-69.9320}' >/dev/null
for ((i = 0; i < TIMEOUT_SECONDS; i++)); do
  TRACKING=$(req GET "/api/v1/tracking/orders/$ORDER_ID" "$CUSTOMER" 2>/dev/null || true)
  [[ $(jq -r '.lastLocation != null' <<<"${TRACKING:-null}" 2>/dev/null) == true ]] && break
  sleep 1
done
[[ $(jq -r '.lastLocation != null' <<<"${TRACKING:-null}") == true ]] || fail "Tracking sin ubicacion del courier: ${TRACKING:-sin respuesta}"
ok "Tracking: status $(jq -r .status <<<"$TRACKING"), ubicacion $(jq -c .lastLocation <<<"$TRACKING")"

step "7. El courier entrega"
req POST /api/v1/couriers/me/current-assignment/delivered "$COURIER" >/dev/null
ok "Entregado"
wait_order_status Completed
TRACKING=$(req GET "/api/v1/tracking/orders/$ORDER_ID" "$CUSTOMER")
ok "Tracking: status $(jq -r .status <<<"$TRACKING")"

printf '\nFLUJO COMPLETO: pedido %s en Completed\n' "$ORDER_ID"
