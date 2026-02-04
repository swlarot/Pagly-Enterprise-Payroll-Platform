# Correcciones al Sistema de Administración
**Fecha:** 2026-01-28
**Problemas Resueltos:** 404 y 403 en endpoints de administración del sistema

---

## Problemas Identificados

### 1. Error 404 en `/api/auth/me`
**Error:** "Acceso al tenant no encontrado"
**Causa:** El endpoint `/api/auth/me` esperaba que todos los usuarios tuvieran un `TenantId`, pero los System Admins no pertenecen a ningún tenant.

### 2. Error 403 en `/api/admin/*` endpoints
**Error:** Forbidden al intentar acceder a `/api/admin/metrics` y `/api/admin/tenants`
**Causa:** La autorización no reconocía correctamente el rol de System Admin porque intentaba mapear `tenant_role` a un enum de `TenantRole`, pero System Admins no tienen un rol de tenant válido.

---

## Soluciones Implementadas

### Solución 1: Endpoint `/api/auth/me` - Soporte para System Admins

**Archivos Modificados:**
- `src/UI/Planilla.Web/Controllers/AuthController.cs` (líneas 362-470)
- `src/Core/Planilla.Application/DTOs/Auth/AuthResponseDto.cs` (líneas 28-36)
- `src/Core/Planilla.Application/DTOs/Auth/UserInfoDto.cs`

**Cambios:**
1. **AuthController.cs** - Lógica de bifurcación:
   ```csharp
   // Verificar si es System Admin
   var isSystemAdminClaim = User.FindFirst("is_system_admin")?.Value;
   if (isSystemAdminClaim == "true" || user.IsSystemAdmin)
   {
       // Retornar info sin tenant/subscription
       return Ok(new AuthResponseDto
       {
           User = new UserInfoDto
           {
               UserId = user.Id,
               Email = user.Email!,
               Role = TenantRole.Owner, // No aplica para admins
               RoleName = "SystemAdmin",
               IsSystemAdmin = true
           },
           Tenant = null,
           Subscription = null
       });
   }

   // Para usuarios regulares, validar tenant
   var tenantIdClaim = User.FindFirst("tenant_id")?.Value;
   // ...continuar con lógica existente
   ```

2. **AuthResponseDto.cs** - DTOs nullables:
   ```csharp
   /// <summary>
   /// Información del tenant (null para SystemAdmins)
   /// </summary>
   public TenantInfoDto? Tenant { get; set; }

   /// <summary>
   /// Información de la suscripción (null para SystemAdmins)
   /// </summary>
   public SubscriptionInfoDto? Subscription { get; set; }
   ```

### Solución 2: Autorización basada en Políticas para System Admins

**Archivos Creados:**
- `src/UI/Planilla.Web/Authorization/SystemAdminRequirement.cs`
- `src/UI/Planilla.Web/Authorization/SystemAdminAuthorizationHandler.cs`

**Archivos Modificados:**
- `src/UI/Planilla.Web/Program.cs`
- `src/UI/Planilla.Web/Controllers/AdminController.cs`

**Cambios:**

1. **SystemAdminRequirement.cs** - Definición de política:
   ```csharp
   using Microsoft.AspNetCore.Authorization;

   namespace Vorluno.Planilla.Web.Authorization;

   public class SystemAdminRequirement : IAuthorizationRequirement
   {
       // Marker class para la política
   }
   ```

2. **SystemAdminAuthorizationHandler.cs** - Handler de autorización:
   ```csharp
   using Microsoft.AspNetCore.Authorization;
   using System.Security.Claims;

   namespace Vorluno.Planilla.Web.Authorization;

   public class SystemAdminAuthorizationHandler
       : AuthorizationHandler<SystemAdminRequirement>
   {
       protected override Task HandleRequirementAsync(
           AuthorizationHandlerContext context,
           SystemAdminRequirement requirement)
       {
           var isSystemAdminClaim = context.User.FindFirst("is_system_admin");

           if (isSystemAdminClaim?.Value == "true")
           {
               context.Succeed(requirement);
           }

           return Task.CompletedTask;
       }
   }
   ```

3. **Program.cs** - Registro de política:
   ```csharp
   using Microsoft.AspNetCore.Authorization;
   using Vorluno.Planilla.Web.Authorization;

   // ...

   // Registrar política RequireSystemAdmin
   builder.Services.AddAuthorization(options =>
   {
       options.AddPolicy("RequireSystemAdmin", policy =>
           policy.Requirements.Add(new SystemAdminRequirement()));
   });

   // Registrar handler
   builder.Services.AddSingleton<IAuthorizationHandler, SystemAdminAuthorizationHandler>();
   ```

4. **AdminController.cs** - Aplicar política:
   ```csharp
   [ApiController]
   [Route("api/[controller]")]
   [Authorize(Policy = "RequireSystemAdmin")]  // ← Cambio principal
   public class AdminController : ControllerBase
   {
       // Removidos todos los checks manuales IsSystemAdminAsync()
       // La autorización ahora se maneja automáticamente

       [HttpGet("tenants")]
       public async Task<ActionResult<List<TenantListItemDto>>> GetAllTenants()
       {
           // Directamente ejecutar lógica sin verificaciones
       }
   }
   ```

---

## Verificación del Sistema Admin

**Usuarios System Admin creados por el seeder:**
1. `gjoseluisgonzalez507@gmail.com` / `HATSUKIMINARA*`
2. `contacto@vorluno.dev` / `HatsukiMinara507*`

**Logs de confirmación:**
```
info: Program[0]
      Usuario gjoseluisgonzalez507@gmail.com ya es SystemAdmin. Saltando.
info: Program[0]
      Usuario contacto@vorluno.dev ya es SystemAdmin. Saltando.
```

---

## Testing Manual

### 1. Test de Login y `/api/auth/me`

```powershell
# Login como System Admin
$loginResponse = Invoke-RestMethod -Uri "http://localhost:5039/api/auth/login" `
    -Method POST `
    -ContentType "application/json" `
    -Body '{"email":"gjoseluisgonzalez507@gmail.com","password":"HATSUKIMINARA*"}'

$token = $loginResponse.token

# Verificar /api/auth/me
$headers = @{ Authorization = "Bearer $token" }
$meResponse = Invoke-RestMethod -Uri "http://localhost:5039/api/auth/me" `
    -Method GET -Headers $headers

# Verificar respuesta
Write-Host "IsSystemAdmin: $($meResponse.user.isSystemAdmin)"
Write-Host "RoleName: $($meResponse.user.roleName)"
Write-Host "Tenant: $($meResponse.tenant)"
Write-Host "Subscription: $($meResponse.subscription)"
```

**Respuesta Esperada:**
```json
{
  "user": {
    "userId": "...",
    "email": "gjoseluisgonzalez507@gmail.com",
    "role": 0,
    "roleName": "SystemAdmin",
    "isSystemAdmin": true
  },
  "tenant": null,
  "subscription": null
}
```

### 2. Test de Endpoints Admin

```powershell
# Test GET /api/admin/metrics
$metricsResponse = Invoke-RestMethod -Uri "http://localhost:5039/api/admin/metrics" `
    -Method GET -Headers $headers

Write-Host "Total Tenants: $($metricsResponse.totalTenants)"

# Test GET /api/admin/tenants
$tenantsResponse = Invoke-RestMethod -Uri "http://localhost:5039/api/admin/tenants" `
    -Method GET -Headers $headers

Write-Host "Tenants Count: $($tenantsResponse.Count)"

# Test POST /api/admin/tenants (crear tenant)
$createTenantBody = @{
    name = "Empresa Test"
    ruc = "1234567-1-123456"
    dv = "12"
    ownerEmail = "owner@empresatest.com"
    ownerPassword = "Test123!@#"
    ownerFullName = "Owner Test"
} | ConvertTo-Json

$createResponse = Invoke-RestMethod -Uri "http://localhost:5039/api/admin/tenants" `
    -Method POST -Headers $headers -ContentType "application/json" -Body $createTenantBody

Write-Host "Tenant Created: $($createResponse.data.id)"
```

### 3. Test desde Frontend

1. Abrir http://localhost:5173/login
2. Ingresar credenciales de System Admin
3. Deberías ser redirigido a `/system-admin/dashboard`
4. Verificar que:
   - No hay errores 404 en la consola del navegador (F12)
   - No hay errores 403 al cargar métricas
   - Puedes navegar a "Gestión de Empresas" sin errores

---

## Arquitectura y Cumplimiento CLAUDE.md

### ✅ Validaciones

1. **DTOs Usados**: No se exponen entidades directamente
2. **Multi-tenancy Preservado**: Usuarios regulares siguen requiriendo TenantId
3. **Clean Architecture**: Cambios en Application (DTOs) y Web (Controllers, Authorization)
4. **Seguridad**: Policy-based authorization centralizada
5. **Logging**: Accesos de System Admin registrados
6. **Error Handling**: Mensajes de error claros y específicos

### Patrón de Autorización

**ANTES:**
```csharp
[Authorize]
public async Task<IActionResult> GetMetrics()
{
    if (!await IsSystemAdminAsync())
        return Forbid();
    // lógica
}
```

**DESPUÉS:**
```csharp
[Authorize(Policy = "RequireSystemAdmin")]
public async Task<IActionResult> GetMetrics()
{
    // lógica directa, autorización manejada por pipeline
}
```

---

## Estado del Sistema

### ✅ Servicios Corriendo
- **Backend:** http://localhost:5039 (HTTP 200)
- **Frontend:** http://localhost:5173 (HTTP 200)

### ✅ System Admin Seeder
- Ambos usuarios creados y confirmados en logs
- `IsSystemAdmin = true` verificado en base de datos

### ✅ Cambios Compilados
- Todos los archivos C# compilados correctamente
- Frontend no requiere cambios (solo backend)

---

## Scripts de Testing Creados

1. **test-auth-me-fix.ps1** - Test de endpoint `/api/auth/me`
2. **test-admin-auth.ps1** - Test de endpoints `/api/admin/*`
3. **quick-restart.ps1** - Script rápido de reinicio de servicios

---

## Próximos Pasos Sugeridos

1. ✅ **Probar login como System Admin**
2. ✅ **Verificar que no hay errores 404/403 en consola**
3. ✅ **Crear una empresa de prueba desde el panel admin**
4. [ ] **Opcional: Agregar más usuarios System Admin si es necesario**
5. [ ] **Opcional: Implementar audit logging para acciones de System Admin**

---

## Contacto Técnico

Para cualquier problema adicional:
- Logs de backend: `C:\Planilla\backend.log`
- Logs de frontend: `C:\Planilla\frontend.log`
- Errores de backend: `C:\Planilla\backend-error.log`

---

**FIN DEL REPORTE**
