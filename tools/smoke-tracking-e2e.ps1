$ErrorActionPreference = "Stop"

# 1) Seed users/merchant/courier/quote (silenciado).
. "$PSScriptRoot/seed-smoke.ps1" *> $null
Write-Host "[1] Seed OK. QUOTE_ID = $QUOTE_ID"

# 2) Crear pedido.
$body = @{ quoteId = $QUOTE_ID; street = "Calle Test 123"; reference = "Apto 1"; latitude = 18.4861; longitude = -69.9312 } | ConvertTo-Json
$order = Invoke-RestMethod -Method Post -Uri "http://localhost:5005/api/v1/orders" -Headers @{ Authorization = "Bearer $CUSTOMER"; "Content-Type" = "application/json"; "Idempotency-Key" = "smoke-fase7-001" } -Body $body
$ORDER_ID = $order.orderId
Write-Host "[2] ORDER_ID = $ORDER_ID"

# 3) Lanzar smoke client en background con stdout redirect.
$smokeLog = "$PSScriptRoot/.smoke-client.log"
$smokeErr = "$PSScriptRoot/.smoke-client.err"
if (Test-Path $smokeLog) { Remove-Item $smokeLog -Force }
if (Test-Path $smokeErr) { Remove-Item $smokeErr -Force }

$smokeProc = Start-Process -FilePath "dotnet" -ArgumentList @("run","--project","tools/Rappix.Tracking.SmokeClient","--","$ORDER_ID","$CUSTOMER") -WorkingDirectory "$PSScriptRoot/.." -RedirectStandardOutput $smokeLog -RedirectStandardError $smokeErr -PassThru -NoNewWindow
Write-Host "[3] Smoke client PID = $($smokeProc.Id). Esperando 10s para conexion + Subscribe + snapshot..."
Start-Sleep -Seconds 10

# 4) Merchant accept.
Invoke-RestMethod -Method Post -Uri "http://localhost:5005/api/v1/orders/$ORDER_ID/accept" -Headers @{ Authorization = "Bearer $MERCHANT" } | Out-Null
Write-Host "[4] Merchant accept enviado. Esperando 8s saga + dispatch + tracking..."
Start-Sleep -Seconds 8

# 5) Courier reporta ubicacion (2 updates).
$loc1 = @{ latitude = 18.4870; longitude = -69.9320 } | ConvertTo-Json
Invoke-RestMethod -Method Post -Uri "http://localhost:5006/api/v1/couriers/me/location" -Headers @{ Authorization = "Bearer $COURIER"; "Content-Type" = "application/json" } -Body $loc1 | Out-Null
Write-Host "[5a] Location 1 enviada (18.487, -69.932)."
Start-Sleep -Seconds 3

$loc2 = @{ latitude = 18.4875; longitude = -69.9325 } | ConvertTo-Json
Invoke-RestMethod -Method Post -Uri "http://localhost:5006/api/v1/couriers/me/location" -Headers @{ Authorization = "Bearer $COURIER"; "Content-Type" = "application/json" } -Body $loc2 | Out-Null
Write-Host "[5b] Location 2 enviada (18.4875, -69.9325)."
Start-Sleep -Seconds 3

# 6) Mark delivered.
Invoke-RestMethod -Method Post -Uri "http://localhost:5005/api/v1/orders/$ORDER_ID/mark-delivered" -Headers @{ Authorization = "Bearer $MERCHANT" } | Out-Null
Write-Host "[6] Mark delivered enviado. Esperando 5s..."
Start-Sleep -Seconds 5

# 7) Cerrar smoke client.
if (-not $smokeProc.HasExited) { Stop-Process -Id $smokeProc.Id -Force }
Start-Sleep -Seconds 1

# 8) Imprimir evidencia.
Write-Host "`n==================== EVIDENCIA SMOKE CLIENT ===================="
Get-Content $smokeLog
Write-Host "================================================================`n"

# 9) Snapshot REST final.
$snap = Invoke-RestMethod -Method Get -Uri "http://localhost:5007/api/v1/tracking/orders/$ORDER_ID" -Headers @{ Authorization = "Bearer $CUSTOMER" }
Write-Host "Snapshot REST final:"
$snap | ConvertTo-Json -Depth 5
