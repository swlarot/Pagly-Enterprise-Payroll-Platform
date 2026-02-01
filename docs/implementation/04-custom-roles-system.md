# Sistema de Roles Personalizados (Custom Roles)

**Fecha**: 2026-02-01
**Commit**: `ae6892e`
**Tipo**: Nueva Funcionalidad Completa
**Archivos**: 18 archivos nuevos

## Propósito

Permitir que cada tenant cree roles personalizados con permisos granulares, más allá de los 5 roles básicos del sistema (Owner, Admin, Manager, Accountant, Employee).

## Problema que Resuelve

**Antes**:
- Solo 5 roles predefinidos y fijos
- No se podía adaptar permisos a necesidades específicas de cada empresa
- Roles "todo o nada" (Admin ve todo, Employee no ve nada)

**Después**:
- Tenants crean roles custom (ej: "Gerente de RRHH", "Supervisor de Nómina")
- Permisos granulares por módulo y acción
- Flexibilidad para adaptar sistema a estructura organizacional

## Arquitectura del Sistema

```
┌─────────────────────────────────────────────────────┐
│                  CustomTenantRole                   │
│  ┌──────────────────────────────────────────────┐  │
│  │ Id, Name, Description, TenantId, IsActive    │  │
│  └──────────────────────────────────────────────┘  │
│                       │                             │
│                       │ 1:N                         │
│                       ↓                             │
│  ┌──────────────────────────────────────────────┐  │
│  │           RolePermission                      │  │
│  │ Id, RoleId, Permission (enum)                 │  │
│  └──────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────┘
```

## Archivos Creados

### 1. Domain Layer (Entidades y Enums)

#### `CustomTenantRole.cs`
```csharp
public class CustomTenantRole : BaseEntity
{
    public string Name { get; set; }              // "Gerente de RRHH"
    public string? Description { get; set; }      // Descripción opcional
    public int TenantId { get; set; }             // Multi-tenancy
    public bool IsActive { get; set; } = true;
    public bool IsSystemRole { get; set; } = false; // Roles predefinidos vs custom

    // Navigation properties
    public virtual Tenant Tenant { get; set; }
    public virtual ICollection<RolePermission> Permissions { get; set; }
    public virtual ICollection<TenantUser> TenantUsers { get; set; }
}
```

**Características**:
- `IsSystemRole`: Distingue roles del sistema (Owner, Admin) de roles custom
- `IsActive`: Soft delete de roles
- Multi-tenant por diseño

#### `RolePermission.cs`
```csharp
public class RolePermission : BaseEntity
{
    public int CustomTenantRoleId { get; set; }
    public SystemPermission Permission { get; set; }

    // Navigation property
    public virtual CustomTenantRole CustomTenantRole { get; set; }
}
```

**Relación**: M:N entre CustomTenantRole y SystemPermission (a través de RolePermission)

#### `SystemPermission.cs` (Enum)
```csharp
public enum SystemPermission
{
    // Empleados
    ViewEmployees = 1,
    CreateEmployees = 2,
    EditEmployees = 3,
    DeleteEmployees = 4,

    // Departamentos
    ViewDepartments = 10,
    CreateDepartments = 11,
    EditDepartments = 12,
    DeleteDepartments = 13,

    // Posiciones
    ViewPositions = 20,
    CreatePositions = 21,
    EditPositions = 22,
    DeletePositions = 23,

    // Planillas
    ViewPayrolls = 30,
    CreatePayrolls = 31,
    ProcessPayrolls = 32,
    ApprovePayrolls = 33,
    DeletePayrolls = 34,

    // Ausencias
    ViewAbsences = 40,
    CreateAbsences = 41,
    EditAbsences = 42,
    DeleteAbsences = 43,

    // Vacaciones
    ViewVacations = 50,
    CreateVacations = 51,
    EditVacations = 52,
    ApproveVacations = 53,
    DeleteVacations = 54,

    // Horas Extra
    ViewOvertime = 60,
    CreateOvertime = 61,
    EditOvertime = 62,
    ApproveOvertime = 63,
    DeleteOvertime = 64,

    // Préstamos
    ViewLoans = 70,
    CreateLoans = 71,
    EditLoans = 72,
    ApproveLoans = 73,
    DeleteLoans = 74,

    // Deducciones
    ViewDeductions = 80,
    CreateDeductions = 81,
    EditDeductions = 82,
    DeleteDeductions = 83,

    // Anticipos
    ViewAdvances = 90,
    CreateAdvances = 91,
    EditAdvances = 92,
    DeleteAdvances = 93,

    // Reportes
    ViewReports = 100,
    ExportReports = 101,
    ViewFinancialReports = 102,

    // Configuración
    ViewSettings = 110,
    EditSettings = 111,
    ManageTaxRates = 112,

    // Usuarios
    ViewUsers = 120,
    InviteUsers = 121,
    EditUsers = 122,
    DeleteUsers = 123,
    ManageRoles = 124,

    // Auditoría
    ViewAuditLog = 130,

    // Suscripción
    ViewSubscription = 140,
    ManageSubscription = 141
}
```

**Total**: 60+ permisos granulares organizados por módulo

### 2. Application Layer (DTOs e Interfaces)

#### DTOs
- `CreateCustomTenantRoleDto.cs`: Para crear rol
- `UpdateCustomTenantRoleDto.cs`: Para editar rol
- `CustomTenantRoleDto.cs`: Para lectura
- `PermissionDto.cs`: Para listar permisos disponibles
- `UpdateRolePermissionsDto.cs`: Para asignar permisos a rol
- `AssignRoleToUserDto.cs`: Para asignar rol a usuario

#### Ejemplo: `CreateCustomTenantRoleDto.cs`
```csharp
public class CreateCustomTenantRoleDto
{
    [Required]
    [StringLength(100)]
    public string Name { get; set; }

    [StringLength(500)]
    public string? Description { get; set; }

    public List<SystemPermission> Permissions { get; set; } = new();
}
```

#### `ICustomTenantRoleService.cs`
```csharp
public interface ICustomTenantRoleService
{
    Task<CustomTenantRoleDto> CreateRoleAsync(CreateCustomTenantRoleDto dto);
    Task<CustomTenantRoleDto> UpdateRoleAsync(int id, UpdateCustomTenantRoleDto dto);
    Task DeleteRoleAsync(int id);
    Task<CustomTenantRoleDto> GetRoleByIdAsync(int id);
    Task<List<CustomTenantRoleDto>> GetAllRolesAsync();
    Task<List<PermissionDto>> GetAllPermissionsAsync();
    Task UpdateRolePermissionsAsync(int roleId, UpdateRolePermissionsDto dto);
    Task<bool> UserHasPermissionAsync(string userId, SystemPermission permission);
}
```

### 3. Infrastructure Layer (Servicios y Migraciones)

#### `CustomTenantRoleService.cs`
```csharp
public class CustomTenantRoleService : ICustomTenantRoleService
{
    private readonly ApplicationDbContext _context;
    private readonly ITenantContext _tenantContext;
    private readonly IMapper _mapper;

    public async Task<CustomTenantRoleDto> CreateRoleAsync(CreateCustomTenantRoleDto dto)
    {
        var tenantId = _tenantContext.TenantId;

        var role = new CustomTenantRole
        {
            Name = dto.Name,
            Description = dto.Description,
            TenantId = tenantId,
            IsSystemRole = false,
            IsActive = true
        };

        _context.CustomTenantRoles.Add(role);
        await _context.SaveChangesAsync();

        // Agregar permisos
        foreach (var permission in dto.Permissions)
        {
            _context.RolePermissions.Add(new RolePermission
            {
                CustomTenantRoleId = role.Id,
                Permission = permission
            });
        }

        await _context.SaveChangesAsync();

        return _mapper.Map<CustomTenantRoleDto>(role);
    }

    public async Task<bool> UserHasPermissionAsync(string userId, SystemPermission permission)
    {
        var tenantId = _tenantContext.TenantId;

        return await _context.TenantUsers
            .Where(tu => tu.UserId == userId && tu.TenantId == tenantId)
            .AnyAsync(tu => tu.CustomTenantRole.Permissions
                .Any(p => p.Permission == permission));
    }
}
```

#### `CustomRolesSeeder.cs`
```csharp
public static class CustomRolesSeeder
{
    public static void SeedSystemRoles(ApplicationDbContext context, int tenantId)
    {
        // Crear roles de sistema con permisos predefinidos
        var ownerRole = new CustomTenantRole
        {
            Name = "Owner",
            Description = "Propietario con acceso total",
            TenantId = tenantId,
            IsSystemRole = true,
            Permissions = GetAllPermissions()
        };

        var adminRole = new CustomTenantRole
        {
            Name = "Admin",
            Description = "Administrador con acceso casi total",
            TenantId = tenantId,
            IsSystemRole = true,
            Permissions = GetAdminPermissions()
        };

        // ... más roles

        context.CustomTenantRoles.AddRange(ownerRole, adminRole, ...);
        context.SaveChanges();
    }

    private static List<RolePermission> GetAllPermissions()
    {
        return Enum.GetValues<SystemPermission>()
            .Select(p => new RolePermission { Permission = p })
            .ToList();
    }
}
```

#### Migraciones
1. `20260131102726_AddCustomRolesAndPermissions.cs`
   - Crea tablas CustomTenantRoles y RolePermissions
   - Agrega relaciones FK

2. `20260131105038_AddSoftDeleteToAppUser.cs`
   - Agrega IsDeleted a AppUser
   - Permite soft delete de usuarios

### 4. Web Layer (Controllers y Authorization)

#### `CustomRolesController.cs`
```csharp
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CustomRolesController : ControllerBase
{
    private readonly ICustomTenantRoleService _roleService;

    [HttpGet]
    [Authorize(Roles = "Owner,Admin")]
    public async Task<IActionResult> GetAll()
    {
        var roles = await _roleService.GetAllRolesAsync();
        return Ok(roles);
    }

    [HttpPost]
    [Authorize(Roles = "Owner")]
    public async Task<IActionResult> Create(CreateCustomTenantRoleDto dto)
    {
        var role = await _roleService.CreateRoleAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = role.Id }, role);
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Owner")]
    public async Task<IActionResult> Update(int id, UpdateCustomTenantRoleDto dto)
    {
        await _roleService.UpdateRoleAsync(id, dto);
        return NoContent();
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Owner")]
    public async Task<IActionResult> Delete(int id)
    {
        await _roleService.DeleteRoleAsync(id);
        return NoContent();
    }

    [HttpGet("permissions")]
    public async Task<IActionResult> GetPermissions()
    {
        var permissions = await _roleService.GetAllPermissionsAsync();
        return Ok(permissions);
    }

    [HttpPut("{id}/permissions")]
    [Authorize(Roles = "Owner")]
    public async Task<IActionResult> UpdatePermissions(
        int id,
        UpdateRolePermissionsDto dto)
    {
        await _roleService.UpdateRolePermissionsAsync(id, dto);
        return NoContent();
    }
}
```

#### `RequirePermissionAttribute.cs`
```csharp
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public class RequirePermissionAttribute : TypeFilterAttribute
{
    public RequirePermissionAttribute(SystemPermission permission)
        : base(typeof(PermissionAuthorizationFilter))
    {
        Arguments = new object[] { permission };
    }
}

public class PermissionAuthorizationFilter : IAsyncAuthorizationFilter
{
    private readonly SystemPermission _requiredPermission;
    private readonly ICustomTenantRoleService _roleService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var userId = _httpContextAccessor.HttpContext?.User
            .FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(userId))
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        var hasPermission = await _roleService
            .UserHasPermissionAsync(userId, _requiredPermission);

        if (!hasPermission)
        {
            context.Result = new ForbidResult();
        }
    }
}
```

**Uso**:
```csharp
[HttpDelete("{id}")]
[RequirePermission(SystemPermission.DeleteEmployees)]
public async Task<IActionResult> DeleteEmployee(int id)
{
    // Solo usuarios con permiso DeleteEmployees pueden ejecutar
}
```

## Casos de Uso

### Caso 1: Crear Rol "Gerente de RRHH"
```json
POST /api/customroles

{
  "name": "Gerente de RRHH",
  "description": "Gestiona empleados y nómina, sin acceso a configuración",
  "permissions": [
    1,  // ViewEmployees
    2,  // CreateEmployees
    3,  // EditEmployees
    30, // ViewPayrolls
    31, // CreatePayrolls
    32, // ProcessPayrolls
    40, // ViewAbsences
    50, // ViewVacations
    53  // ApproveVacations
  ]
}
```

### Caso 2: Rol "Supervisor de Nómina" (Solo Lectura + Reportes)
```json
POST /api/customroles

{
  "name": "Supervisor de Nómina",
  "description": "Revisa planillas y genera reportes, sin edición",
  "permissions": [
    1,   // ViewEmployees
    30,  // ViewPayrolls
    100, // ViewReports
    101  // ExportReports
  ]
}
```

### Caso 3: Rol "Asistente Administrativo"
```json
POST /api/customroles

{
  "name": "Asistente Administrativo",
  "description": "Registra ausencias y vacaciones, sin acceso a salarios",
  "permissions": [
    1,  // ViewEmployees (sin ver salarios)
    40, // ViewAbsences
    41, // CreateAbsences
    50, // ViewVacations
    51  // CreateVacations
  ]
}
```

## Migración de Roles Básicos a Custom

```csharp
// Al crear un tenant, seed roles básicos como CustomTenantRoles
public async Task OnTenantCreatedAsync(int tenantId)
{
    var ownerRole = new CustomTenantRole
    {
        Name = "Owner",
        TenantId = tenantId,
        IsSystemRole = true,
        Permissions = AllPermissions()
    };

    var employeeRole = new CustomTenantRole
    {
        Name = "Employee",
        TenantId = tenantId,
        IsSystemRole = true,
        Permissions = new List<SystemPermission>
        {
            SystemPermission.ViewEmployees, // Solo sus datos
            SystemPermission.ViewAbsences,
            SystemPermission.ViewVacations
        }
    };

    _context.CustomTenantRoles.AddRange(ownerRole, employeeRole);
    await _context.SaveChangesAsync();
}
```

## Ventajas

1. **Flexibilidad**: Cada empresa define sus propios roles
2. **Granularidad**: 60+ permisos específicos por acción
3. **Escalabilidad**: Agregar nuevos permisos sin cambiar código
4. **Seguridad**: Principio de "least privilege" aplicado correctamente
5. **Multi-tenant**: Cada tenant tiene sus propios roles custom

## Limitaciones y Consideraciones

1. **Performance**: Verificar permisos en cada request puede ser costoso
   - **Solución**: Cachear permisos en memoria o Redis

2. **Complejidad UI**: Mostrar 60+ permisos puede ser abrumador
   - **Solución**: Agrupar por categorías (Empleados, Planillas, etc.)

3. **Conflictos**: Rol con permisos contradictorios
   - **Solución**: Validaciones al asignar permisos

4. **Migración**: Usuarios existentes con roles básicos
   - **Solución**: Script de migración a CustomTenantRoles

## Próximos Pasos

1. Aplicar `[RequirePermission]` en todos los endpoints
2. UI para gestión de roles custom
3. Preset templates (ej: "Gerente de RRHH", "Contador")
4. Auditoría de cambios de permisos
5. Reportes de roles y permisos por usuario

---

**Impacto**: Muy Alto - Cambio fundamental en autorización
**Complejidad**: Alta - 18 archivos nuevos, lógica compleja
**Prioridad**: Media-Alta - Mejora significativa pero no crítica
