# Campo UserId en Entidad Empleado

**Fecha**: 2026-02-01
**Commit**: `7fcfc31`
**Tipo**: Modificación de Entidad + Migración
**Archivos**:
- `src/Core/Planilla.Domain/Entities/Empleado.cs`
- `src/Infrastructure/Planilla.Infrastructure/Migrations/20260201012923_AddUserIdToEmpleado.cs`
- `src/Infrastructure/Planilla.Infrastructure/Migrations/ApplicationDbContextModelSnapshot.cs`

## Propósito

Establecer una relación entre la entidad `Empleado` (datos de nómina) y `ApplicationUser` (usuario del sistema) para permitir que empleados puedan iniciar sesión y ver únicamente su propia información.

## Problema que Resuelve

**Antes**:
- No existía vínculo entre un empleado en nómina y un usuario del sistema
- Usuarios con rol "Employee" no podían acceder a su información de manera segura
- No se podía filtrar datos de empleado por usuario autenticado

**Después**:
- Empleados pueden tener una cuenta de usuario vinculada
- Sistema filtra automáticamente datos por UserId cuando el rol es Employee
- Portal de autoservicio para empleados es posible

## Cambios en Empleado.cs

### Antes
```csharp
public class Empleado : ITenantEntity
{
    public int Id { get; set; }
    public string Nombre { get; set; }
    public string Apellido { get; set; }
    public int TenantId { get; set; }
    // ... otros campos
}
```

### Después
```csharp
public class Empleado : ITenantEntity
{
    public int Id { get; set; }
    public string Nombre { get; set; }
    public string Apellido { get; set; }

    public int TenantId { get; set; }

    /// <summary>
    /// ID del usuario vinculado (para permitir que empleados accedan como usuarios)
    /// </summary>
    [StringLength(450)]
    public string? UserId { get; set; }  // ⬅️ NUEVO CAMPO

    // ... otros campos
}
```

## Detalles del Campo

### Tipo de Dato
- **C#**: `string?` (nullable)
- **Base de Datos**: `nvarchar(450)`

### Características
- **Nullable**: Sí - No todos los empleados necesitan ser usuarios
- **Longitud**: 450 caracteres (mismo que ASP.NET Identity UserIds)
- **Índice**: No indexado por defecto (puede agregarse si hay consultas frecuentes)
- **Foreign Key**: No configurada explícitamente (relación opcional)

### Casos de Uso

#### 1. Empleado Sin Usuario (NULL)
```csharp
var empleado = new Empleado
{
    Nombre = "Juan",
    Apellido = "Pérez",
    NumeroIdentificacion = "8-123-456",
    UserId = null  // No tiene cuenta de usuario
};
```

**Escenario**: Empleado que solo aparece en nómina, no accede al sistema.

#### 2. Empleado Con Usuario (Vinculado)
```csharp
var empleado = new Empleado
{
    Nombre = "María",
    Apellido = "González",
    NumeroIdentificacion = "8-789-012",
    UserId = "f8d3c2b1-4a5e-6789-0abc-def123456789"  // Vinculado a usuario
};
```

**Escenario**: Empleado que puede iniciar sesión y ver su información de nómina.

## Migración de Base de Datos

### Archivo: `20260201012923_AddUserIdToEmpleado.cs`

```csharp
public partial class AddUserIdToEmpleado : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "UserId",
            table: "Empleados",
            type: "nvarchar(450)",
            maxLength: 450,
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "UserId",
            table: "Empleados");
    }
}
```

### Comando Ejecutado
```bash
dotnet ef migrations add AddUserIdToEmpleado \
  --project src/Infrastructure/Planilla.Infrastructure \
  --startup-project src/UI/Planilla.Web

dotnet ef database update \
  --project src/Infrastructure/Planilla.Infrastructure \
  --startup-project src/UI/Planilla.Web
```

### SQL Generado
```sql
ALTER TABLE [Empleados]
ADD [UserId] nvarchar(450) NULL;
```

## Uso en Controllers

### EmpleadosController - Filtrado por UserId

```csharp
[HttpGet]
[Authorize(Roles = "Owner,Admin,Manager,Accountant,Employee")]
public async Task<IActionResult> GetAll()
{
    var tenantId = _tenantContext.TenantId;
    var role = this.GetCurrentTenantRole();

    var query = _context.Empleados
        .Where(e => e.TenantId == tenantId);

    // FILTRADO POR ROL EMPLOYEE
    if (role == TenantRole.Employee)
    {
        var userId = this.GetCurrentUserId(); // Del token JWT

        var empleadoUsuario = await _context.Empleados
            .Where(e => e.UserId == userId && e.TenantId == tenantId)
            .FirstOrDefaultAsync();

        if (empleadoUsuario != null)
        {
            query = query.Where(e => e.Id == empleadoUsuario.Id);
        }
        else
        {
            // Usuario no tiene empleado vinculado
            return Ok(Array.Empty<EmpleadoVerDto>());
        }
    }

    var empleados = await query.ToListAsync();
    return Ok(_mapper.Map<IEnumerable<EmpleadoVerDto>>(empleados));
}
```

## Flujo de Vinculación

### Opción 1: Manual (Admin vincula)
```
1. Admin crea empleado en nómina
2. Admin invita usuario con rol "Employee"
3. Admin edita empleado y asigna UserId del usuario creado
4. Usuario inicia sesión y ve solo sus datos
```

### Opción 2: Automático (Futuro)
```
1. Admin crea empleado en nómina
2. Admin marca checkbox "Crear usuario para este empleado"
3. Sistema automáticamente:
   - Crea usuario en ASP.NET Identity
   - Asigna rol "Employee" en tenant
   - Vincula UserId al empleado
   - Envía email de invitación
```

## Validaciones Recomendadas

### 1. UserId Único por Tenant
```csharp
// Un usuario solo puede estar vinculado a un empleado por tenant
var existeVinculo = await _context.Empleados
    .AnyAsync(e =>
        e.UserId == userId &&
        e.TenantId == tenantId &&
        e.Id != empleadoId);

if (existeVinculo)
{
    return BadRequest("Este usuario ya está vinculado a otro empleado");
}
```

### 2. UserId Válido
```csharp
// Verificar que el UserId corresponde a un usuario real
if (!string.IsNullOrEmpty(dto.UserId))
{
    var userExists = await _userManager.FindByIdAsync(dto.UserId);
    if (userExists == null)
    {
        return BadRequest("Usuario no encontrado");
    }
}
```

### 3. Usuario Pertenece al Tenant
```csharp
// Verificar que el usuario tiene acceso a este tenant
var tenantUser = await _context.TenantUsers
    .AnyAsync(tu =>
        tu.UserId == dto.UserId &&
        tu.TenantId == tenantId);

if (!tenantUser)
{
    return BadRequest("Usuario no pertenece a este tenant");
}
```

## Impacto en Otras Entidades

### Ausencias
```csharp
// Employee solo ve sus ausencias
if (role == TenantRole.Employee)
{
    var empleadoId = await GetEmpleadoIdByUserId(userId);
    query = query.Where(a => a.EmpleadoId == empleadoId);
}
```

### Vacaciones
```csharp
// Employee solo ve sus vacaciones
if (role == TenantRole.Employee)
{
    var empleadoId = await GetEmpleadoIdByUserId(userId);
    query = query.Where(v => v.EmpleadoId == empleadoId);
}
```

### HorasExtra
```csharp
// Employee solo ve sus horas extra
if (role == TenantRole.Employee)
{
    var empleadoId = await GetEmpleadoIdByUserId(userId);
    query = query.Where(h => h.EmpleadoId == empleadoId);
}
```

### RecibosDeSueldo
```csharp
// Employee solo ve sus recibos de pago
if (role == TenantRole.Employee)
{
    var empleadoId = await GetEmpleadoIdByUserId(userId);
    query = query.Where(r => r.EmpleadoId == empleadoId);
}
```

## Método Helper Reutilizable

```csharp
// En un servicio o clase base
private async Task<int?> GetEmpleadoIdByUserId(string userId, int tenantId)
{
    var empleado = await _context.Empleados
        .Where(e => e.UserId == userId && e.TenantId == tenantId)
        .Select(e => e.Id)
        .FirstOrDefaultAsync();

    return empleado != 0 ? empleado : null;
}
```

## Consideraciones de Seguridad

1. **Validación en Backend**: Siempre validar UserId en servidor, nunca confiar en frontend
2. **Filtrado Automático**: Usar Query Filters de EF Core para aplicar filtrado automático
3. **Auditoría**: Registrar cuando se vincula/desvincula un usuario a empleado
4. **GDPR/Privacidad**: Considerar implicaciones de vincular datos personales

## Datos de Ejemplo

```sql
-- Empleado sin usuario (solo nómina)
INSERT INTO Empleados (Nombre, Apellido, NumeroIdentificacion, UserId, TenantId)
VALUES ('Carlos', 'Ruiz', '8-111-222', NULL, 1);

-- Empleado con usuario vinculado
INSERT INTO Empleados (Nombre, Apellido, NumeroIdentificacion, UserId, TenantId)
VALUES ('Ana', 'Torres', '8-333-444', 'f8d3c2b1-4a5e-6789-0abc-def123456789', 1);
```

## Próximos Pasos

1. **UI de Vinculación**: Crear interfaz para que Admin vincule usuarios a empleados
2. **Invitación Automática**: Crear empleado + usuario + vinculación en un solo flujo
3. **Portal Employee**: Dashboard personalizado para empleados
4. **Notificaciones**: Email cuando se vincula/desvincula usuario
5. **Reportes**: Identificar empleados sin usuario vinculado

---

**Impacto**: Crítico - Habilita portal de autoservicio para empleados
**Complejidad**: Media - Requiere migración de BD y cambios en múltiples controllers
**Prioridad**: Alta - Necesario para filtrado por rol Employee
