#requires -Version 5.1

<#
.SYNOPSIS
  Smoke E2E del retiro del SimulatedDeliveryResponder.

.DESCRIPTION
  Demuestra que tras el retiro del responder, un pedido en InProgress NO se auto-completa.
  El gate del cambio es el paso 10: esperar 15s sin marcar delivered y verificar que el
  pedido sigue en "InProgress". Si ese assert falla, el simulador sobrevivio o hay otro
  auto-delivery oculto.

  Flujo:
    1. Seed (usuarios + merchant + item + courier seed con location, Online).
    2. Customer crea pedido.
    3. Merchant accept.
    4. Esperar 10s -> el pedido llega a InProgress (saga: AwaitingMerchant -> AwaitingPayment
       -> AwaitingCourier -> Dispatch claim -> Committing -> InProgress).
    5. ASSERT BACKEND-IS-WAITING (paso 8): status = "InProgress" (NO Completed).
    6. Esperar 15s sin marcar delivered.
    7. ASSERT NO-AUTO-DELIVERY (paso 10, EL GATE): status sigue "InProgress".
    8. Courier marca delivered (POST /me/current-assignment/delivered).
    9. Esperar 5s.
   10. ASSERT NOW-COMPLETED (paso 13): order=Completed, courier=Online, /current-assignment=204.

.REQUIREMENTS
  - Backend completo via docker compose con la rama chore/retire-simulated-delivery REBUILD.
  - PowerShell 5.1 o superior.
#>

$ErrorActionPreference = "Stop"

$IDENTITY = "http://localhost:5001"
$ORDERS   = "http://localhost:5005"
$DISPATCH = "http://localhost:5006"

function Step  ($msg) { Write-Host "`n=== $msg ===" -ForegroundColor Cyan }
function Ok    ($msg) { Write-Host "  [OK] $msg" -ForegroundColor Green }
function Info  ($msg) { Write-Host "  $msg" -ForegroundColor Gray }
function Assert($cond, $label) {
    if ($cond) { Write-Host "  [ASSERT OK] $label" -ForegroundColor Green }
    else       { Write-Host "  [ASSERT FAIL] $label" -ForegroundColor Red; throw "Assert fallido: $label" }
}
function PostJson($url, $body, $token) {
    $h = @{ "Content-Type" = "application/json" }
    if ($token) { $h["Authorization"] = "Bearer $token" }
    return Invoke-RestMethod -Method Post -Uri $url -Headers $h -Body ($body | ConvertTo-Json -Depth 10 -Compress)
}

# 1) Seed (reusa el script ya existente - registra usuarios + merchant + item + courier Online + quote fresco).
Step "1. Seed (delegando a seed-smoke.ps1)"
. "$PSScriptRoot/seed-smoke.ps1" *> $null
Ok "Tokens y IDs listos"
Info "CUSTOMER    = $CUSTOMER"
Info "MERCHANT    = $MERCHANT"
Info "COURIER     = $COURIER"
Info "QUOTE_ID    = $QUOTE_ID"

# 2) Customer crea pedido.
Step "2. Customer crea pedido"
$order = PostJson "$ORDERS/api/v1/orders" @{
    quoteId   = $QUOTE_ID
    street    = "Av. Retiro 100"
    reference = "Edif. Sin Simulator, apto 3B"
    latitude  = 18.4861
    longitude = -69.9312
} $CUSTOMER
$ORDER_ID = $order.orderId
Ok "ORDER_ID = $ORDER_ID  status=$($order.status)"

# Pequena espera para que la saga arranque (outbox + saga: Submitted -> ValidatingQuote ->
# ReservingStock -> AwaitingMerchant). Misma logica que el smoke-courier-e2e.
Info "Esperando 5s para que la saga arranque..."
Start-Sleep -Seconds 5

# 3) Merchant accept.
Step "3. Merchant accept"
Invoke-RestMethod -Method Post -Uri "$ORDERS/api/v1/orders/$ORDER_ID/accept" -Headers @{ Authorization = "Bearer $MERCHANT" } | Out-Null
Ok "Accept enviado"

# 4) Esperar saga + payment auto-fake + dispatch claim. Tras 10s el pedido deberia estar en
#    InProgress (saga llego a CourierAssigned y StockCommitted).
Info "Esperando 10s para que la saga corra hasta InProgress (saga -> payment -> dispatch -> commit)..."
Start-Sleep -Seconds 10

# 5) [ASSERT PASO 8 DEL PLAN] El pedido debe estar en InProgress.
Step "5. ASSERT BACKEND-IS-WAITING - status = InProgress (NO Completed)"
$o = Invoke-RestMethod -Uri "$ORDERS/api/v1/orders/$ORDER_ID" -Headers @{ Authorization = "Bearer $CUSTOMER" }
Write-Host "  --- Order JSON literal ---" -ForegroundColor Yellow
$o | ConvertTo-Json -Depth 3 -Compress | Out-Host
Write-Host "  --------------------------" -ForegroundColor Yellow
Assert ($o.status -eq "InProgress") "Order.status == 'InProgress' (recibido: '$($o.status)')"

# 6+7) [ASSERT PASO 10 DEL PLAN - EL GATE DEL CAMBIO]
#      Esperar 15s sin marcar delivered y volver a verificar. Si el simulator sobrevive,
#      en este intervalo el pedido se cierra a Completed y este assert falla.
Step "6+7. ASSERT NO-AUTO-DELIVERY (GATE) - esperar 15s SIN marcar delivered"
Info "Esperando 15s..."
Start-Sleep -Seconds 15
$o2 = Invoke-RestMethod -Uri "$ORDERS/api/v1/orders/$ORDER_ID" -Headers @{ Authorization = "Bearer $CUSTOMER" }
Write-Host "  --- Order JSON literal (post 15s wait) ---" -ForegroundColor Yellow
$o2 | ConvertTo-Json -Depth 3 -Compress | Out-Host
Write-Host "  ------------------------------------------" -ForegroundColor Yellow
Assert ($o2.status -eq "InProgress") "Order.status SIGUE en 'InProgress' tras 15s sin marcar delivered (recibido: '$($o2.status)')"

# 8) Courier marca delivered por el endpoint REAL (Fase 13.6).
Step "8. Courier marca delivered via POST /me/current-assignment/delivered"
$resp = Invoke-WebRequest -Method Post -Uri "$DISPATCH/api/v1/couriers/me/current-assignment/delivered" `
    -Headers @{ Authorization = "Bearer $COURIER" } -UseBasicParsing -ErrorAction Stop
Assert ([int]$resp.StatusCode -eq 204) "POST .../delivered responde 204 No Content (recibido $($resp.StatusCode))"

# 9) Esperar bus.
Info "Esperando 5s para que el bus propague OrderDelivered -> consumers terminales..."
Start-Sleep -Seconds 5

# 10) [ASSERT PASO 13 DEL PLAN] Secuencia final.
Step "10. ASSERT NOW-COMPLETED - order Completed, courier Online, /current-assignment 204"
$oFinal = Invoke-RestMethod -Uri "$ORDERS/api/v1/orders/$ORDER_ID" -Headers @{ Authorization = "Bearer $CUSTOMER" }
Write-Host "  --- Order final JSON ---" -ForegroundColor Yellow
$oFinal | ConvertTo-Json -Depth 3 -Compress | Out-Host
Assert ($oFinal.status -eq "Completed") "GET /orders/{id} (customer) status='Completed' (recibido: '$($oFinal.status)')"

$me = Invoke-RestMethod -Uri "$DISPATCH/api/v1/couriers/me" -Headers @{ Authorization = "Bearer $COURIER" }
Write-Host "  --- Courier /me JSON ---" -ForegroundColor Yellow
$me | ConvertTo-Json -Depth 3 -Compress | Out-Host
Assert ($me.status -eq "Online") "GET /couriers/me status='Online' tras liberar (recibido: '$($me.status)')"

$finalAssign = Invoke-WebRequest -Uri "$DISPATCH/api/v1/couriers/me/current-assignment" `
    -Headers @{ Authorization = "Bearer $COURIER" } -UseBasicParsing
Assert ([int]$finalAssign.StatusCode -eq 204) "GET /me/current-assignment 204 No Content tras liberar (recibido $($finalAssign.StatusCode))"

Write-Host "`n=================================================================" -ForegroundColor Green
Write-Host " SMOKE COMPLETO - el retiro del SimulatedDeliveryResponder funciona" -ForegroundColor Green
Write-Host "=================================================================" -ForegroundColor Green
Write-Host "  Gate del cambio (paso 6+7): el pedido NO se auto-completa." -ForegroundColor Green
Write-Host "  Solo el courier real puede llevarlo a Completed via POST .../delivered." -ForegroundColor Green
