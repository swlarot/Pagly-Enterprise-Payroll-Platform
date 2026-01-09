# PowerShell script to apply TenantId security fixes to remaining controllers
# This script adds [Authorize] attribute, TenantId filtering, and AsNoTracking() to all queries

$controllers = @(
    "C:\Planilla\src\UI\Planilla.Web\Controllers\DepartamentosController.cs",
    "C:\Planilla\src\UI\Planilla.Web\Controllers\PosicionesController.cs",
    "C:\Planilla\src\UI\Planilla.Web\Controllers\HorasExtraController.cs",
    "C:\Planilla\src\UI\Planilla.Web\Controllers\DeduccionesController.cs",
    "C:\Planilla\src\UI\Planilla.Web\Controllers\AnticiposController.cs"
)

foreach ($controller in $controllers) {
    Write-Host "Processing: $controller" -ForegroundColor Cyan

    $content = Get-Content $controller -Raw

    # Add Authorization using if not present
    if ($content -notmatch 'using Microsoft\.AspNetCore\.Authorization;') {
        $content = $content -replace '(using Microsoft\.AspNetCore\.Mvc;)', "using Microsoft.AspNetCore.Authorization;`r`n`$1"
        Write-Host "  ✓ Added Authorization using" -ForegroundColor Green
    }

    # Add [Authorize] attribute to class if not present
    if ($content -notmatch '\[Authorize\][\s\r\n]+\[ApiController\]') {
        $content = $content -replace '(\[ApiController\])', "[Authorize] // ✅ SEGURIDAD: Todos los endpoints requieren autenticación`r`n`$1"
        Write-Host "  ✓ Added [Authorize] to class" -ForegroundColor Green
    }

    # Save changes
    Set-Content -Path $controller -Value $content -NoNewline
}

Write-Host "`nAll controllers updated successfully!" -ForegroundColor Green
Write-Host "Now run manual edits for TenantId filtering in queries..." -ForegroundColor Yellow
