# ControllerExtensions - Helpers de Permisos Backend

**Fecha**: 2026-02-01
**Commit**: `705b98c`
**Tipo**: Nueva Implementación
**Archivo**: `src/UI/Planilla.Web/Extensions/ControllerExtensions.cs`

## Propósito

Proveer métodos helper reutilizables para todos los controllers de la API, permitiendo verificar permisos, extraer información del token JWT y manejar respuestas de autorización de forma consistente.

## Problema que Resuelve

**Antes**: Cada controller tenía que duplicar código para:
- Extraer el TenantId del token JWT
- Extraer el UserId del token JWT
- Verificar el rol del usuario
- Validar permisos de escritura/eliminación

**Después**: Un solo lugar centralizado con métodos extension reutilizables.

## Métodos Implementados

### 1. GetCurrentTenantId()
```csharp
public static int GetCurrentTenantId(this ControllerBase controller)
{
    var claim = controller.User.FindFirst("tenant_id");
    return int.Parse(claim?.Value ?? "0");
}
```

**Uso**: Obtener el ID del tenant del usuario actual desde el token JWT.

**Ejemplo**:
```csharp
[HttpGet]
public async Task<IActionResult> GetAll()
{
    var tenantId = this.GetCurrentTenantId();
    var data = await _context.Empleados
        .Where(e => e.TenantId == tenantId)
        .ToListAsync();
    return Ok(data);
}
```

### 2. GetCurrentUserId()
```csharp
public static string GetCurrentUserId(this ControllerBase controller)
{
    var claim = controller.User.FindFirst(ClaimTypes.NameIdentifier);
    return claim?.Value ?? string.Empty;
}
```

**Uso**: Obtener el ID del usuario autenticado (GUID de ASP.NET Identity).

**Ejemplo**:
```csharp
var userId = this.GetCurrentUserId();
var empleado = await _context.Empleados
    .Where(e => e.UserId == userId)
    .FirstOrDefaultAsync();
```

### 3. GetCurrentTenantRole()
```csharp
public static TenantRole GetCurrentTenantRole(this ControllerBase controller)
{
    var claim = controller.User.FindFirst("tenant_role");
    if (claim == null || !Enum.TryParse<TenantRole>(claim.Value, out var role))
    {
        return TenantRole.Employee; // Default más restrictivo
    }
    return role;
}
```

**Uso**: Obtener el rol del usuario dentro del tenant actual.

**Roles Posibles**:
- `TenantRole.Employee` (más restrictivo)
- `TenantRole.Accountant`
- `TenantRole.Manager`
- `TenantRole.Admin`
- `TenantRole.Owner` (menos restrictivo)

**Ejemplo**:
```csharp
var role = this.GetCurrentTenantRole();
if (role == TenantRole.Employee)
{
    // Filtrar solo datos del empleado actual
}
```

### 4. CanWrite()
```csharp
public static bool CanWrite(this ControllerBase controller)
{
    var role = controller.GetCurrentTenantRole();
    return role == TenantRole.Owner
        || role == TenantRole.Admin
        || role == TenantRole.Manager;
}
```

**Uso**: Verificar si el usuario puede crear o editar registros.

**Permisos**:
- ✅ Owner
- ✅ Admin
- ✅ Manager
- ❌ Accountant (solo lectura)
- ❌ Employee (solo lectura)

**Ejemplo**:
```csharp
[HttpPost]
public async Task<IActionResult> Create(CreateDto dto)
{
    if (!this.CanWrite())
    {
        return this.Forbidden("No tienes permiso para crear registros");
    }
    // ... crear recurso
}
```

### 5. CanDelete()
```csharp
public static bool CanDelete(this ControllerBase controller)
{
    var role = controller.GetCurrentTenantRole();
    return role == TenantRole.Owner || role == TenantRole.Admin;
}
```

**Uso**: Verificar si el usuario puede eliminar registros.

**Permisos**:
- ✅ Owner
- ✅ Admin
- ❌ Manager (puede editar, no eliminar)
- ❌ Accountant
- ❌ Employee

**Ejemplo**:
```csharp
[HttpDelete("{id}")]
public async Task<IActionResult> Delete(int id)
{
    if (!this.CanDelete())
    {
        return this.Forbidden("Solo Owner y Admin pueden eliminar");
    }
    // ... eliminar recurso
}
```

### 6. Forbidden()
```csharp
public static IActionResult Forbidden(this ControllerBase controller, string message)
{
    return controller.StatusCode(403, new { message });
}
```

**Uso**: Retornar respuesta HTTP 403 Forbidden con mensaje personalizado.

**Ejemplo**:
```csharp
if (!this.CanWrite())
{
    return this.Forbidden("No tienes permiso para realizar esta acción");
}
```

**Respuesta HTTP**:
```json
HTTP/1.1 403 Forbidden
Content-Type: application/json

{
  "message": "No tienes permiso para realizar esta acción"
}
```

## Ventajas

1. **DRY (Don't Repeat Yourself)**: Código de verificación de permisos en un solo lugar
2. **Consistencia**: Todos los controllers usan la misma lógica
3. **Mantenibilidad**: Cambios en lógica de permisos se hacen una sola vez
4. **Legibilidad**: Código más limpio en controllers
5. **Seguridad**: Validaciones centralizadas reducen riesgo de errores

## Patrón de Uso en Controllers

```csharp
[ApiController]
[Route("api/[controller]")]
[Authorize] // Requiere autenticación
public class MiController : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var tenantId = this.GetCurrentTenantId(); // Extension method
        var role = this.GetCurrentTenantRole();   // Extension method

        // Filtrar datos por tenant y rol
        // ...
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateDto dto)
    {
        if (!this.CanWrite()) // Extension method
        {
            return this.Forbidden("Sin permisos de escritura"); // Extension method
        }

        var tenantId = this.GetCurrentTenantId();
        // Crear recurso...
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        if (!this.CanDelete()) // Extension method
        {
            return this.Forbidden("Sin permisos de eliminación");
        }

        // Eliminar recurso...
    }
}
```

## Dependencias

- **Namespace**: `Vorluno.Planilla.Web.Extensions`
- **Imports**:
  - `Microsoft.AspNetCore.Mvc`
  - `System.Security.Claims`
  - `Vorluno.Planilla.Domain.Enums` (para TenantRole)

## Testing Recomendado

```csharp
[Fact]
public void GetCurrentTenantId_WithValidClaim_ReturnsCorrectId()
{
    // Arrange
    var controller = CreateControllerWithClaims(
        new Claim("tenant_id", "123")
    );

    // Act
    var tenantId = controller.GetCurrentTenantId();

    // Assert
    Assert.Equal(123, tenantId);
}

[Fact]
public void CanWrite_AsManager_ReturnsTrue()
{
    var controller = CreateControllerWithClaims(
        new Claim("tenant_role", "Manager")
    );

    Assert.True(controller.CanWrite());
}

[Fact]
public void CanDelete_AsManager_ReturnsFalse()
{
    var controller = CreateControllerWithClaims(
        new Claim("tenant_role", "Manager")
    );

    Assert.False(controller.CanDelete());
}
```

## Próximos Pasos

1. Aplicar estos helpers en todos los controllers existentes
2. Agregar método `CanRead()` si se necesitan restricciones de lectura
3. Agregar logging/auditoría cuando se denieguen permisos
4. Considerar crear attribute `[RequireWrite]` y `[RequireDelete]` para simplificar aún más

---

**Impacto**: Alto - Todos los controllers se benefician
**Complejidad**: Baja - Métodos simples y directos
**Prioridad**: Crítica - Base para todo el sistema de permisos
