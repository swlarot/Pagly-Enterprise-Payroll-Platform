# ============================================
# Script para Detener Servidores - Planilla
# ============================================

Write-Host "🛑 Deteniendo servidores de desarrollo..." -ForegroundColor Yellow
Write-Host ""

# Detener procesos dotnet (Backend)
Write-Host "🔧 Deteniendo Backend (.NET)..." -ForegroundColor Cyan
$dotnetProcesses = Get-Process dotnet -ErrorAction SilentlyContinue
if ($dotnetProcesses) {
    $dotnetProcesses | Stop-Process -Force
    Write-Host "   ✓ Backend detenido" -ForegroundColor Green
} else {
    Write-Host "   - No hay procesos .NET corriendo" -ForegroundColor Gray
}

# Detener procesos node (Frontend)
Write-Host "⚛️  Deteniendo Frontend (Vite)..." -ForegroundColor Cyan
$nodeProcesses = Get-Process node -ErrorAction SilentlyContinue
if ($nodeProcesses) {
    $nodeProcesses | Stop-Process -Force
    Write-Host "   ✓ Frontend detenido" -ForegroundColor Green
} else {
    Write-Host "   - No hay procesos Node corriendo" -ForegroundColor Gray
}

Write-Host ""
Write-Host "✅ Todos los servidores han sido detenidos" -ForegroundColor Green
Write-Host ""

Start-Sleep -Seconds 2
