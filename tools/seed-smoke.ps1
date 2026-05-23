<#
  Rappix - Seed para Smoke Test de la Saga de Orders
  ---------------------------------------------------
  Automatiza TODO el setup tedioso:
    - Registra cliente, merchant y admin
    - Confirma sus emails via SQL (no hay SendGrid en dev)
    - Hace al admin Admin via SQL
    - Loguea los 3 y captura tokens
    - Completa + aprueba el merchant
    - Crea categoria + item con stock
    - Crea una cotizacion fresca

  Al final imprime los tokens + IDs para que hagas A MANO la parte
  interesante (crear pedido -> accept -> mark-delivered) observando Seq.

  Uso:
    1. Asegurate de que los 5 servicios + infra esten arriba:
         docker compose up -d --build identity-api merchants-api catalog-api pricing-api orders-api
    2. Corre:  ./tools/seed-smoke.ps1
    3. Sigue las instrucciones que imprime al final.

  Requisitos: PowerShell 5+ (Invoke-RestMethod), contenedor postgres llamado 'rappix-postgres'.
  Pensado para un stack recien levantado (docker compose down -v + up).
#>

$ErrorActionPreference = "Stop"

# ----- Config -----
$IDENTITY  = "http://localhost:5001"
$MERCHANTS = "http://localhost:5002"
$CATALOG   = "http://localhost:5003"
$PRICING   = "http://localhost:5004"
$ORDERS    = "http://localhost:5005"
$DISPATCH  = "http://localhost:5006"
$PG        = "rappix-postgres"   # nombre del contenedor postgres

$CustomerEmail = "cliente@rappix.test"
$MerchantEmail = "comercio@rappix.test"
$AdminEmail    = "admin@rappix.test"
$CourierEmail  = "courier@rappix.test"
$Pwd           = "Sup3rSecret!"

# ----- Helpers -----
function Step($msg) { Write-Host "`n=== $msg ===" -ForegroundColor Cyan }
function Ok($msg)   { Write-Host "  [OK] $msg" -ForegroundColor Green }
function Info($msg) { Write-Host "  $msg" -ForegroundColor Gray }

function PostJson($url, $body, $token) {
    $headers = @{ "Content-Type" = "application/json" }
    if ($token) { $headers["Authorization"] = "Bearer $token" }
    $json = $body | ConvertTo-Json -Depth 10 -Compress
    return Invoke-RestMethod -Method Post -Uri $url -Headers $headers -Body $json
}
function PutJson($url, $body, $token) {
    $headers = @{ "Content-Type" = "application/json" }
    if ($token) { $headers["Authorization"] = "Bearer $token" }
    $json = $body | ConvertTo-Json -Depth 10 -Compress
    return Invoke-RestMethod -Method Put -Uri $url -Headers $headers -Body $json
}
function PostEmpty($url, $token) {
    $headers = @{ "Content-Type" = "application/json" }
    if ($token) { $headers["Authorization"] = "Bearer $token" }
    return Invoke-RestMethod -Method Post -Uri $url -Headers $headers
}
function GetJson($url, $token) {
    $headers = @{}
    if ($token) { $headers["Authorization"] = "Bearer $token" }
    return Invoke-RestMethod -Method Get -Uri $url -Headers $headers
}
function Psql($db, $sql) {
    # El SQL se pasa por STDIN (no como -c) para preservar las comillas dobles de los identificadores
    # PascalCase ("Email", "EmailConfirmed", "UserType"). Con -c, Windows PowerShell 5.1 descarta esas
    # comillas al invocar docker.exe y psql falla con 'column "email" does not exist'.
    $sql | docker exec -i $PG psql -U rappix -d $db -v ON_ERROR_STOP=1 | Out-Null
}

# Intenta registrar; si ya existe (409/400) lo ignora para que el script sea re-ejecutable
function TryRegister($email, $extra) {
    $body = @{ email = $email; password = $Pwd; firstName = "Test"; lastName = "User" }
    foreach ($k in $extra.Keys) { $body[$k] = $extra[$k] }
    try {
        PostJson "$IDENTITY/api/v1/auth/register" $body $null | Out-Null
        Ok "Registrado $email"
    } catch {
        Info "($email ya existia o registro no-2xx; continuo)"
    }
}

function Login($email) {
    $resp = PostJson "$IDENTITY/api/v1/auth/login" @{ identifier = $email; password = $Pwd } $null
    # El token puede venir como accessToken o token; cubrimos ambos
    if ($resp.accessToken) { return $resp.accessToken }
    if ($resp.token)       { return $resp.token }
    throw "No encontre el token en la respuesta de login de $email. Respuesta: $($resp | ConvertTo-Json -Compress)"
}

# =====================================================================
Step "1. Registrar usuarios"
TryRegister $CustomerEmail @{}                                  # Customer por defecto
TryRegister $MerchantEmail @{ accountType = "Merchant" }        # Merchant
TryRegister $CourierEmail  @{ accountType = "Courier" }         # Courier (Fase 6)
TryRegister $AdminEmail    @{}                                  # luego lo elevamos a Admin

Step "2. Confirmar emails + elevar admin (SQL)"
Psql "rappix_identity" "UPDATE identity.users SET ""EmailConfirmed""=true WHERE ""Email"" IN ('$CustomerEmail','$MerchantEmail','$CourierEmail','$AdminEmail');"
Ok "Emails confirmados"
Psql "rappix_identity" "UPDATE identity.users SET ""UserType""='Admin' WHERE ""Email""='$AdminEmail';"
Ok "Admin elevado"

Step "3. Login (tokens frescos, post-confirmacion)"
$CUSTOMER = Login $CustomerEmail; Ok "Token cliente"
$MERCHANT = Login $MerchantEmail; Ok "Token merchant"
$COURIER  = Login $CourierEmail;  Ok "Token courier"
$ADMIN    = Login $AdminEmail;    Ok "Token admin"

Step "4. Completar perfil del merchant"
# El consumer de UserRegistered ya creo el Draft; lo completamos.
# Pequena espera por si el consumer aun no proceso el evento
Start-Sleep -Seconds 2
PutJson "$MERCHANTS/api/v1/merchants/me" @{
    name         = "Tienda Smoke"
    slug         = "tienda-smoke"
    rnc          = "131246803"
    verticalType = "Food"
} $MERCHANT | Out-Null
Ok "Perfil actualizado"

PostJson "$MERCHANTS/api/v1/merchants/me/service-areas" @{
    type            = "Circle"
    centerLatitude  = 18.4861
    centerLongitude = -69.9312
    radiusMeters    = 5000
} $MERCHANT | Out-Null
Ok "Zona de cobertura"

PutJson "$MERCHANTS/api/v1/merchants/me/operating-hours" @{
    hours = @(@{ dayOfWeek = "Monday"; opensAt = "00:00"; closesAt = "23:59" })
} $MERCHANT | Out-Null
Ok "Horarios"

# Fase 6: PickupLocation es obligatoria para enviar a aprobacion. La saga de Orders la
# resuelve via gRPC y la propaga en CourierRequested para que Dispatch haga el matching.
PutJson "$MERCHANTS/api/v1/merchants/me/pickup-location" @{
    latitude  = 18.4861
    longitude = -69.9312
} $MERCHANT | Out-Null
Ok "Pickup location del merchant"

PostEmpty "$MERCHANTS/api/v1/merchants/me/submit-for-approval" $MERCHANT | Out-Null
Ok "Enviado a aprobacion"

$me = GetJson "$MERCHANTS/api/v1/merchants/me" $MERCHANT
$MERCHANT_ID = $me.id
Info "merchantId = $MERCHANT_ID"

Step "5. Aprobar el merchant (admin)"
PostEmpty "$MERCHANTS/api/v1/admin/merchants/$MERCHANT_ID/approve" $ADMIN | Out-Null
Ok "Merchant aprobado (Active)"
# Espera a que el consumer de Catalog cree el catalogo del merchant
Start-Sleep -Seconds 2

Step "6. Crear categoria + item con stock"
$cat = PostJson "$CATALOG/api/v1/catalog/me/categories" @{
    name      = "Pizzas"
    sortOrder = "1"
} $MERCHANT
# La respuesta puede no traer id; si no, lo buscamos
$CAT_ID = $cat.id
if (-not $CAT_ID) {
    $cats = GetJson "$CATALOG/api/v1/catalog/me/categories" $MERCHANT
    $CAT_ID = ($cats | Select-Object -First 1).id
    if (-not $CAT_ID -and $cats.items) { $CAT_ID = ($cats.items | Select-Object -First 1).id }
}
Info "categoryId = $CAT_ID"

$item = PostJson "$CATALOG/api/v1/catalog/me/items" @{
    categoryId      = $CAT_ID
    name            = "Pizza Margarita"
    description     = "Clasica"
    priceAmount     = 250.00
    currency        = "DOP"
    tracksInventory = $true
    initialStock    = 10
    attributes      = @{}
} $MERCHANT
$ITEM_ID = $item.id
Ok "Item creado, stock 10"
Info "itemId = $ITEM_ID"

Step "7. Configurar al courier (Fase 6: Dispatch)"
# El UserRegisteredConsumer de Dispatch ya creo el CourierProfile en Offline al registrar.
# Espera por si el evento aun no llego.
Start-Sleep -Seconds 2
PutJson "$DISPATCH/api/v1/couriers/me/vehicle" @{
    vehicleType = "Moto"
    plate       = "A1234"
    capacityKg  = 15
} $COURIER | Out-Null
Ok "Vehiculo del courier"

# El courier debe reportar location antes de ir Online para entrar al Redis Geo.
PostJson "$DISPATCH/api/v1/couriers/me/location" @{
    latitude  = 18.4862
    longitude = -69.9311
} $COURIER | Out-Null
Ok "Location del courier reportada"

PostEmpty "$DISPATCH/api/v1/couriers/me/online" $COURIER | Out-Null
Ok "Courier Online (en Redis Geo: dispatch:couriers:geo)"

Step "8. Crear cotizacion (cliente)"
$quote = PostJson "$PRICING/api/v1/pricing/quotes" @{
    merchantId  = $MERCHANT_ID
    vertical    = "Food"
    distanceKm  = 3
    tip         = 50
    isFirstOrder = $true
    lines       = @(@{ itemId = $ITEM_ID; quantity = 2; modifierTotal = 0 })
} $CUSTOMER
$QUOTE_ID = $quote.quoteId
Ok "Cotizacion creada, total = $($quote.breakdown.total)"
Info "quoteId = $QUOTE_ID (expira en 10 min)"

# =====================================================================
Write-Host "`n========================================================" -ForegroundColor Yellow
Write-Host " SETUP COMPLETO - ahora la parte que observas en Seq" -ForegroundColor Yellow
Write-Host "========================================================" -ForegroundColor Yellow
Write-Host ""
Write-Host "Variables listas (copialas si abres otra terminal):" -ForegroundColor White
Write-Host "  `$CUSTOMER    = '$CUSTOMER'"
Write-Host "  `$MERCHANT    = '$MERCHANT'"
Write-Host "  `$COURIER     = '$COURIER'"
Write-Host "  `$ADMIN       = '$ADMIN'"
Write-Host "  `$MERCHANT_ID = '$MERCHANT_ID'"
Write-Host "  `$ITEM_ID     = '$ITEM_ID'"
Write-Host "  `$QUOTE_ID    = '$QUOTE_ID'"
Write-Host ""
Write-Host "Abre Seq (http://localhost:5341, filtro Service='orders' o 'dispatch') y luego:" -ForegroundColor White
Write-Host ""
Write-Host "  # 1) Crear pedido (arranca la saga)" -ForegroundColor Green
Write-Host @"
  `$order = Invoke-RestMethod -Method Post -Uri http://localhost:5005/api/v1/orders ``
    -Headers @{ Authorization = "Bearer `$CUSTOMER"; "Content-Type"="application/json"; "Idempotency-Key"="smoke-001" } ``
    -Body (@{ quoteId = "`$QUOTE_ID"; street="Calle Test 123"; reference="Apto 1"; latitude=18.4861; longitude=-69.9312 } | ConvertTo-Json)
  `$ORDER_ID = `$order.orderId
  `$ORDER_ID
"@
Write-Host ""
Write-Host "  # En Seq deberias ver: ValidatingQuote -> ReservingStock -> AwaitingMerchant" -ForegroundColor Gray
Write-Host ""
Write-Host "  # 2) Merchant acepta" -ForegroundColor Green
Write-Host @"
  Invoke-RestMethod -Method Post -Uri "http://localhost:5005/api/v1/orders/`$ORDER_ID/accept" ``
    -Headers @{ Authorization = "Bearer `$MERCHANT" }
"@
Write-Host ""
Write-Host "  # En Seq Orders: AwaitingPayment -> AwaitingCourier" -ForegroundColor Gray
Write-Host "  # En Seq Dispatch: CourierRequested consumido -> GEOSEARCH -> claim -> CourierAssigned" -ForegroundColor Gray
Write-Host "  # En Seq Orders: AwaitingCourier -> Committing -> InProgress" -ForegroundColor Gray
Write-Host ""
Write-Host "  # 3) Verificar la asignacion (Fase 6 Dispatch)" -ForegroundColor Green
Write-Host @"
  Invoke-RestMethod -Method Get -Uri "http://localhost:5006/api/v1/couriers/me/current-assignment" ``
    -Headers @{ Authorization = "Bearer `$COURIER" }
  # Debe devolver { assignmentId, orderId=`$ORDER_ID, assignedAtUtc }.
"@
Write-Host ""
Write-Host "  # 4) Marcar entregado" -ForegroundColor Green
Write-Host @"
  Invoke-RestMethod -Method Post -Uri "http://localhost:5005/api/v1/orders/`$ORDER_ID/mark-delivered" ``
    -Headers @{ Authorization = "Bearer `$MERCHANT" }
"@
Write-Host ""
Write-Host "  # 5) Verificar estado final = Completed + courier liberado (Online de nuevo)" -ForegroundColor Green
Write-Host @"
  Invoke-RestMethod -Method Get -Uri "http://localhost:5005/api/v1/orders/`$ORDER_ID" ``
    -Headers @{ Authorization = "Bearer `$CUSTOMER" } | Select-Object status, completedAtUtc
  Invoke-RestMethod -Method Get -Uri "http://localhost:5006/api/v1/couriers/me" ``
    -Headers @{ Authorization = "Bearer `$COURIER" } | Select-Object status
  # status courier debe ser 'Online' (Dispatch lo libero al consumir OrderDelivered).
"@
Write-Host ""
Write-Host "  # 6) Verificar stock bajo de 10 a 8" -ForegroundColor Green
Write-Host @"
  Invoke-RestMethod -Method Get -Uri "http://localhost:5003/api/v1/catalog/me/items/`$ITEM_ID" ``
    -Headers @{ Authorization = "Bearer `$MERCHANT" }
"@
Write-Host ""
Write-Host "Tip: estas variables solo viven en ESTA sesion de PowerShell." -ForegroundColor DarkGray
Write-Host "Si corriste el script con ./tools/seed-smoke.ps1, las vars internas no persisten;" -ForegroundColor DarkGray
Write-Host "usa los valores impresos arriba, o ejecuta el script con dot-sourcing:  . ./tools/seed-smoke.ps1" -ForegroundColor DarkGray
