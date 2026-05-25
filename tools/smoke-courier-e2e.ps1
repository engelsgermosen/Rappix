#requires -Version 5.1

<#
.SYNOPSIS
  Smoke E2E del flujo courier (Fase 13.6).

.DESCRIPTION
  Verifica que:
    (a) El enriquecimiento del Gap #1 funciona: GET /api/v1/couriers/me/current-assignment
        responde con direcciones reales de pickup + entrega + lineas + total.
    (b) El nuevo endpoint del Gap #4 funciona: POST /me/current-assignment/delivered libera
        al courier, mueve el pedido a Completed y respeta el ownership.

  Reusa tools/seed-smoke.ps1 para setup de usuarios + merchant + item + cotizacion fresca.
  Imprime cada assert individualmente - no dice "smoke paso" sin mostrar el resultado.

.REQUIREMENTS
  - docker compose down -v && docker compose up -d   (DB limpia)
  - Los 10 servicios + gateway corriendo en background
  - PowerShell 5.1+
#>

$ErrorActionPreference = "Stop"

# Endpoints (mismos puertos que seed-smoke.ps1, sin gateway - el smoke golpea cada servicio directo
# para diagnostico mas claro; el portal real iria por http://localhost:5000 via gateway).
$IDENTITY  = "http://localhost:5001"
$MERCHANTS = "http://localhost:5002"
$ORDERS    = "http://localhost:5005"
$DISPATCH  = "http://localhost:5006"

# Helpers comunes (copiados del seed-smoke para no acoplar este smoke al import de seed).
function Step  ($msg) { Write-Host "`n=== $msg ===" -ForegroundColor Cyan }
function Ok    ($msg) { Write-Host "  [OK] $msg" -ForegroundColor Green }
function Fail  ($msg) { Write-Host "  [FAIL] $msg" -ForegroundColor Red; throw $msg }
function Info  ($msg) { Write-Host "  $msg" -ForegroundColor Gray }
function Assert($cond, $label) {
    if ($cond) { Write-Host "  [ASSERT OK] $label" -ForegroundColor Green }
    else       { Write-Host "  [ASSERT FAIL] $label" -ForegroundColor Red; throw "Assert fallido: $label" }
}

# REST helpers que tambien capturan el status code para asserts negativos.
function PostJson($url, $body, $token) {
    $headers = @{ "Content-Type" = "application/json" }
    if ($token) { $headers["Authorization"] = "Bearer $token" }
    $json = $body | ConvertTo-Json -Depth 10 -Compress
    return Invoke-RestMethod -Method Post -Uri $url -Headers $headers -Body $json
}
function PostEmpty($url, $token) {
    $headers = @{}
    if ($token) { $headers["Authorization"] = "Bearer $token" }
    return Invoke-WebRequest -Method Post -Uri $url -Headers $headers -UseBasicParsing
}
function GetJson($url, $token) {
    $headers = @{}
    if ($token) { $headers["Authorization"] = "Bearer $token" }
    return Invoke-RestMethod -Method Get -Uri $url -Headers $headers
}
function GetRaw($url, $token) {
    # Devuelve el WebResponse (incluye StatusCode) para chequear 204 vs 200.
    $headers = @{}
    if ($token) { $headers["Authorization"] = "Bearer $token" }
    return Invoke-WebRequest -Method Get -Uri $url -Headers $headers -UseBasicParsing
}
function ExpectStatusCode($url, $method, $token, $expected, $label) {
    # Catch generico: PS 5.1 (System.Net.WebException) y PS 7+ (Microsoft.PowerShell.Commands.HttpResponseException)
    # exponen ambos $_.Exception.Response.StatusCode con el HttpStatusCode enum, asi que el dispatch
    # dinamico cubre los dos sin que el parser tenga que resolver tipos especificos en parse-time.
    $headers = @{}
    if ($token) { $headers["Authorization"] = "Bearer $token" }
    $code = $null
    try {
        $resp = Invoke-WebRequest -Method $method -Uri $url -Headers $headers -UseBasicParsing -ErrorAction Stop
        $code = [int]$resp.StatusCode
    } catch {
        if ($_.Exception.Response -and $_.Exception.Response.StatusCode) {
            $code = [int]$_.Exception.Response.StatusCode
        } else {
            Write-Host "  [ASSERT FAIL] $label (excepcion sin Response.StatusCode: $($_.Exception.Message))" -ForegroundColor Red
            throw
        }
    }
    if ($code -eq $expected) {
        Write-Host "  [ASSERT OK] $label (HTTP $code)" -ForegroundColor Green
    } else {
        Write-Host "  [ASSERT FAIL] $label (esperado HTTP $expected, recibido $code)" -ForegroundColor Red
        throw "status code mismatch"
    }
}

# 1) Seed (incluye registro de courier@rappix.test + vehiculo + Online + quote fresco).
Step "1. Seed (delegando a seed-smoke.ps1)"
. "$PSScriptRoot/seed-smoke.ps1" *> $null
Ok "Tokens y IDs listos"
Info "CUSTOMER    = $CUSTOMER"
Info "MERCHANT    = $MERCHANT"
Info "COURIER     = $COURIER"
Info "MERCHANT_ID = $MERCHANT_ID"
Info "ITEM_ID     = $ITEM_ID"
Info "QUOTE_ID    = $QUOTE_ID"

# El smoke necesita un segundo courier para uno de los negativos de seguridad. Lo registramos
# ad-hoc (idempotente: si ya existe, login).
Step "2. Registrar segundo courier para el negativo de seguridad"
$Pwd2 = "Sup3rSecret!"
$Courier2Email = "courier2@rappix.test"
try {
    PostJson "$IDENTITY/api/v1/auth/register" @{
        email = $Courier2Email; password = $Pwd2; firstName = "Courier2"; lastName = "Test"
        accountType = "Courier"
    } $null | Out-Null
    Ok "Registrado $Courier2Email"
    # Confirmar email en BD (replica de seed-smoke).
    "UPDATE identity.users SET ""EmailConfirmed""=true WHERE ""Email""='$Courier2Email';" |
        docker exec -i rappix-postgres psql -U rappix -d rappix_identity -v ON_ERROR_STOP=1 | Out-Null
    Ok "Email confirmado"
} catch {
    Info "($Courier2Email ya existia; continuo)"
}
$resp2 = PostJson "$IDENTITY/api/v1/auth/login" @{ identifier = $Courier2Email; password = $Pwd2 } $null
$COURIER2 = $resp2.accessToken
Ok "Token COURIER2"

# 3) Crear pedido con direccion + referencia (los campos que viajan al snapshot del courier).
Step "3. Customer crea pedido (con direccion + referencia)"
$DELIVERY_STREET    = "Av. 27 de Febrero 100"
$DELIVERY_REFERENCE = "Edif Azul, apto 3B"
$order = PostJson "$ORDERS/api/v1/orders" @{
    quoteId   = $QUOTE_ID
    street    = $DELIVERY_STREET
    reference = $DELIVERY_REFERENCE
    latitude  = 18.4861
    longitude = -69.9312
} $CUSTOMER
$ORDER_ID = $order.orderId
Ok "ORDER_ID = $ORDER_ID (status=$($order.status))"

# Pausa entre create y accept para evitar la carrera del outbox: el POST /orders escribe el outbox
# y MassTransit lo despacha asincronamente. Si el accept llega antes que la saga reciba
# OrderSubmittedIntegrationEvent, MerchantAccepted cae en OnUnhandledEvent.Ignore() (la saga aun
# no existe). 5s da margen al outbox (~500ms tipico) + ValidatingQuote + ReservingStock.
Info "Esperando 5s para que la saga arranque (outbox -> OrderSubmitted -> AwaitingMerchant)..."
Start-Sleep -Seconds 5

# 4) Merchant accept.
Step "4. Merchant accept"
Invoke-RestMethod -Method Post -Uri "$ORDERS/api/v1/orders/$ORDER_ID/accept" -Headers @{ Authorization = "Bearer $MERCHANT" } | Out-Null
Ok "Accept enviado"

# La saga avanza: AwaitingMerchant -> AwaitingPayment -> (payment simulado succ) -> AwaitingCourier
# -> CourierRequested -> Dispatch claim atomico -> CourierAssigned -> Order InProgress. Damos
# tiempo a la cadena completa.
Info "Esperando 10s para que la saga + dispatch + claim completen..."
Start-Sleep -Seconds 10

# 5) [ASSERTS DEL PASO 8 DEL PLAN] El courier ve su asignacion enriquecida.
Step "5. ASSERTS - el courier ve direcciones, comercio, lineas, total (Gap #1)"
$assignment = GetJson "$DISPATCH/api/v1/couriers/me/current-assignment" $COURIER
Write-Host "  --- Response JSON literal ---" -ForegroundColor Yellow
$assignment | ConvertTo-Json -Depth 5
Write-Host "  -----------------------------" -ForegroundColor Yellow

Assert ($assignment.orderId -eq $ORDER_ID) "response.orderId == orderId del paso 3 ($ORDER_ID)"
Assert ($assignment.pickup.merchantName -eq "Tienda Smoke") "response.pickup.merchantName == 'Tienda Smoke' (nombre del merchant seed)"
Assert ($assignment.pickup.latitude -ne 0)  "response.pickup.latitude  no es 0 (es $($assignment.pickup.latitude))"
Assert ($assignment.pickup.longitude -ne 0) "response.pickup.longitude no es 0 (es $($assignment.pickup.longitude))"
Assert ($assignment.delivery.street -eq $DELIVERY_STREET) "response.delivery.street == '$DELIVERY_STREET'"
Assert ($assignment.delivery.reference -eq $DELIVERY_REFERENCE) "response.delivery.reference == '$DELIVERY_REFERENCE'"
Assert ($assignment.delivery.latitude  -ne 0) "response.delivery.latitude  no es 0"
Assert ($assignment.delivery.longitude -ne 0) "response.delivery.longitude no es 0"
Assert ($assignment.lines.Count -ge 1) "response.lines.Count >= 1 (es $($assignment.lines.Count))"
Assert (-not [string]::IsNullOrWhiteSpace($assignment.lines[0].itemName)) "response.lines[0].itemName no es vacio ('$($assignment.lines[0].itemName)')"
Assert ($assignment.lines[0].quantity -gt 0) "response.lines[0].quantity > 0 ($($assignment.lines[0].quantity))"
Assert ($assignment.orderTotal -gt 0) "response.orderTotal > 0 ($($assignment.orderTotal))"
Assert ($assignment.orderCurrency -eq "DOP") "response.orderCurrency == 'DOP' ($($assignment.orderCurrency))"
# El customer del JWT del seed debe ser el customerUserId del snapshot.
$customerMe = GetJson "$IDENTITY/api/v1/auth/me" $CUSTOMER
Assert ($assignment.customerUserId -eq $customerMe.id) "response.customerUserId == GUID del cliente ('$($customerMe.id)')"

# 6) Courier reporta movimiento (no es estrictamente parte de los asserts pero verifica que el
#    POST /me/location sigue funcionando bajo la nueva firma del evento).
Step "6. Courier reporta location (imita movimiento)"
PostJson "$DISPATCH/api/v1/couriers/me/location" @{ latitude = 18.4670; longitude = -69.9200 } $COURIER | Out-Null
Ok "Location actualizada"

# 7) [GAP #4] Courier marca entregado por el endpoint NUEVO.
Step "7. Courier marca entregado por POST /me/current-assignment/delivered (Gap #4)"
$deliveredUrl = "$DISPATCH/api/v1/couriers/me/current-assignment/delivered"
$deliveredCode = $null
$deliveredBody = ""
try {
    $resp = Invoke-WebRequest -Method Post -Uri $deliveredUrl `
        -Headers @{ Authorization = "Bearer $COURIER" } -UseBasicParsing -ErrorAction Stop
    $deliveredCode = [int]$resp.StatusCode
    $deliveredBody = $resp.Content
} catch {
    if ($_.Exception.Response -and $_.Exception.Response.StatusCode) {
        $deliveredCode = [int]$_.Exception.Response.StatusCode
        try { $deliveredBody = $_.ErrorDetails.Message } catch { $deliveredBody = "" }
    } else {
        Write-Host "  [ASSERT FAIL] excepcion sin Response: $($_.Exception.Message)" -ForegroundColor Red
        throw
    }
}
Info "HTTP $deliveredCode  Body: '$deliveredBody'"
Assert ($deliveredCode -eq 204) "POST .../delivered responde 204 No Content (recibido $deliveredCode)"

Info "Esperando 6s para que el bus propague OrderDelivered -> consumers terminales..."
Start-Sleep -Seconds 6

# 8) [ASSERTS DEL PASO 12 DEL PLAN] Secuencia final.
Step "8. ASSERTS - secuencia final tras entregar"
$finalAssign = GetRaw "$DISPATCH/api/v1/couriers/me/current-assignment" $COURIER
Assert ([int]$finalAssign.StatusCode -eq 204) "GET /me/current-assignment ahora responde 204 No Content (asignacion liberada)"

$me = GetJson "$DISPATCH/api/v1/couriers/me" $COURIER
Assert ($me.status -eq "Online") "GET /me responde status='Online' (courier liberado, listo para nueva asignacion). Recibido: $($me.status)"

$finalOrder = GetJson "$ORDERS/api/v1/orders/$ORDER_ID" $CUSTOMER
Assert ($finalOrder.status -eq "Completed") "GET /api/v1/orders/{id} (token customer) responde status='Completed'. Recibido: $($finalOrder.status)"

# 9) [ASSERTS DEL PASO 13 DEL PLAN] Negativos de seguridad.
Step "9. ASSERTS - negativos de seguridad"

# 9a) El mismo courier sin asignacion vuelve a llamar -> 404 Dispatch.Assignment.NoActiveAssignment.
ExpectStatusCode "$DISPATCH/api/v1/couriers/me/current-assignment/delivered" "POST" $COURIER 404 `
    "POST .../delivered desde courier ya liberado responde 404"

# 9b) Segundo courier (recien creado, sin asignacion) -> 404.
ExpectStatusCode "$DISPATCH/api/v1/couriers/me/current-assignment/delivered" "POST" $COURIER2 404 `
    "POST .../delivered desde courier2 sin asignacion responde 404"

# 9c) Customer JWT (sin RequireCourier) -> 403.
ExpectStatusCode "$DISPATCH/api/v1/couriers/me/current-assignment/delivered" "POST" $CUSTOMER 403 `
    "POST .../delivered con JWT de customer responde 403 (RequireCourier)"

# Cierre.
Write-Host "`n=================================================================" -ForegroundColor Green
Write-Host " SMOKE-COURIER-E2E COMPLETO - todos los asserts en VERDE" -ForegroundColor Green
Write-Host "=================================================================" -ForegroundColor Green
Write-Host "Gap #1 (snapshot con direcciones)            : verificado en paso 5" -ForegroundColor Green
Write-Host "Gap #4 (endpoint courier de entrega)         : verificado en paso 7-8" -ForegroundColor Green
Write-Host "Ownership (404 sin asignacion, 403 customer) : verificado en paso 9" -ForegroundColor Green
