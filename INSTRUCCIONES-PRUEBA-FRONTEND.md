# Instrucciones para Probar el Frontend Corregido

## Pasos para Verificar la Corrección

### 1. Asegurarse que el Backend está Corriendo

```powershell
# Desde C:\Planilla\src\UI\Planilla.Web
dotnet run
```

Debería mostrar: `Now listening on: http://localhost:5039`

### 2. Iniciar el Frontend en Modo Desarrollo

```powershell
# Desde C:\Planilla\src\UI\Planilla.Web\ClientApp
npm run dev
```

Debería mostrar: `Local: http://localhost:5173/`

### 3. Limpiar la Caché del Navegador

**IMPORTANTE:** Antes de probar, limpiar la caché:

- **Chrome/Edge:** Presionar `Ctrl + Shift + Delete`, seleccionar "Imágenes y archivos en caché", hacer clic en "Borrar datos"
- **O simplemente:** Presionar `Ctrl + F5` en la página para hacer un hard refresh

### 4. Probar las Páginas

#### Paso 1: Login
1. Ir a: http://localhost:5173/login
2. Ingresar credenciales:
   - Email: `contacto@vorluno.dev`
   - Password: `HatsukiMinara507*`
3. Hacer clic en "Iniciar Sesión"

#### Paso 2: Dashboard del Sistema
- Deberías ser redirigido automáticamente a `/system-admin/dashboard`
- **Verificar que se muestra:**
  - Total Tenants: 5
  - Total Usuarios: 5
  - Gráficos de distribución por plan
  - Crecimiento reciente

#### Paso 3: Ver Todos los Tenants
1. Hacer clic en "Ver Todos los Tenants" o ir a: http://localhost:5173/system-admin/tenants
2. **Verificar que se muestra:**
   - Tabla con 5 tenants
   - Filtros funcionando
   - Información de cada tenant visible

#### Paso 4: Detalles del Tenant
1. Hacer clic en el botón "Ver" de cualquier tenant
2. **Verificar que se muestra:**
   - Información del tenant
   - Estadísticas de uso (empleados, usuarios)
   - Información de suscripción
   - Datos del propietario

#### Paso 5: Crear Tenant
1. Hacer clic en "Crear Nuevo Tenant" o ir a: http://localhost:5173/system-admin/tenants/create
2. **Verificar que se muestra:**
   - Formulario con todos los campos
   - Validación funcionando

## Verificación con PowerShell (Opcional)

Si las páginas siguen en blanco, ejecutar este script para verificar que el backend está respondiendo correctamente:

```powershell
# Desde C:\Planilla
.\test-working-admin.ps1
```

Debería mostrar:
```
✅ Login successful
✅ /api/auth/me - Success
✅ /api/admin/metrics - Success
✅ /api/admin/tenants - Success
```

## Si Aún Hay Problemas

### 1. Verificar Consola del Navegador
1. Presionar `F12` para abrir DevTools
2. Ir a la pestaña "Console"
3. Buscar errores en rojo

### 2. Verificar Respuestas de Red
1. En DevTools, ir a la pestaña "Network"
2. Recargar la página
3. Verificar que las llamadas a `/api/admin/metrics` y `/api/admin/tenants` responden con status 200

### 3. Verificar que los Archivos están Actualizados
```powershell
# Reconstruir frontend
cd C:\Planilla\src\UI\Planilla.Web\ClientApp
npm run build

# Verificar que se generaron nuevos archivos
ls ../wwwroot/app.js | Select-Object LastWriteTime
```

## Tipos Corregidos

Los siguientes DTOs fueron actualizados para coincidir con el backend:

1. **AdminTenantDto**
   - `ruc`, `dv`, `address`, `phone`, `email` son opcionales (nullable)
   - `owner` es opcional (nullable en lista, populated en detalle)
   - `usage` incluye todas las propiedades

2. **AdminTenantUsageDto**
   - Agregadas propiedades: `userUsagePercentage`, `employeeUsagePercentage`

3. **SubscriptionInfoDto**
   - `plan` y `status` son números (enums)
   - Incluye `planName` y `statusName` como strings

4. **SystemMetricsDto**
   - `planDistribution` con propiedades lowercase
   - `recentGrowth` con `last7Days` y `last30Days`

## Próximos Pasos si Todo Funciona

1. Probar navegación entre páginas
2. Probar filtros en la tabla de tenants
3. Probar creación de un nuevo tenant
4. Verificar que los datos se actualizan correctamente
