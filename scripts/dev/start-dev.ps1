# Start Development Environment for Planilla
# This script starts both the backend (.NET) and frontend (Vite React) servers

Write-Host "Starting Planilla Development Environment..." -ForegroundColor Cyan
Write-Host ""

# Start Backend
Write-Host "[1/2] Starting Backend (.NET API)..." -ForegroundColor Green
$backendPath = Join-Path $PSScriptRoot "..\src\UI\Planilla.Web"
Start-Process -FilePath "dotnet" -ArgumentList "run" -WorkingDirectory $backendPath -WindowStyle Normal

Write-Host "Backend starting on http://localhost:5000" -ForegroundColor Yellow
Start-Sleep -Seconds 3

# Start Frontend
Write-Host "[2/2] Starting Frontend (Vite + React)..." -ForegroundColor Green
$frontendPath = Join-Path $PSScriptRoot "..\src\UI\Planilla.Web\ClientApp"
Start-Process -FilePath "npm" -ArgumentList "run dev" -WorkingDirectory $frontendPath -WindowStyle Normal

Write-Host "Frontend starting on http://localhost:5173" -ForegroundColor Yellow
Write-Host ""
Write-Host "Development environment ready!" -ForegroundColor Cyan
Write-Host ""
Write-Host "Backend:  http://localhost:5000" -ForegroundColor White
Write-Host "Frontend: http://localhost:5173" -ForegroundColor White
Write-Host ""
Write-Host "Press Ctrl+C to stop both servers" -ForegroundColor Gray
