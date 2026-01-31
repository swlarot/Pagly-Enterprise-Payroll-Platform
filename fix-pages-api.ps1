# Script para actualizar páginas JSX con api.ts y react-hot-toast
# Actualiza las páginas problemáticas que usan fetch() directo y useToast deprecated

$pages = @(
    "DeduccionesPage.jsx",
    "AnticiposPage.jsx",
    "HorasExtraPage.jsx",
    "AusenciasPage.jsx",
    "VacacionesPage.jsx"
)

$pagesPath = "C:\Planilla\src\UI\Planilla.Web\ClientApp\src\pages"

foreach ($page in $pages) {
    $filePath = Join-Path $pagesPath $page

    if (Test-Path $filePath) {
        Write-Host "Actualizando $page..." -ForegroundColor Cyan

        # Leer contenido
        $content = Get-Content $filePath -Raw

        # 1. Reemplazar import de useToast por react-hot-toast
        $content = $content -replace "import \{ useToast \} from '\.\./components/ToastContext';", "import toast from 'react-hot-toast';`nimport { api } from '../services/api';"

        # 2. Remover línea const { showToast } = useToast();
        $content = $content -replace "\s*const \{ showToast \} = useToast\(\);", ""

        # 3. Reemplazar showToast({ type: 'error', message: ... }) por toast.error(...)
        $content = $content -replace "showToast\(\s*\{\s*type:\s*'error',\s*message:\s*`"([^`"]+)`"\s*\}\s*\)", 'toast.error("$1")'
        $content = $content -replace "showToast\(\s*\{\s*type:\s*'error',\s*message:\s*([^\}]+)\}\s*\)", 'toast.error($1 || "Error")'

        # 4. Reemplazar showToast({ type: 'success', message: ... }) por toast.success(...)
        $content = $content -replace "showToast\(\s*\{\s*type:\s*'success',\s*message:\s*`"([^`"]+)`"\s*\}\s*\)", 'toast.success("$1")'
        $content = $content -replace "showToast\(\s*\{\s*type:\s*'success',\s*message:\s*([^\}]+)\}\s*\)", 'toast.success($1 || "Éxito")'

        # 5. Reemplazar fetch() por api.get()
        $content = $content -replace "const response = await fetch\('(/api/[^']+)'\);[\s\S]*?if \(!response\.ok\) throw[^;]+;[\s\S]*?const data = await response\.json\(\);", 'const data = await api.get(''$1'');'

        # 6. Reemplazar fetch con query params
        $content = $content -replace "const response = await fetch\(``(/api/[^``]+)``\);[\s\S]*?if \(!response\.ok\) throw[^;]+;[\s\S]*?const data = await response\.json\(\);", 'const data = await api.get(`$1`);'

        # Guardar cambios
        $content | Set-Content $filePath -NoNewline

        Write-Host "✓ $page actualizado" -ForegroundColor Green
    }
    else {
        Write-Host "✗ $page no encontrado" -ForegroundColor Red
    }
}

Write-Host "`nActualización completada. Verifica los cambios con git diff." -ForegroundColor Yellow
