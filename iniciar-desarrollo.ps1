# Script de Inicio para Desarrollo - Planilla
# Backend: http://localhost:5039 (API)
# Frontend: http://localhost:5173 (React + Vite)

Write-Host "Iniciando Planilla en modo desarrollo..." -ForegroundColor Cyan
Write-Host ""

# Verificar puerto 5039
Write-Host "Verificando puerto 5039..." -ForegroundColor Yellow
$port5039 = Get-NetTCPConnection -LocalPort 5039 -ErrorAction SilentlyContinue
if ($port5039) {
    Write-Host "Puerto 5039 ocupado. Cerrando procesos..." -ForegroundColor Yellow
    $processId = $port5039.OwningProcess
    Stop-Process -Id $processId -Force -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 2
}

# Verificar puerto 5173
Write-Host "Verificando puerto 5173..." -ForegroundColor Yellow
$port5173 = Get-NetTCPConnection -LocalPort 5173 -ErrorAction SilentlyContinue
if ($port5173) {
    Write-Host "Puerto 5173 ocupado. Cerrando procesos..." -ForegroundColor Yellow
    $processId = $port5173.OwningProcess
    Stop-Process -Id $processId -Force -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 2
}

Write-Host ""
Write-Host "Directorio de trabajo: $PSScriptRoot" -ForegroundColor Gray
Write-Host ""

# Iniciar Backend
Write-Host "Iniciando Backend (.NET)..." -ForegroundColor Green
$backendScript = @"
Write-Host 'BACKEND - Planilla API' -ForegroundColor Green
Write-Host 'Puerto: http://localhost:5039' -ForegroundColor Cyan
Write-Host 'Swagger: http://localhost:5039/swagger' -ForegroundColor Cyan
Write-Host ''
cd '$PSScriptRoot\src\UI\Planilla.Web'
dotnet run
"@

Start-Process powershell -ArgumentList "-NoExit", "-Command", $backendScript

# Esperar backend
Write-Host "Esperando a que el backend inicie (15 segundos)..." -ForegroundColor Yellow
Start-Sleep -Seconds 15

# Iniciar Frontend
Write-Host "Iniciando Frontend (React + Vite)..." -ForegroundColor Green
$frontendScript = @"
Write-Host 'FRONTEND - React SPA' -ForegroundColor Green
Write-Host 'Puerto: http://localhost:5173' -ForegroundColor Cyan
Write-Host 'Hot Module Replacement: ACTIVADO' -ForegroundColor Cyan
Write-Host ''
cd '$PSScriptRoot\src\UI\Planilla.Web\ClientApp'
npm run dev
"@

Start-Process powershell -ArgumentList "-NoExit", "-Command", $frontendScript

# Esperar frontend
Start-Sleep -Seconds 8

Write-Host ""
Write-Host "Servidores iniciados correctamente!" -ForegroundColor Green
Write-Host ""
Write-Host "============================================" -ForegroundColor Cyan
Write-Host "  ABRIR EN NAVEGADOR:" -ForegroundColor Cyan
Write-Host "  http://localhost:5173" -ForegroundColor Yellow
Write-Host "" -ForegroundColor Cyan
Write-Host "  Swagger (API Docs):" -ForegroundColor Cyan
Write-Host "  http://localhost:5039/swagger" -ForegroundColor Yellow
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Credenciales de prueba:" -ForegroundColor Gray
Write-Host "   Email: admin@sistema.com" -ForegroundColor Gray
Write-Host "   Password: Admin123!" -ForegroundColor Gray
Write-Host ""
Write-Host "Los logs apareceran en las ventanas de PowerShell abiertas" -ForegroundColor Gray
Write-Host "Para detener: ejecuta detener-desarrollo.ps1" -ForegroundColor Gray
Write-Host ""

# Abrir navegador
Start-Sleep -Seconds 5
Write-Host "Abriendo navegador..." -ForegroundColor Cyan
Start-Process "http://localhost:5173"

Write-Host ""
Write-Host "Presiona Enter para cerrar esta ventana..."
Read-Host
