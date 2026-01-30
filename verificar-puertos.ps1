# ============================================
# Script de Verificación de Puertos - Planilla
# ============================================

Write-Host "🔍 Verificando estado de los servidores..." -ForegroundColor Cyan
Write-Host ""

# Verificar Backend (puerto 5039)
Write-Host "🔧 Backend (.NET) - Puerto 5039:" -ForegroundColor Yellow
$backend = Get-NetTCPConnection -LocalPort 5039 -State Listen -ErrorAction SilentlyContinue
if ($backend) {
    $process = Get-Process -Id $backend.OwningProcess
    Write-Host "   ✅ ACTIVO - Proceso: $($process.ProcessName) (PID: $($process.Id))" -ForegroundColor Green
} else {
    Write-Host "   ❌ NO ACTIVO" -ForegroundColor Red
}

# Verificar Frontend (puerto 5173)
Write-Host "⚛️  Frontend (Vite) - Puerto 5173:" -ForegroundColor Yellow
$frontend = Get-NetTCPConnection -LocalPort 5173 -State Listen -ErrorAction SilentlyContinue
if ($frontend) {
    $process = Get-Process -Id $frontend.OwningProcess
    Write-Host "   ✅ ACTIVO - Proceso: $($process.ProcessName) (PID: $($process.Id))" -ForegroundColor Green
} else {
    Write-Host "   ❌ NO ACTIVO" -ForegroundColor Red
}

Write-Host ""

# Resumen
if ($backend -and $frontend) {
    Write-Host "✅ Todos los servidores están corriendo correctamente" -ForegroundColor Green
    Write-Host "   🌐 Abre: http://localhost:5173" -ForegroundColor Cyan
} elseif ($backend -and -not $frontend) {
    Write-Host "⚠️  El backend está corriendo pero falta el frontend" -ForegroundColor Yellow
    Write-Host "   Ejecuta: cd src/UI/Planilla.Web/ClientApp && npm run dev" -ForegroundColor Gray
} elseif (-not $backend -and $frontend) {
    Write-Host "⚠️  El frontend está corriendo pero falta el backend" -ForegroundColor Yellow
    Write-Host "   Ejecuta: dotnet run --project src/UI/Planilla.Web" -ForegroundColor Gray
} else {
    Write-Host "❌ Ningún servidor está corriendo" -ForegroundColor Red
    Write-Host "   Ejecuta: .\iniciar-desarrollo.ps1" -ForegroundColor Gray
}

Write-Host ""
