#!/usr/bin/env bash
# Rappix - Seed para smoke test de la saga de Orders (bash + curl + jq).
# Mismos pasos y datos que tools/seed-smoke.ps1, pero IDEMPOTENTE: cada paso consulta el estado
# actual y se salta si ya esta hecho (usuario registrado, comercio enviado/aprobado, zona de
# cobertura existente, categoria/item creados, courier Online). Solo la cotizacion se crea siempre
# (caduca a los 10 min).
#
# Uso:  ./tools/seed-smoke.sh
# Requisitos: stack arriba (docker compose up -d), jq, contenedor postgres 'rappix-postgres'.
set -euo pipefail

IDENTITY=http://localhost:5001
MERCHANTS=http://localhost:5002
CATALOG=http://localhost:5003
PRICING=http://localhost:5004
DISPATCH=http://localhost:5006
PG=rappix-postgres

CUSTOMER_EMAIL=cliente@rappix.test
MERCHANT_EMAIL=comercio@rappix.test
ADMIN_EMAIL=admin@rappix.test
COURIER_EMAIL=courier@rappix.test
PASSWORD='Sup3rSecret!'

# Datos del comercio (mismos que el .ps1).
LAT=18.4861
LNG=-69.9312
RADIUS=5000

step() { printf '\n=== %s ===\n' "$1"; }
ok()   { printf '  [OK] %s\n' "$1"; }
skip() { printf '  [--] %s\n' "$1"; }

# req METHOD URL [TOKEN] [JSON] -> imprime el body; falla si la respuesta no es 2xx.
req() {
  local method=$1 url=$2 token=${3:-} body=${4:-}
  local args=(-sS -X "$method" "$url" -H 'Content-Type: application/json' -w '\n%{http_code}')
  [[ -n $token ]] && args+=(-H "Authorization: Bearer $token")
  [[ -n $body ]] && args+=(--data "$body")
  local out code
  out=$(curl "${args[@]}")
  code=${out##*$'\n'}
  out=${out%$'\n'*}
  if [[ $code != 2* ]]; then
    echo "HTTP $code $method $url: $out" >&2
    return 1
  fi
  printf '%s' "$out"
}

psql_() { echo "$2" | docker exec -i "$PG" psql -U rappix -d "$1" -v ON_ERROR_STOP=1 >/dev/null; }

register() {
  local email=$1 account_type=${2:-} body code
  body=$(jq -nc --arg e "$email" --arg p "$PASSWORD" --arg t "$account_type" \
    '{email:$e, password:$p, firstName:"Test", lastName:"User"} + (if $t == "" then {} else {accountType:$t} end)')
  code=$(curl -sS -o /dev/null -w '%{http_code}' -X POST "$IDENTITY/api/v1/auth/register" \
    -H 'Content-Type: application/json' --data "$body")
  case $code in
    2*)  ok "Registrado $email" ;;
    409) skip "$email ya estaba registrado" ;;
    *)   echo "HTTP $code al registrar $email" >&2; return 1 ;;
  esac
}

login() {
  req POST "$IDENTITY/api/v1/auth/login" "" \
    "$(jq -nc --arg e "$1" --arg p "$PASSWORD" '{identifier:$e, password:$p}')" | jq -er '.accessToken // .token'
}

step "1. Registrar usuarios"
register "$CUSTOMER_EMAIL"
register "$MERCHANT_EMAIL" Merchant
register "$COURIER_EMAIL" Courier
register "$ADMIN_EMAIL"

step "2. Confirmar emails + elevar admin (SQL, idempotente)"
psql_ rappix_identity "UPDATE identity.users SET \"EmailConfirmed\"=true WHERE \"Email\" IN ('$CUSTOMER_EMAIL','$MERCHANT_EMAIL','$COURIER_EMAIL','$ADMIN_EMAIL');"
ok "Emails confirmados"
psql_ rappix_identity "UPDATE identity.users SET \"UserType\"='Admin' WHERE \"Email\"='$ADMIN_EMAIL';"
ok "Admin elevado"

step "3. Login"
CUSTOMER=$(login "$CUSTOMER_EMAIL"); ok "Token cliente"
MERCHANT=$(login "$MERCHANT_EMAIL"); ok "Token merchant"
COURIER=$(login "$COURIER_EMAIL");   ok "Token courier"
ADMIN=$(login "$ADMIN_EMAIL");       ok "Token admin"

step "4. Perfil del merchant"
# El consumer de UserRegistered crea el Draft de forma asincrona: se espera a que exista.
for _ in {1..10}; do
  ME=$(req GET "$MERCHANTS/api/v1/merchants/me" "$MERCHANT" 2>/dev/null) && break
  sleep 1
done
MERCHANT_ID=$(jq -er .id <<<"$ME")
STATUS=$(jq -r .status <<<"$ME")
echo "  merchantId = $MERCHANT_ID (status $STATUS)"

if [[ $STATUS == Draft ]]; then
  req PUT "$MERCHANTS/api/v1/merchants/me" "$MERCHANT" \
    '{"name":"Tienda Smoke","slug":"tienda-smoke","rnc":"131246803","verticalType":"Food"}' >/dev/null
  ok "Perfil"
  req PUT "$MERCHANTS/api/v1/merchants/me/operating-hours" "$MERCHANT" \
    '{"hours":[{"dayOfWeek":"Monday","opensAt":"00:00","closesAt":"23:59"}]}' >/dev/null
  ok "Horarios"
  # Fase 6: PickupLocation es obligatoria para enviar a aprobacion.
  req PUT "$MERCHANTS/api/v1/merchants/me/pickup-location" "$MERCHANT" \
    "{\"latitude\":$LAT,\"longitude\":$LNG}" >/dev/null
  ok "Pickup location"
else
  skip "Perfil, horarios y pickup (comercio ya en $STATUS)"
fi

HAS_AREA=$(jq --argjson lat $LAT --argjson lng $LNG --argjson r $RADIUS \
  '[.serviceAreas[]? | select(.type=="Circle" and .centerLatitude==$lat and .centerLongitude==$lng and .radiusMeters==$r)] | length > 0' <<<"$ME")
if [[ $HAS_AREA == true ]]; then
  skip "Zona de cobertura ya existe"
else
  req POST "$MERCHANTS/api/v1/merchants/me/service-areas" "$MERCHANT" \
    "{\"type\":\"Circle\",\"centerLatitude\":$LAT,\"centerLongitude\":$LNG,\"radiusMeters\":$RADIUS}" >/dev/null
  ok "Zona de cobertura"
fi

if [[ $STATUS == Draft ]]; then
  req POST "$MERCHANTS/api/v1/merchants/me/submit-for-approval" "$MERCHANT" >/dev/null
  ok "Enviado a aprobacion"
  STATUS=Pending
else
  skip "Envio a aprobacion (comercio ya en $STATUS)"
fi

step "5. Aprobar el merchant (admin)"
if [[ $STATUS == Pending ]]; then
  req POST "$MERCHANTS/api/v1/admin/merchants/$MERCHANT_ID/approve" "$ADMIN" >/dev/null
  ok "Merchant aprobado (Active)"
else
  skip "Aprobacion (comercio en $STATUS)"
fi

step "6. Categoria + item con stock"
# El catalogo lo crea un consumer de MerchantApproved: se espera a que exista.
for _ in {1..10}; do
  CAT_ME=$(req GET "$CATALOG/api/v1/catalog/me" "$MERCHANT" 2>/dev/null) && break
  sleep 1
done
CAT_ID=$(jq -r '[.categories[]? | select(.name=="Pizzas")][0].id // empty' <<<"$CAT_ME")
if [[ -n $CAT_ID ]]; then
  skip "Categoria Pizzas ya existe"
else
  CAT_ID=$(req POST "$CATALOG/api/v1/catalog/me/categories" "$MERCHANT" '{"name":"Pizzas","sortOrder":"1"}' | jq -r '.id // empty')
  if [[ -z $CAT_ID ]]; then
    CAT_ID=$(req GET "$CATALOG/api/v1/catalog/me" "$MERCHANT" | jq -er '[.categories[] | select(.name=="Pizzas")][0].id')
  fi
  ok "Categoria Pizzas"
fi
echo "  categoryId = $CAT_ID"

ITEM_ID=$(req GET "$CATALOG/api/v1/catalog/me/items" "$MERCHANT" | jq -r '[.items[]? | select(.name=="Pizza Margarita")][0].id // empty')
if [[ -n $ITEM_ID ]]; then
  skip "Item Pizza Margarita ya existe"
else
  ITEM_ID=$(req POST "$CATALOG/api/v1/catalog/me/items" "$MERCHANT" \
    "$(jq -nc --arg c "$CAT_ID" '{categoryId:$c, name:"Pizza Margarita", description:"Clasica", priceAmount:250.00, currency:"DOP", tracksInventory:true, initialStock:10, attributes:{}}')" \
    | jq -er .id)
  ok "Item creado, stock 10"
fi
echo "  itemId = $ITEM_ID"

step "7. Courier (Dispatch)"
# El CourierProfile lo crea un consumer de UserRegistered: se espera a que exista.
for _ in {1..10}; do
  COURIER_ME=$(req GET "$DISPATCH/api/v1/couriers/me" "$COURIER" 2>/dev/null) && break
  sleep 1
done
if [[ $(jq -r '.vehicle != null' <<<"$COURIER_ME") == true ]]; then
  skip "Vehiculo ya registrado"
else
  req PUT "$DISPATCH/api/v1/couriers/me/vehicle" "$COURIER" '{"vehicleType":"Moto","plate":"A1234","capacityKg":15}' >/dev/null
  ok "Vehiculo"
fi
if [[ $(jq -r .status <<<"$COURIER_ME") == Online ]]; then
  skip "Courier ya Online"
else
  # Debe reportar ubicacion antes de ir Online para entrar al Redis Geo.
  req POST "$DISPATCH/api/v1/couriers/me/location" "$COURIER" '{"latitude":18.4862,"longitude":-69.9311}' >/dev/null
  ok "Location"
  req POST "$DISPATCH/api/v1/couriers/me/online" "$COURIER" >/dev/null
  ok "Courier Online"
fi

step "8. Cotizacion (cliente; siempre nueva, caduca en 10 min)"
QUOTE=$(req POST "$PRICING/api/v1/pricing/quotes" "$CUSTOMER" \
  "$(jq -nc --arg m "$MERCHANT_ID" --arg i "$ITEM_ID" '{merchantId:$m, vertical:"Food", distanceKm:3, tip:50, isFirstOrder:true, lines:[{itemId:$i, quantity:2, modifierTotal:0}]}')")
QUOTE_ID=$(jq -r .quoteId <<<"$QUOTE")
ok "Cotizacion total = $(jq -r .breakdown.total <<<"$QUOTE")"

cat <<EOF

========================================================
 SETUP COMPLETO
========================================================
  MERCHANT_ID=$MERCHANT_ID
  ITEM_ID=$ITEM_ID
  QUOTE_ID=$QUOTE_ID

Crear el pedido (arranca la saga; observa Seq en http://localhost:5341):
  curl -X POST http://localhost:5005/api/v1/orders \\
    -H "Authorization: Bearer \$CUSTOMER" -H 'Content-Type: application/json' -H 'Idempotency-Key: smoke-\$(date +%s)' \\
    -d '{"quoteId":"$QUOTE_ID","street":"Calle Test 123","reference":"Apto 1","latitude":$LAT,"longitude":$LNG}'

Token cliente (CUSTOMER):
  $CUSTOMER
EOF
