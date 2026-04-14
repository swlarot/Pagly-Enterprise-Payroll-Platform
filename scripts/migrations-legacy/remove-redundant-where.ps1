# Script para eliminar cláusulas WHERE redundantes de TenantId
$controllersPath = "C:\Planilla\src\UI\Planilla.Web\Controllers"
$files = Get-ChildItem -Path $controllersPath -Filter "*.cs"

foreach ($file in $files) {
    $content = Get-Content $file.FullName -Raw
    $originalContent = $content
    
    # Eliminar .Where(x => x.TenantId == 1) en todas sus variantes
    $content = $content -replace '\.Where\([a-z] => [a-z]\.TenantId == 1\)\s*\r?\n', ''
    $content = $content -replace '\s*\.Where\([a-z] => [a-z]\.TenantId == 1\)', ''
    
    # Eliminar && x.TenantId == 1 de condiciones compuestas
    $content = $content -replace ' && [a-z]\.TenantId == 1', ''
    $content = $content -replace '[a-z]\.TenantId == 1 && ', ''
    
    # Guardar solo si hubo cambios
    if ($content -ne $originalContent) {
        Set-Content -Path $file.FullName -Value $content -NoNewline
        Write-Host "Limpiado: $($file.Name)"
    }
}

Write-Host "`nLimpieza completada."
