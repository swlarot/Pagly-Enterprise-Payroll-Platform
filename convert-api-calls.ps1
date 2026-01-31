# Script para convertir fetch() a api.* y showToast() a toast.*

$archivos = @(
    "src\UI\Planilla.Web\ClientApp\src\pages\AnticiposPage.jsx",
    "src\UI\Planilla.Web\ClientApp\src\pages\HorasExtraPage.jsx",
    "src\UI\Planilla.Web\ClientApp\src\pages\AusenciasPage.jsx",
    "src\UI\Planilla.Web\ClientApp\src\pages\VacacionesPage.jsx"
)

foreach ($archivo in $archivos) {
    Write-Host "Procesando $archivo..." -ForegroundColor Cyan

    $contenido = Get-Content $archivo -Raw -Encoding UTF8

    # Patrón 1: fetch GET -> api.get
    $contenido = $contenido -replace "const response = await fetch\('([^']+)'\);[\r\n\s]+if \(!response\.ok\) throw new Error\(`"Error \`\$\{response\.status\}[^`]+`"\);[\r\n\s]+const data = await response\.json\(\);", "const data = await api.get('`$1');"

    # Patrón 2: fetch GET con template literal -> api.get
    $contenido = $contenido -replace "const response = await fetch\(``([^``]+)``\);[\r\n\s]+if \(!response\.ok\) throw new Error\(`"Error \`\$\{response\.status\}[^`]+`"\);[\r\n\s]+const data = await response\.json\(\);", "const data = await api.get``(`$1``);"

    # Patrón 3: showToast error -> toast.error
    $contenido = $contenido -replace "showToast\(\{ type: 'error', message: ([^}]+) \}\)", "toast.error(`$1)"

    # Patrón 4: showToast success -> toast.success
    $contenido = $contenido -replace "showToast\(\{ type: 'success', message: ([^}]+) \}\)", "toast.success(`$1)"

    # Guardar cambios
    [System.IO.File]::WriteAllText($archivo, $contenido, [System.Text.UTF8Encoding]::new($false))

    Write-Host "  ✓ Completado" -ForegroundColor Green
}

Write-Host "`nConversión automática completada. Verifica los archivos y completa manualmente fetch() con POST/PUT/DELETE" -ForegroundColor Yellow
