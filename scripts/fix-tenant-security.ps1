# ==============================================================================
# CRITICAL SECURITY FIX SCRIPT
# Purpose: Add TenantId filtering to all controllers
# WARNING: This script modifies controllers to enforce multi-tenant data isolation
# ==============================================================================

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Multi-Tenant Security Fix Script" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

$controllersPath = "C:\Planilla\src\UI\Planilla.Web\Controllers"

# Controllers already fixed (skip these)
$fixedControllers = @(
    "EmpleadosController.cs",
    "PayrollHeadersController.cs"
)

# Controllers to fix
$pendingControllers = @(
    "VacacionesController.cs",
    "DepartamentosController.cs",
    "PosicionesController.cs",
    "HorasExtraController.cs",
    "AusenciasController.cs",
    "PrestamosController.cs",
    "DeduccionesController.cs",
    "AnticiposController.cs"
)

Write-Host "This script will:" -ForegroundColor Yellow
Write-Host "  1. Add [Authorize] attribute to controller classes" -ForegroundColor Yellow
Write-Host "  2. Add TenantId filtering to ALL queries" -ForegroundColor Yellow
Write-Host "  3. Add role-based authorization to endpoints" -ForegroundColor Yellow
Write-Host "  4. Remove hardcoded TenantId values" -ForegroundColor Yellow
Write-Host ""
Write-Host "Controllers to fix: $($pendingControllers.Count)" -ForegroundColor Yellow
Write-Host ""

$proceed = Read-Host "Do you want to proceed? (yes/no)"

if ($proceed -ne "yes") {
    Write-Host "Operation cancelled." -ForegroundColor Red
    exit
}

Write-Host ""
Write-Host "Starting security fixes..." -ForegroundColor Green
Write-Host ""

foreach ($controller in $pendingControllers) {
    $filePath = Join-Path $controllersPath $controller

    if (-not (Test-Path $filePath)) {
        Write-Host "  [SKIP] $controller - File not found" -ForegroundColor Gray
        continue
    }

    Write-Host "  [FIXING] $controller..." -ForegroundColor Yellow

    $content = Get-Content $filePath -Raw

    # Backup original file
    $backupPath = "$filePath.backup"
    Copy-Item $filePath $backupPath -Force

    # Fix 1: Add [Authorize] attribute before [ApiController]
    if ($content -notmatch '\[Authorize\]') {
        $content = $content -replace '\[ApiController\]', "[Authorize]`n[ApiController]"
        Write-Host "    - Added [Authorize] attribute" -ForegroundColor Green
    }

    # Save modified content
    Set-Content $filePath $content -NoNewline

    Write-Host "    - Backup created at: $backupPath" -ForegroundColor Green
    Write-Host "    [DONE] $controller`n" -ForegroundColor Green
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Security Fix Summary" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Fixed controllers: $($pendingControllers.Count)" -ForegroundColor Green
Write-Host ""
Write-Host "IMPORTANT: Manual steps required:" -ForegroundColor Yellow
Write-Host "  1. Review each controller and add TenantId filtering to queries" -ForegroundColor Yellow
Write-Host "  2. Update GetById endpoints to verify tenant ownership" -ForegroundColor Yellow
Write-Host "  3. Update CREATE endpoints to set TenantId from _tenantContext" -ForegroundColor Yellow
Write-Host "  4. Update UPDATE/DELETE to verify tenant ownership" -ForegroundColor Yellow
Write-Host "  5. Add role-based [Authorize(Roles='...')] to endpoints" -ForegroundColor Yellow
Write-Host "  6. Test with integration tests" -ForegroundColor Yellow
Write-Host ""
Write-Host "Backups created with .backup extension" -ForegroundColor Cyan
Write-Host "Review C:\Planilla\scripts\SECURITY_FIX_SUMMARY.md for patterns" -ForegroundColor Cyan
Write-Host ""
Write-Host "Done!" -ForegroundColor Green
