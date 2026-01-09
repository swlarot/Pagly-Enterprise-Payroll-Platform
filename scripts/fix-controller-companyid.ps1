# Script para reemplazar CompanyId por TenantId en Controllers
# Solo reemplaza la propiedad, NO las inyecciones ni la lógica

$controllersPath = "C:\Planilla\src\UI\Planilla.Web\Controllers"
$files = Get-ChildItem -Path $controllersPath -Filter "*.cs"

foreach ($file in $files) {
    $content = Get-Content $file.FullName -Raw
    $originalContent = $content
    
    # Reemplazar .CompanyId por .TenantId en accesos a propiedades
    $content = $content -replace '\.CompanyId', '.TenantId'
    
    # Reemplazar CompanyId = en asignaciones
    $content = $content -replace 'CompanyId\s*=', 'TenantId ='
    
    # Guardar solo si hubo cambios
    if ($content -ne $originalContent) {
        Set-Content -Path $file.FullName -Value $content -NoNewline
        Write-Host "Actualizado: $($file.Name)"
    }
}

Write-Host "`nReemplazo completado en todos los archivos."
