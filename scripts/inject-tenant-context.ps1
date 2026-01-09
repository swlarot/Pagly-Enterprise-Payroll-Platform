# Script para inyectar ITenantContext y reemplazar TenantId = 1
$files = @(
    "C:\Planilla\src\UI\Planilla.Web\Controllers\DeduccionesController.cs",
    "C:\Planilla\src\UI\Planilla.Web\Controllers\PrestamosController.cs",
    "C:\Planilla\src\UI\Planilla.Web\Controllers\DepartamentosController.cs",
    "C:\Planilla\src\UI\Planilla.Web\Controllers\EmpleadosController.cs",
    "C:\Planilla\src\UI\Planilla.Web\Controllers\HorasExtraController.cs",
    "C:\Planilla\src\UI\Planilla.Web\Controllers\PosicionesController.cs",
    "C:\Planilla\src\UI\Planilla.Web\Controllers\AusenciasController.cs",
    "C:\Planilla\src\UI\Planilla.Web\Controllers\VacacionesController.cs"
)

foreach ($file in $files) {
    $content = Get-Content $file -Raw
    $originalContent = $content
    
    # Inyectar ITenantContext field y constructor parameter
    # Pattern: find "private readonly ApplicationDbContext _context;\n" and add ITenantContext after it
    if ($content -match 'private readonly ApplicationDbContext _context;' -and $content -notmatch 'private readonly ITenantContext _tenantContext;') {
        $content = $content -replace '(private readonly ApplicationDbContext _context;)', "`$1`r`n    private readonly ITenantContext _tenantContext;"
        Write-Host "Agregado field en $(Split-Path $file -Leaf)"
    }
    
    # Agregar parámetro al constructor (buscar el patrón del constructor con ApplicationDbContext)
    if ($content -notmatch ', ITenantContext tenantContext\)') {
        $content = $content -replace '(ApplicationDbContext context)\)', '$1, ITenantContext tenantContext)'
        Write-Host "Agregado parámetro en $(Split-Path $file -Leaf)"
        
        # Agregar asignación en el constructor
        $content = $content -replace '(_context = context;)', "`$1`r`n        _tenantContext = tenantContext;"
        Write-Host "Agregada asignación en $(Split-Path $file -Leaf)"
    }
    
    # Reemplazar TenantId = 1 con TenantId = _tenantContext.TenantId
    $content = $content -replace 'TenantId = 1,', 'TenantId = _tenantContext.TenantId,'
    $content = $content -replace 'TenantId = 1 //', 'TenantId = _tenantContext.TenantId //'
    
    # Guardar solo si hubo cambios
    if ($content -ne $originalContent) {
        Set-Content -Path $file -Value $content -NoNewline
        Write-Host "Actualizado: $(Split-Path $file -Leaf)"
    }
}

Write-Host "`nInyección completada en todos los archivos."
