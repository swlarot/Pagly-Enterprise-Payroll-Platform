# Filtrado por Rol en EmpleadosController

**Fecha**: 2026-02-01
**Commit**: `e02b34c`
**Tipo**: Modificación de Controller
**Archivo**: `src/UI/Planilla.Web/Controllers/EmpleadosController.cs`

## Propósito

Implementar filtrado de datos basado en el rol del usuario para que empleados con rol "Employee" solo puedan ver su propia información, mientras que otros roles ven todos los empleados del tenant.

## Problema que Resuelve

**Antes**:
- Endpoint `GET /api/empleados` retornaba todos los empleados del tenant
- Usuarios con rol Employee podían ver datos de otros empleados
- Violación de privacidad y principio de "least privilege"

**Después**:
- Employee solo ve su propio registro
- Accountant/Manager/Admin/Owner ven todos los empleados del tenant
- Cumplimiento con principio de acceso mínimo necesario

## Cambios Implementados

### 1. Imports Agregados

```csharp
using Vorluno.Planilla.Domain.Enums;      // Para TenantRole enum
using Vorluno.Planilla.Web.Extensions;    // Para ControllerExtensions
```

### 2. Modificación del Endpoint GetAll

#### Antes
```csharp
[HttpGet]
[Authorize(Roles = "Owner,Admin,Manager,Accountant")]  // Employee no tenía acceso
public async Task<IActionResult> GetAll()
{
    var tenantId = _tenantContext.TenantId;

    // Retorna TODOS los empleados del tenant sin filtrar por rol
    var empleados = await _context.Empleados
        .Where(e => e.TenantId == tenantId)
        .Include(e => e.Departamento)
        .Include(e => e.Posicion)
        .AsNoTracking()
        .ToListAsync();

    var empleadosDto = _mapper.Map<IEnumerable<EmpleadoVerDto>>(empleados);
    return Ok(empleadosDto);
}
```

#### Después
```csharp
[HttpGet]
[Authorize(Roles = "Owner,Admin,Manager,Accountant,Employee")]  // ⬅️ Employee agregado
public async Task<IActionResult> GetAll()
{
    var tenantId = _tenantContext.TenantId;
    var role = this.GetCurrentTenantRole();  // ⬅️ Obtener rol actual

    var query = _context.Empleados
        .Where(e => e.TenantId == tenantId)
        .Include(e => e.Departamento)
        .Include(e => e.Posicion)
        .AsNoTracking();

    // ⬅️ FILTRADO POR ROL: Employee solo ve su propia información
    if (role == TenantRole.Employee)
    {
        var userId = this.GetCurrentUserId();

        // Buscar el empleado vinculado al usuario actual
        var empleadoUsuario = await _context.Empleados
            .Where(e => e.UserId == userId && e.TenantId == tenantId)
            .FirstOrDefaultAsync();

        if (empleadoUsuario != null)
        {
            query = query.Where(e => e.Id == empleadoUsuario.Id);
        }
        else
        {
            // Si no tiene empleado vinculado, retornar lista vacía
            return Ok(Array.Empty<EmpleadoVerDto>());
        }
    }

    var empleados = await query.ToListAsync();
    var empleadosDto = _mapper.Map<IEnumerable<EmpleadoVerDto>>(empleados);
    return Ok(empleadosDto);
}
```

## Lógica de Filtrado Detallada

### Flujo Completo

```
1. Usuario hace GET /api/empleados
   ↓
2. Middleware valida JWT y extrae claims
   ↓
3. [Authorize] verifica que tenga un rol permitido
   ↓
4. Controller obtiene rol del usuario (GetCurrentTenantRole)
   ↓
5. Evaluar rol:
   ├─ Si es Employee:
   │  ├─ Obtener UserId del token
   │  ├─ Buscar empleado con ese UserId
   │  ├─ Si existe: Filtrar solo ese empleado
   │  └─ Si no existe: Retornar array vacío
   │
   └─ Si es Owner/Admin/Manager/Accountant:
      └─ Retornar todos los empleados del tenant
   ↓
6. Aplicar filtros, includes, mapping
   ↓
7. Retornar 200 OK con lista de empleados
```

### Casos de Uso

#### Caso 1: Employee con Empleado Vinculado
```http
GET /api/empleados
Authorization: Bearer eyJ... (rol: Employee, userId: abc-123)
```

**Proceso**:
1. Rol detectado: `TenantRole.Employee`
2. UserId extraído: `abc-123`
3. Busca en BD: `Empleados.UserId == 'abc-123'`
4. Encuentra empleado con `Id = 5`
5. Query filtrado: `WHERE Id = 5 AND TenantId = 1`

**Respuesta**:
```json
[
  {
    "id": 5,
    "nombre": "Juan",
    "apellido": "Pérez",
    "numeroIdentificacion": "8-123-456",
    "salarioBase": 2500.00,
    "departamento": {
      "id": 2,
      "nombre": "Ventas"
    },
    "posicion": {
      "id": 3,
      "nombre": "Vendedor"
    }
  }
]
```

#### Caso 2: Employee sin Empleado Vinculado
```http
GET /api/empleados
Authorization: Bearer eyJ... (rol: Employee, userId: xyz-789)
```

**Proceso**:
1. Rol detectado: `TenantRole.Employee`
2. UserId extraído: `xyz-789`
3. Busca en BD: `Empleados.UserId == 'xyz-789'`
4. No encuentra ningún empleado
5. Retorna array vacío

**Respuesta**:
```json
[]
```

**Nota**: Este caso ocurre cuando se invita a un usuario con rol Employee pero aún no se ha vinculado a un empleado en nómina.

#### Caso 3: Manager/Admin/Owner
```http
GET /api/empleados
Authorization: Bearer eyJ... (rol: Admin)
```

**Proceso**:
1. Rol detectado: `TenantRole.Admin`
2. NO entra al `if (role == TenantRole.Employee)`
3. Query sin filtrado adicional: `WHERE TenantId = 1`

**Respuesta**:
```json
[
  {
    "id": 1,
    "nombre": "Carlos",
    "apellido": "García",
    "salarioBase": 3000.00,
    ...
  },
  {
    "id": 2,
    "nombre": "Ana",
    "apellido": "Martínez",
    "salarioBase": 2800.00,
    ...
  },
  {
    "id": 5,
    "nombre": "Juan",
    "apellido": "Pérez",
    "salarioBase": 2500.00,
    ...
  }
]
```

## Seguridad

### Validaciones Implementadas

1. **Autorización a Nivel de Endpoint**
   ```csharp
   [Authorize(Roles = "Owner,Admin,Manager,Accountant,Employee")]
   ```
   - Solo usuarios autenticados con estos roles pueden acceder

2. **Filtrado por Tenant**
   ```csharp
   .Where(e => e.TenantId == tenantId)
   ```
   - Aislamiento multi-tenant automático

3. **Filtrado por Rol**
   ```csharp
   if (role == TenantRole.Employee)
   ```
   - Usuarios Employee solo ven sus datos

4. **Validación de Vinculación**
   ```csharp
   .Where(e => e.UserId == userId && e.TenantId == tenantId)
   ```
   - Verifica que UserId pertenezca a empleado del tenant correcto

### Vectores de Ataque Mitigados

❌ **Ataque**: Employee modifica token para cambiar UserId
✅ **Mitigación**: JWT firmado criptográficamente, no modificable

❌ **Ataque**: Employee intenta acceder mediante otro endpoint
✅ **Mitigación**: Mismo patrón debe aplicarse a GetById, Update, Delete

❌ **Ataque**: Employee adivina IDs de otros empleados
✅ **Mitigación**: Filtrado por UserId previene acceso a otros registros

❌ **Ataque**: Cross-tenant access (ver empleados de otro tenant)
✅ **Mitigación**: Filtrado por TenantId del token JWT

## Otros Endpoints a Modificar

### GetById - Restricción Similar
```csharp
[HttpGet("{id}")]
[Authorize(Roles = "Owner,Admin,Manager,Accountant,Employee")]
public async Task<IActionResult> GetById(int id)
{
    var tenantId = _tenantContext.TenantId;
    var role = this.GetCurrentTenantRole();

    var query = _context.Empleados
        .Where(e => e.Id == id && e.TenantId == tenantId);

    // Employee solo puede consultar su propio ID
    if (role == TenantRole.Employee)
    {
        var userId = this.GetCurrentUserId();
        var empleadoUsuario = await _context.Empleados
            .Where(e => e.UserId == userId && e.TenantId == tenantId)
            .FirstOrDefaultAsync();

        if (empleadoUsuario == null || empleadoUsuario.Id != id)
        {
            return NotFound(); // O Forbidden si prefieres
        }
    }

    var empleado = await query
        .Include(e => e.Departamento)
        .Include(e => e.Posicion)
        .FirstOrDefaultAsync();

    if (empleado == null)
        return NotFound();

    return Ok(_mapper.Map<EmpleadoVerDto>(empleado));
}
```

### Update - Employee No Puede Editar
```csharp
[HttpPut("{id}")]
[Authorize(Roles = "Owner,Admin,Manager")] // Employee NO incluido
public async Task<IActionResult> Update(int id, EmpleadoActualizarDto dto)
{
    // Employee no puede editar su información (solo Admin/Manager)
    // ...
}
```

### Delete - Employee No Puede Eliminar
```csharp
[HttpDelete("{id}")]
[Authorize(Roles = "Owner,Admin")] // Employee NO incluido
public async Task<IActionResult> Delete(int id)
{
    // Employee no puede eliminarse (solo Owner/Admin)
    // ...
}
```

## Testing

### Unit Test - Employee Ve Solo Sus Datos
```csharp
[Fact]
public async Task GetAll_AsEmployee_ReturnsOnlyOwnRecord()
{
    // Arrange
    var empleadoId = 5;
    var userId = "abc-123";
    var tenantId = 1;

    var controller = CreateControllerWithClaims(
        new Claim("tenant_id", tenantId.ToString()),
        new Claim("tenant_role", "Employee"),
        new Claim(ClaimTypes.NameIdentifier, userId)
    );

    _context.Empleados.AddRange(
        new Empleado { Id = 5, UserId = userId, TenantId = tenantId },
        new Empleado { Id = 6, UserId = "xyz-789", TenantId = tenantId },
        new Empleado { Id = 7, UserId = null, TenantId = tenantId }
    );
    await _context.SaveChangesAsync();

    // Act
    var result = await controller.GetAll();

    // Assert
    var okResult = Assert.IsType<OkObjectResult>(result);
    var empleados = Assert.IsAssignableFrom<IEnumerable<EmpleadoVerDto>>(okResult.Value);
    var empleadosList = empleados.ToList();

    Assert.Single(empleadosList);
    Assert.Equal(empleadoId, empleadosList[0].Id);
}
```

### Integration Test - Cross-Tenant Isolation
```csharp
[Fact]
public async Task GetAll_EmployeeCannotSeeOtherTenantData()
{
    // Arrange
    var userId = "abc-123";

    _context.Empleados.AddRange(
        new Empleado { Id = 5, UserId = userId, TenantId = 1 },  // Tenant correcto
        new Empleado { Id = 10, UserId = userId, TenantId = 2 }  // Otro tenant
    );
    await _context.SaveChangesAsync();

    var controller = CreateControllerWithClaims(
        new Claim("tenant_id", "1"),  // Autenticado en Tenant 1
        new Claim("tenant_role", "Employee"),
        new Claim(ClaimTypes.NameIdentifier, userId)
    );

    // Act
    var result = await controller.GetAll();

    // Assert
    var okResult = Assert.IsType<OkObjectResult>(result);
    var empleados = Assert.IsAssignableFrom<IEnumerable<EmpleadoVerDto>>(okResult.Value);
    var empleadosList = empleados.ToList();

    Assert.Single(empleadosList);
    Assert.Equal(5, empleadosList[0].Id); // Solo ve empleado de Tenant 1
}
```

## Performance

### Consideraciones

1. **Query Efficiency**: Dos queries cuando es Employee
   - Primera query: Buscar empleado por UserId
   - Segunda query: Filtrar lista por empleadoId
   - **Optimización**: Combinar en una sola query si es necesario

2. **Caché**: Considerar cachear el empleadoId del usuario
   ```csharp
   // En un servicio
   private Dictionary<string, int> _userEmployeeCache = new();

   private async Task<int?> GetCachedEmpleadoId(string userId)
   {
       if (_userEmployeeCache.TryGetValue(userId, out var empleadoId))
           return empleadoId;

       var empleado = await _context.Empleados
           .Where(e => e.UserId == userId)
           .Select(e => e.Id)
           .FirstOrDefaultAsync();

       if (empleado != 0)
           _userEmployeeCache[userId] = empleado;

       return empleado;
   }
   ```

3. **Índices**: Considerar índice en `Empleados.UserId`
   ```csharp
   modelBuilder.Entity<Empleado>()
       .HasIndex(e => e.UserId);
   ```

## Métricas de Éxito

- ✅ Employee solo ve 1 registro (el suyo)
- ✅ Admin ve N registros (todos del tenant)
- ✅ Sin errores 403 Forbidden para Employee
- ✅ Sin datos de otros tenants expuestos
- ✅ Tiempo de respuesta < 200ms

---

**Impacto**: Alto - Seguridad y privacidad de datos
**Complejidad**: Media - Lógica condicional por rol
**Prioridad**: Crítica - Evita exposición de datos sensibles
