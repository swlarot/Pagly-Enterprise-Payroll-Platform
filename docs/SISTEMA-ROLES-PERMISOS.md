# Sistema de Roles y Permisos - Planilla SaaS

**Versión**: 2.0
**Última actualización**: 31/01/2026
**Autor**: Planilla Documentation Team

---

## Tabla de Contenidos

1. [Resumen Ejecutivo](#resumen-ejecutivo)
2. [Arquitectura del Sistema](#arquitectura-del-sistema)
3. [Roles y Niveles de Acceso](#roles-y-niveles-de-acceso)
4. [Implementación Backend](#implementacion-backend)
5. [Implementación Frontend](#implementacion-frontend)
6. [Guía de Uso por Rol](#guia-de-uso-por-rol)
7. [Casos de Uso](#casos-de-uso)
8. [Seguridad y Mejores Prácticas](#seguridad-y-mejores-practicas)

---

## 1. Resumen Ejecutivo {#resumen-ejecutivo}

### 1.1 Objetivo del Sistema

El Sistema de Roles y Permisos de Planilla implementa un modelo **RBAC (Role-Based Access Control)** granular que permite:

- **Control de acceso por rol**: 5 roles predefinidos (Owner, Admin, Manager, Accountant, Employee)
- **Aislamiento multi-tenant**: Cada usuario solo ve datos de su empresa (tenant)
- **Restricciones de UI progresivas**: Botones y módulos visibles según permisos
- **Filtrado de datos a nivel de backend**: Empleados pueden ver solo sus propios datos
- **Dashboard adaptativo**: Métricas y acciones según rol del usuario

### 1.2 Cambios Implementados (Enero 2026)

#### Backend:
- ✅ `ControllerExtensions.cs` con helpers de permisos reutilizables
- ✅ Filtrado por rol en `EmpleadosController` (Employee solo ve sus datos)
- ✅ Campo `UserId` en entidad `Empleado` para vincular empleados con usuarios
- ✅ Migración de base de datos `AddUserIdToEmpleado`

#### Frontend:
- ✅ Dashboard empresarial con métricas clave (reemplazo de botones simples)
- ✅ `ConfiguracionPage.jsx` con 4 nuevas tabs filtradas por rol
- ✅ `AuthContext.tsx` con helpers `canWrite()`, `canDelete()`, `isReadOnly()`
- ✅ `AuthLayout.tsx` con función `canAccessModule()` para navegación
- ✅ 9 páginas de módulos con restricciones UI (Empleados, Departamentos, Posiciones, etc.)

### 1.3 Beneficios de Negocio

| Beneficio | Descripción |
|-----------|-------------|
| **Seguridad mejorada** | Empleados no pueden modificar ni ver datos ajenos |
| **Cumplimiento legal** | Registro de auditoría para Accountants |
| **Eficiencia operativa** | Managers procesan planillas sin acceso a configuración sensible |
| **Experiencia de usuario** | UI limpia sin opciones irrelevantes para cada rol |
| **Escalabilidad** | Sistema extensible para futuros roles personalizados |

---

## 2. Arquitectura del Sistema {#arquitectura-del-sistema}

### 2.1 Diagrama de Componentes

```
┌──────────────────────────────────────────────────────────────────┐
│                        PLANILLA SaaS                              │
├──────────────────────────────────────────────────────────────────┤
│                                                                   │
│  ┌─────────────────────────────────────────────────────────┐    │
│  │                    FRONTEND (React)                      │    │
│  │                                                           │    │
│  │  AuthContext                                             │    │
│  │  ├─ canWrite()        → Owner/Admin/Manager             │    │
│  │  ├─ canDelete()       → Owner/Admin                     │    │
│  │  ├─ isReadOnly()      → Accountant/Employee             │    │
│  │  └─ hasRole(...)      → Verificación de rol específico  │    │
│  │                                                           │    │
│  │  AuthLayout                                              │    │
│  │  └─ canAccessModule() → Filtrado de navegación          │    │
│  │                                                           │    │
│  │  Páginas (9 módulos)                                     │    │
│  │  └─ Restricciones UI  → Botones ocultos según permisos  │    │
│  └─────────────────────────────────────────────────────────┘    │
│                              ▲                                    │
│                              │ API Calls (JWT Bearer)            │
│                              │                                    │
│  ┌─────────────────────────────────────────────────────────┐    │
│  │                  BACKEND (ASP.NET Core)                  │    │
│  │                                                           │    │
│  │  JWT Middleware                                          │    │
│  │  └─ Extrae: tenant_id, sub, tenant_role, is_system_admin│    │
│  │                                                           │    │
│  │  ControllerExtensions                                    │    │
│  │  ├─ GetCurrentTenantId()  → Validación de tenant        │    │
│  │  ├─ GetCurrentUserId()    → Identificación de usuario   │    │
│  │  ├─ GetCurrentTenantRole()→ Rol en el tenant            │    │
│  │  ├─ CanWrite()            → Permisos de escritura       │    │
│  │  ├─ CanDelete()           → Permisos de eliminación     │    │
│  │  └─ IsOwnData()           → Validación de datos propios │    │
│  │                                                           │    │
│  │  EmpleadosController                                     │    │
│  │  └─ GET /api/empleados → Filtrado por UserId si Employee│    │
│  └─────────────────────────────────────────────────────────┘    │
│                              ▲                                    │
│                              │                                    │
│  ┌─────────────────────────────────────────────────────────┐    │
│  │                    BASE DE DATOS                         │    │
│  │                                                           │    │
│  │  Empleados (TenantId, UserId)                           │    │
│  │  TenantUsers (UserId, TenantId, Role)                   │    │
│  │  Tenants (Id, Name, SubscriptionId)                     │    │
│  └─────────────────────────────────────────────────────────┘    │
└──────────────────────────────────────────────────────────────────┘
```

### 2.2 Flujo de Autenticación y Autorización

```
1. Usuario ingresa credenciales
   ↓
2. Backend valida y genera JWT con claims:
   - sub (userId)
   - tenant_id
   - tenant_role (Owner/Admin/Manager/Accountant/Employee)
   - is_system_admin (true/false)
   ↓
3. Frontend almacena token en localStorage
   ↓
4. Cada request incluye: Authorization: Bearer <token>
   ↓
5. Middleware extrae claims del JWT
   ↓
6. Controller usa ControllerExtensions para validar permisos
   ↓
7. Respuesta filtrada según rol (Employee solo ve sus datos)
```

---

## 3. Roles y Niveles de Acceso {#roles-y-niveles-de-acceso}

### 3.1 Matriz de Permisos

| Módulo | Owner | Admin | Manager | Accountant | Employee |
|--------|-------|-------|---------|------------|----------|
| **Dashboard** | ✅ | ✅ | ✅ | ✅ | ✅ (limitado) |
| **Empleados** (Crear) | ✅ | ✅ | ✅ | ❌ | ❌ |
| **Empleados** (Editar) | ✅ | ✅ | ✅ | ❌ | ❌ |
| **Empleados** (Eliminar) | ✅ | ✅ | ❌ | ❌ | ❌ |
| **Empleados** (Ver) | ✅ Todos | ✅ Todos | ✅ Todos | ✅ Todos | ✅ Solo propios |
| **Departamentos** | ✅ | ✅ | ✅ | ❌ Solo lectura | ❌ |
| **Posiciones** | ✅ | ✅ | ✅ | ❌ Solo lectura | ❌ |
| **Planillas** (Crear) | ✅ | ✅ | ✅ | ❌ | ❌ |
| **Planillas** (Calcular) | ✅ | ✅ | ✅ | ❌ | ❌ |
| **Planillas** (Aprobar) | ✅ | ✅ | ❌ | ❌ | ❌ |
| **Reportes** (Ver) | ✅ | ✅ | ✅ | ✅ | ❌ |
| **Reportes** (Exportar) | ✅ | ✅ | ✅ | ✅ | ❌ |
| **Horas Extra** | ✅ | ✅ | ✅ | ❌ Solo lectura | ✅ Solo propias |
| **Ausencias** | ✅ | ✅ | ✅ | ❌ Solo lectura | ✅ Solo propias |
| **Vacaciones** | ✅ | ✅ | ✅ | ❌ Solo lectura | ✅ Solo propias |
| **Anticipos** | ✅ | ✅ | ✅ | ❌ Solo lectura | ❌ |
| **Préstamos** | ✅ | ✅ | ✅ | ❌ Solo lectura | ❌ |
| **Deducciones** | ✅ | ✅ | ✅ | ❌ Solo lectura | ❌ |
| **Configuración** (Tasas CSS/ISR) | ✅ | ✅ | ❌ Solo lectura | ❌ Solo lectura | ❌ |
| **Usuarios** (Invitar) | ✅ | ✅ | ❌ | ❌ | ❌ |
| **Usuarios** (Eliminar) | ✅ | ❌ | ❌ | ❌ | ❌ |
| **Audit Log** | ✅ | ✅ | ✅ Solo lectura | ✅ Solo lectura | ❌ |
| **Suscripción** (Upgrade) | ✅ | ❌ | ❌ | ❌ | ❌ |

### 3.2 Descripción de Roles

#### **Owner (Propietario)**
- **Descripción**: Dueño de la empresa y cuenta principal del tenant
- **Permisos**: Acceso total sin restricciones
- **Limitaciones**: Solo puede haber un Owner por tenant
- **Capacidades únicas**:
  - Cambiar plan de suscripción
  - Eliminar el tenant
  - Transferir ownership a otro usuario
  - Gestionar facturación en Stripe

#### **Admin (Administrador)**
- **Descripción**: Administrador del sistema con permisos casi totales
- **Permisos**: Todo excepto gestión de suscripción y eliminación del tenant
- **Limitaciones**: No puede cambiar plan ni eliminar tenant
- **Capacidades únicas**:
  - Invitar y eliminar usuarios del tenant
  - Aprobar planillas
  - Configurar tasas CSS/ISR

#### **Manager (Gerente)**
- **Descripción**: Gerente de recursos humanos que gestiona planillas
- **Permisos**: Lectura/escritura en empleados, planillas, asistencia y conceptos
- **Limitaciones**: No puede aprobar planillas ni gestionar usuarios
- **Capacidades únicas**:
  - Calcular planillas (pero no aprobarlas)
  - Crear y editar empleados
  - Gestionar ausencias, vacaciones y horas extra

#### **Accountant (Contador)**
- **Descripción**: Contador externo o interno con acceso de solo lectura
- **Permisos**: Solo lectura en reportes, planillas, empleados y audit log
- **Limitaciones**: No puede modificar ningún dato
- **Capacidades únicas**:
  - Exportar reportes a Excel/PDF (según plan)
  - Ver audit log completo
  - Consultar histórico de planillas

#### **Employee (Empleado)**
- **Descripción**: Empleado que solo puede ver su propia información
- **Permisos**: Solo lectura de sus propios datos (recibos, vacaciones, etc.)
- **Limitaciones**: No puede ver datos de otros empleados
- **Capacidades únicas**:
  - Ver sus recibos de pago
  - Consultar saldo de vacaciones
  - Ver sus horas extra y ausencias

---

## 4. Implementación Backend {#implementacion-backend}

### 4.1 ControllerExtensions.cs

Ubicación: `src/UI/Planilla.Web/Extensions/ControllerExtensions.cs`

```csharp
/// <summary>
/// Extensiones para Controllers que facilitan la verificación de permisos y roles
/// </summary>
public static class ControllerExtensions
{
    /// <summary>
    /// Obtiene el TenantId del usuario actual desde los claims del JWT
    /// </summary>
    public static int GetCurrentTenantId(this ControllerBase controller)
    {
        var claim = controller.User.FindFirst("tenant_id");
        if (claim == null || !int.TryParse(claim.Value, out var tenantId))
        {
            throw new UnauthorizedAccessException("No se encontró el TenantId en el token");
        }
        return tenantId;
    }

    /// <summary>
    /// Obtiene el UserId del usuario actual desde los claims del JWT
    /// </summary>
    public static string GetCurrentUserId(this ControllerBase controller)
    {
        var claim = controller.User.FindFirst(ClaimTypes.NameIdentifier)
                 ?? controller.User.FindFirst("sub");
        if (claim == null)
        {
            throw new UnauthorizedAccessException("No se encontró el UserId en el token");
        }
        return claim.Value;
    }

    /// <summary>
    /// Obtiene el rol del usuario actual en el tenant
    /// </summary>
    public static TenantRole GetCurrentTenantRole(this ControllerBase controller)
    {
        var roleClaim = controller.User.FindFirst("tenant_role");
        if (roleClaim == null || !Enum.TryParse<TenantRole>(roleClaim.Value, out var role))
        {
            throw new UnauthorizedAccessException("No se encontró el rol del tenant en el token");
        }
        return role;
    }

    /// <summary>
    /// Verifica si el usuario puede escribir (no es Employee ni Accountant en solo lectura)
    /// </summary>
    public static bool CanWrite(this ControllerBase controller)
    {
        try
        {
            var role = controller.GetCurrentTenantRole();
            return role == TenantRole.Owner
                || role == TenantRole.Admin
                || role == TenantRole.Manager;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Verifica si el usuario puede eliminar recursos
    /// </summary>
    public static bool CanDelete(this ControllerBase controller)
    {
        try
        {
            var role = controller.GetCurrentTenantRole();
            return role == TenantRole.Owner || role == TenantRole.Admin;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Crea una respuesta 403 Forbidden con mensaje personalizado
    /// </summary>
    public static ObjectResult Forbidden(this ControllerBase controller,
        string message = "No tienes permisos para realizar esta acción")
    {
        return new ObjectResult(new { error = message })
        {
            StatusCode = StatusCodes.Status403Forbidden
        };
    }
}
```

### 4.2 Ejemplo de Uso en EmpleadosController

```csharp
[HttpGet]
public async Task<IActionResult> GetEmpleados()
{
    try
    {
        var tenantId = this.GetCurrentTenantId();
        var userId = this.GetCurrentUserId();
        var role = this.GetCurrentTenantRole();

        // Si es Employee, solo puede ver sus propios datos
        if (role == TenantRole.Employee)
        {
            var empleado = await _empleadoService.GetByUserIdAsync(userId, tenantId);
            if (empleado == null)
            {
                return NotFound(new { error = "No se encontró su perfil de empleado" });
            }
            return Ok(new[] { empleado });
        }

        // Otros roles ven todos los empleados del tenant
        var empleados = await _empleadoService.GetAllByTenantAsync(tenantId);
        return Ok(empleados);
    }
    catch (UnauthorizedAccessException ex)
    {
        return Unauthorized(new { error = ex.Message });
    }
}

[HttpPost]
public async Task<IActionResult> CreateEmpleado([FromBody] CreateEmpleadoDto dto)
{
    if (!this.CanWrite())
    {
        return this.Forbidden("Solo Owner, Admin y Manager pueden crear empleados");
    }

    // Crear empleado...
}

[HttpDelete("{id}")]
public async Task<IActionResult> DeleteEmpleado(int id)
{
    if (!this.CanDelete())
    {
        return this.Forbidden("Solo Owner y Admin pueden eliminar empleados");
    }

    // Eliminar empleado...
}
```

### 4.3 Migración de Base de Datos

**Nombre**: `AddUserIdToEmpleado`
**Fecha**: 01/02/2026

```csharp
public partial class AddUserIdToEmpleado : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "UserId",
            table: "Empleados",
            type: "character varying(450)",
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

**Propósito**: Vincular cada empleado con un usuario de ASP.NET Identity, permitiendo que empleados accedan al sistema y vean solo sus datos.

### 4.4 Actualización de Entidad Empleado

```csharp
public class Empleado : ITenantEntity
{
    [Key]
    public int Id { get; set; }

    public int TenantId { get; set; }

    /// <summary>
    /// ID del usuario vinculado (para permitir que empleados accedan como usuarios)
    /// </summary>
    [StringLength(450)]
    public string? UserId { get; set; }

    [Required]
    public string Nombre { get; set; } = string.Empty;

    [Required]
    public string Apellido { get; set; } = string.Empty;

    // ... otros campos
}
```

---

## 5. Implementación Frontend {#implementacion-frontend}

### 5.1 AuthContext - Helpers de Permisos

**Ubicación**: `src/UI/Planilla.Web/ClientApp/src/contexts/AuthContext.tsx`

```typescript
const canWrite = (): boolean => {
  if (!user || isLoading) return false;
  // Owner, Admin y Manager pueden escribir
  return user.role === TenantRole.Owner
      || user.role === TenantRole.Admin
      || user.role === TenantRole.Manager;
};

const canDelete = (): boolean => {
  if (!user || isLoading) return false;
  // Solo Owner y Admin pueden eliminar
  return user.role === TenantRole.Owner || user.role === TenantRole.Admin;
};

const isReadOnly = (): boolean => {
  if (!user || isLoading) return true;
  // Accountant y Employee son solo lectura
  return user.role === TenantRole.Accountant || user.role === TenantRole.Employee;
};

const hasRole = (...roles: TenantRole[]): boolean => {
  // Retornar false si no hay usuario o si está cargando
  if (!user || isLoading) return false;
  return roles.includes(user.role);
};
```

**Uso en componentes**:

```tsx
const { canWrite, canDelete, hasRole } = useAuth();

// Mostrar botón solo si puede escribir
{canWrite() && (
  <button onClick={handleCreate}>Crear Empleado</button>
)}

// Mostrar botón solo si puede eliminar
{canDelete() && (
  <button onClick={handleDelete}>Eliminar</button>
)}

// Validación por rol específico
{hasRole(TenantRole.Owner, TenantRole.Admin) && (
  <button onClick={handleInviteUser}>Invitar Usuario</button>
)}
```

### 5.2 AuthLayout - Filtrado de Navegación

**Ubicación**: `src/UI/Planilla.Web/ClientApp/src/components/layout/AuthLayout.tsx`

```tsx
// Matriz de permisos: qué roles pueden ver qué módulos
const canAccessModule = (module: string): boolean => {
  if (!user) return false;

  const role = user.role;

  // Employee solo puede ver: Dashboard y su propia info
  if (role === TenantRole.Employee) {
    return ['dashboard', 'empleados', 'vacaciones', 'ausencias', 'horas-extra'].includes(module);
  }

  // Accountant puede ver todo excepto gestión de usuarios
  if (role === TenantRole.Accountant) {
    return module !== 'users' && module !== 'roles';
  }

  // Manager, Admin, Owner pueden ver todo
  return true;
};
```

**Aplicación en navegación**:

```tsx
{canAccessModule('empleados') && (
  <NavLink to="/empleados">
    <svg>...</svg>
    <span>Empleados</span>
  </NavLink>
)}

{canAccessModule('users') && (
  <NavLink to="/users">
    <svg>...</svg>
    <span>Usuarios</span>
  </NavLink>
)}
```

### 5.3 Páginas con Restricciones UI

**Ejemplo: EmpleadosPage.jsx**

```jsx
export default function EmpleadosPage() {
  const { canWrite, canDelete } = useAuth();

  return (
    <div className="space-y-6">
      {/* Header con botón Create solo si tiene permisos */}
      <div className="flex justify-between items-center">
        <h1>Gestión de Empleados</h1>
        {canWrite() && (
          <button onClick={handleCreate}>
            + Nuevo Empleado
          </button>
        )}
      </div>

      {/* Tabla de empleados */}
      <table>
        <tbody>
          {empleados.map(emp => (
            <tr key={emp.id}>
              <td>{emp.nombre}</td>
              <td>{emp.salario}</td>
              <td>
                <button onClick={() => handleView(emp)}>Ver</button>

                {canWrite() && (
                  <button onClick={() => handleEdit(emp)}>Editar</button>
                )}

                {canDelete() && (
                  <button onClick={() => handleDelete(emp)}>Eliminar</button>
                )}
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
```

### 5.4 ConfiguracionPage con Tabs Filtradas

**Ubicación**: `src/UI/Planilla.Web/ClientApp/src/pages/ConfiguracionPage.jsx`

```jsx
const allTabs = [
  { id: 'tasas', label: 'Tasas CSS/SE', visible: true },
  { id: 'isr', label: 'Tabla ISR', visible: true },
  {
    id: 'usuarios',
    label: 'Usuarios',
    visible: hasRole ? hasRole(TenantRole.Owner, TenantRole.Admin) : false
  },
  {
    id: 'audit',
    label: 'Audit Log',
    visible: hasRole ? hasRole(
      TenantRole.Owner, TenantRole.Admin, TenantRole.Manager, TenantRole.Accountant
    ) : false
  },
  { id: 'plan', label: 'Uso del Plan', visible: true },
  { id: 'soporte', label: 'Soporte', visible: true }
];

const tabs = allTabs.filter(tab => tab.visible);
```

**Resultado**:
- **Owner/Admin**: Ven 6 tabs
- **Manager/Accountant**: Ven 5 tabs (sin Usuarios)
- **Employee**: Ven 4 tabs (sin Usuarios ni Audit Log)

### 5.5 Dashboard Adaptativo

**Ubicación**: `src/UI/Planilla.Web/ClientApp/src/pages/AdminDashboardPage.tsx`

```tsx
// Stats Grid con métricas empresariales
const [stats, setStats] = useState<DashboardStats>({
  totalEmpleados: 0,
  empleadosActivos: 0,
  ultimaPlanilla: null,
  aportesCss: 0,
  pendientes: 0,
});

// Carga datos según rol
const loadDashboardData = async () => {
  // Cargar empleados (filtrado automático en backend si es Employee)
  const empleados = await api.get('/api/empleados');

  // Cargar planillas (solo si tiene acceso)
  if (canAccessModule('planillas')) {
    const planillas = await api.get('/api/payrollheaders');
    // Calcular métricas...
  }
};

return (
  <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-6">
    {/* Empleados Activos */}
    <div className="stat-card">
      <p>Empleados Activos</p>
      <p className="text-3xl">{stats.empleadosActivos}</p>
    </div>

    {/* Última Planilla */}
    {stats.ultimaPlanilla && (
      <div className="stat-card">
        <p>Última Planilla</p>
        <p className="text-3xl">{formatCurrency(stats.ultimaPlanilla.totalNetPay)}</p>
      </div>
    )}

    {/* Más métricas... */}
  </div>
);
```

---

## 6. Guía de Uso por Rol {#guia-de-uso-por-rol}

### 6.1 Owner (Propietario)

**Dashboard**: Visualiza todas las métricas empresariales y financieras.

**Empleados**:
- ✅ Crear, editar, eliminar empleados
- ✅ Ver todos los empleados del tenant
- ✅ Vincular empleados con usuarios

**Planillas**:
- ✅ Crear, calcular y aprobar planillas
- ✅ Generar reportes CSS, SE, ISR
- ✅ Exportar a Excel/PDF (según plan)

**Configuración**:
- ✅ Gestionar tasas CSS/ISR
- ✅ Invitar y eliminar usuarios
- ✅ Ver audit log completo
- ✅ Cambiar plan de suscripción
- ✅ Acceder a soporte prioritario

**Facturación**:
- ✅ Ver uso del plan
- ✅ Actualizar plan (upgrade/downgrade)
- ✅ Ver historial de facturas
- ✅ Cancelar suscripción

### 6.2 Admin (Administrador)

**Dashboard**: Mismas métricas que Owner.

**Empleados**:
- ✅ Crear, editar, eliminar empleados
- ✅ Ver todos los empleados
- ✅ Vincular empleados con usuarios

**Planillas**:
- ✅ Crear, calcular y aprobar planillas
- ✅ Generar reportes
- ✅ Exportar según plan

**Configuración**:
- ✅ Gestionar tasas CSS/ISR
- ✅ Invitar usuarios (no puede eliminar Owner)
- ✅ Ver audit log
- ❌ No puede cambiar plan de suscripción
- ❌ No puede eliminar el tenant

### 6.3 Manager (Gerente)

**Dashboard**: Visualiza métricas operativas (empleados, planillas pendientes).

**Empleados**:
- ✅ Crear y editar empleados
- ❌ No puede eliminar empleados
- ✅ Ver todos los empleados

**Planillas**:
- ✅ Crear y calcular planillas
- ❌ No puede aprobar planillas (requiere Admin/Owner)
- ✅ Generar reportes
- ✅ Exportar según plan

**Asistencia**:
- ✅ Registrar horas extra, ausencias, vacaciones
- ✅ Ver todos los registros del tenant

**Configuración**:
- 🔍 Solo lectura de tasas CSS/ISR
- ❌ No puede gestionar usuarios
- 🔍 Ver audit log (solo lectura)

### 6.4 Accountant (Contador)

**Dashboard**: Visualiza métricas financieras de solo lectura.

**Empleados**:
- 🔍 Ver todos los empleados (solo lectura)
- ❌ No puede crear, editar ni eliminar

**Planillas**:
- 🔍 Ver todas las planillas (solo lectura)
- 🔍 Ver reportes CSS, SE, ISR
- ✅ Exportar reportes a Excel/PDF (según plan)

**Configuración**:
- 🔍 Ver tasas CSS/ISR (solo lectura)
- 🔍 Ver audit log completo
- ❌ No puede modificar configuración

**Uso típico**: Contador externo que necesita acceso a reportes tributarios y registros de auditoría.

### 6.5 Employee (Empleado)

**Dashboard**: Visualiza solo sus propios datos.

**Empleados**:
- 🔍 Ver solo su propio perfil
- ❌ No puede ver otros empleados
- ❌ No puede editar su perfil

**Recibos**:
- 🔍 Ver sus propios recibos de pago
- 🔍 Descargar recibos en PDF (según plan)

**Vacaciones/Ausencias/Horas Extra**:
- 🔍 Ver solo sus propios registros
- ❌ No puede crear ni editar registros

**Configuración**:
- ❌ No tiene acceso a configuración
- ❌ No puede ver otros usuarios

**Uso típico**: Empleado que accede para consultar su información de pago y vacaciones.

---

## 7. Casos de Uso {#casos-de-uso}

### Caso 1: Empleado consulta su recibo de pago

**Actor**: Employee (Maria González, empleada de ventas)

**Flujo**:
1. Maria ingresa a `app.planilla.cloud`
2. Inicia sesión con su correo corporativo
3. El sistema valida que su rol es `Employee` y su `UserId` está vinculado a un empleado
4. El Dashboard muestra solo sus métricas personales
5. Navega a "Recibos de Pago"
6. El backend filtra automáticamente por `UserId = Maria's ID`
7. Maria ve solo sus propios recibos (no ve los de otros empleados)
8. Descarga su último recibo en PDF

**Validaciones**:
- ✅ Maria no puede acceder a `/empleados` (lista completa)
- ✅ Maria no puede ver recibos de otros empleados
- ✅ El menú lateral no muestra "Planillas", "Configuración", ni "Usuarios"

---

### Caso 2: Manager crea planilla pero no puede aprobarla

**Actor**: Manager (Carlos Pérez, gerente de RRHH)

**Flujo**:
1. Carlos ingresa al sistema
2. Navega a "Planillas" → "Nueva Planilla"
3. Crea planilla quincenal del 1-15 de febrero
4. Registra horas extra de 3 empleados
5. Calcula la planilla (botón visible porque `canWrite() = true`)
6. El sistema procesa salarios, deducciones CSS, ISR
7. Intenta aprobar la planilla
8. **Error**: Botón "Aprobar" NO está visible (solo Owner/Admin pueden aprobar)
9. Carlos notifica a su jefe (Admin) para aprobación

**Validaciones**:
- ✅ Carlos puede crear y calcular planillas
- ✅ Carlos puede editar planillas en estado "Borrador"
- ❌ Carlos NO puede aprobar ni pagar planillas

---

### Caso 3: Accountant exporta reporte CSS

**Actor**: Accountant (Laura Martínez, contadora externa)

**Flujo**:
1. Laura ingresa al sistema
2. Navega a "Reportes" → "Reporte CSS"
3. Selecciona planilla de diciembre 2025
4. Visualiza el reporte en pantalla (solo lectura)
5. Hace clic en "Exportar a Excel" (visible porque plan es Professional)
6. Descarga archivo `Reporte_CSS_Diciembre_2025.xlsx`
7. Intenta editar una planilla
8. **No hay botón de editar** (solo lectura)

**Validaciones**:
- ✅ Laura puede ver todos los reportes
- ✅ Laura puede exportar reportes (según plan)
- ❌ Laura NO puede crear, editar ni eliminar ningún dato
- ✅ Laura puede ver audit log completo

---

### Caso 4: Admin invita nuevo empleado al sistema

**Actor**: Admin (Juan Pérez, administrador)

**Flujo**:
1. Juan navega a "Organización" → "Empleados" → "Nuevo Empleado"
2. Completa el formulario:
   - Nombre: Roberto Castillo
   - Cédula: 8-777-7777
   - Salario: B/. 1,200.00
   - Departamento: Ventas
3. Activa checkbox "Crear usuario con acceso al sistema"
4. Ingresa email: `roberto.castillo@empresa.com`
5. Selecciona rol: `Employee`
6. Guarda empleado
7. El sistema:
   - Crea empleado en tabla `Empleados`
   - Vincula `UserId` con el registro de empleado
   - Envía invitación por email a Roberto
8. Roberto recibe email con enlace para establecer contraseña
9. Roberto acepta invitación y accede como `Employee`

**Validaciones**:
- ✅ Juan puede crear empleados y vincularlos con usuarios
- ✅ El sistema valida límite de empleados según plan
- ✅ Roberto solo puede ver su propia información

---

## 8. Seguridad y Mejores Prácticas {#seguridad-y-mejores-practicas}

### 8.1 Principios de Seguridad

#### **Defense in Depth (Defensa en Profundidad)**

```
┌─────────────────────────────────────────────────────┐
│  Capa 1: Validación Frontend (UI)                   │
│  └─ Ocultar botones según permisos                  │
│                                                      │
│  Capa 2: Validación Backend (Controllers)           │
│  └─ ControllerExtensions.CanWrite(), CanDelete()    │
│                                                      │
│  Capa 3: Filtrado de Datos (Services/Repositories)  │
│  └─ Filtrado automático por TenantId y UserId       │
│                                                      │
│  Capa 4: Base de Datos (Global Query Filters)       │
│  └─ EF Core aplica filtros automáticos por tenant   │
└─────────────────────────────────────────────────────┘
```

**Nunca confiar solo en el frontend**: Aunque ocultamos botones, el backend SIEMPRE valida permisos.

#### **Least Privilege (Mínimos Privilegios)**

Cada rol tiene solo los permisos necesarios para su función:
- Employee: Solo lectura de sus datos
- Accountant: Solo lectura de reportes
- Manager: Operación diaria sin acceso administrativo
- Admin: Gestión completa excepto facturación
- Owner: Control total

#### **Audit Trail (Registro de Auditoría)**

```csharp
// Registrar TODAS las operaciones críticas
public async Task<IActionResult> DeleteEmpleado(int id)
{
    var userId = this.GetCurrentUserId();
    var tenantId = this.GetCurrentTenantId();

    await _auditService.LogAsync(new AuditLog
    {
        UserId = userId,
        TenantId = tenantId,
        Action = "DELETE_EMPLOYEE",
        EntityType = "Empleado",
        EntityId = id,
        Timestamp = DateTime.UtcNow
    });

    // Eliminar empleado...
}
```

### 8.2 Validaciones Obligatorias

#### ✅ **Backend: SIEMPRE validar TenantId**

```csharp
// ❌ MAL: Sin validación de tenant
public async Task<IActionResult> GetEmpleados()
{
    var empleados = await _context.Empleados.ToListAsync();
    return Ok(empleados); // ¡EXPONE DATOS DE TODOS LOS TENANTS!
}

// ✅ BIEN: Con validación de tenant
public async Task<IActionResult> GetEmpleados()
{
    var tenantId = this.GetCurrentTenantId();
    var empleados = await _context.Empleados
        .Where(e => e.TenantId == tenantId)
        .ToListAsync();
    return Ok(empleados);
}
```

#### ✅ **Backend: Validar permisos de escritura**

```csharp
// ❌ MAL: Sin validación de permisos
[HttpPost]
public async Task<IActionResult> CreateEmpleado([FromBody] CreateEmpleadoDto dto)
{
    // Cualquiera puede crear empleados
}

// ✅ BIEN: Con validación de permisos
[HttpPost]
public async Task<IActionResult> CreateEmpleado([FromBody] CreateEmpleadoDto dto)
{
    if (!this.CanWrite())
    {
        return this.Forbidden();
    }
    // Crear empleado...
}
```

#### ✅ **Frontend: Validación defensiva en helpers**

```tsx
// ✅ BIEN: Manejo de estado de carga
const hasRole = (...roles: TenantRole[]): boolean => {
  // Retornar false si no hay usuario o si está cargando
  if (!user || isLoading) return false;
  return roles.includes(user.role);
};

// ❌ MAL: Sin validación de loading
const hasRole = (...roles: TenantRole[]): boolean => {
  return roles.includes(user.role); // ¡Puede causar crash si user es null!
};
```

#### ✅ **Frontend: Filtrado de tabs visible**

```tsx
// ✅ BIEN: Validación defensiva
const allTabs = [
  {
    id: 'usuarios',
    label: 'Usuarios',
    visible: hasRole ? hasRole(TenantRole.Owner, TenantRole.Admin) : false
  }
];

const tabs = allTabs.filter(tab => tab.visible);
```

### 8.3 Checklist de Seguridad

Al implementar nuevas funcionalidades, verificar:

- [ ] **Backend valida TenantId** en todas las queries
- [ ] **Backend valida permisos** usando `CanWrite()`, `CanDelete()`, etc.
- [ ] **Frontend oculta UI** según permisos con `canWrite()`, `canDelete()`
- [ ] **Navegación filtrada** en `AuthLayout.canAccessModule()`
- [ ] **Employee solo ve sus datos** con filtro por `UserId`
- [ ] **Audit log registra** operaciones críticas
- [ ] **Tests unitarios** validan permisos por rol
- [ ] **Tests de integración** verifican aislamiento multi-tenant

### 8.4 Errores Comunes a Evitar

#### ❌ **Error 1: Confiar solo en el frontend**

```tsx
// ❌ Ocultar botón NO es suficiente
{canWrite() && <button onClick={handleDelete}>Eliminar</button>}

// El usuario puede llamar directamente a DELETE /api/empleados/123
// ✅ SOLUCIÓN: Backend DEBE validar permisos
```

#### ❌ **Error 2: Olvidar filtrar por UserId en Employee**

```csharp
// ❌ Employee ve todos los empleados
public async Task<IActionResult> GetEmpleados()
{
    var tenantId = this.GetCurrentTenantId();
    return Ok(await _context.Empleados.Where(e => e.TenantId == tenantId).ToListAsync());
}

// ✅ Filtrar por UserId si es Employee
public async Task<IActionResult> GetEmpleados()
{
    var tenantId = this.GetCurrentTenantId();
    var role = this.GetCurrentTenantRole();

    if (role == TenantRole.Employee)
    {
        var userId = this.GetCurrentUserId();
        var empleado = await _context.Empleados
            .Where(e => e.TenantId == tenantId && e.UserId == userId)
            .ToListAsync();
        return Ok(empleado);
    }

    return Ok(await _context.Empleados.Where(e => e.TenantId == tenantId).ToListAsync());
}
```

#### ❌ **Error 3: No validar estado de carga en hooks**

```tsx
// ❌ Crash si user es null durante loading
const hasRole = (...roles: TenantRole[]): boolean => {
  return roles.includes(user.role); // TypeError: Cannot read property 'role' of null
};

// ✅ Validación defensiva
const hasRole = (...roles: TenantRole[]): boolean => {
  if (!user || isLoading) return false;
  return roles.includes(user.role);
};
```

---

## 9. Roadmap Futuro

### Fase 1: Roles Personalizados (Q2 2026)
- Permitir que Owner cree roles personalizados
- Editor visual de permisos granulares
- Límite de roles según plan de suscripción

### Fase 2: Permisos a Nivel de Campo (Q3 2026)
- Ocultar campos sensibles según rol (ej: salario)
- Máscaras de datos para Accountants externos

### Fase 3: Auditoría Avanzada (Q4 2026)
- Historial de cambios con diff visual
- Alertas de accesos sospechosos
- Exportación de audit log para cumplimiento legal

### Fase 4: Multi-Factor Authentication (Q1 2027)
- MFA obligatorio para Owner y Admin
- Autenticación biométrica en app móvil

---

## Anexos

### A. Códigos de Respuesta HTTP

| Código | Significado | Uso en Planilla |
|--------|-------------|-----------------|
| 200 OK | Éxito | Operación completada exitosamente |
| 201 Created | Creado | Nuevo empleado/planilla creado |
| 400 Bad Request | Datos inválidos | Validación de formulario falló |
| 401 Unauthorized | No autenticado | Token JWT inválido o expirado |
| 403 Forbidden | Sin permisos | Usuario no tiene el rol requerido |
| 404 Not Found | No encontrado | Recurso no existe o no pertenece al tenant |
| 409 Conflict | Conflicto | Cédula duplicada, planilla ya aprobada |
| 500 Internal Server Error | Error servidor | Error inesperado (registrar en logs) |

### B. JWT Claims Utilizados

```json
{
  "sub": "user-guid-12345",
  "email": "usuario@empresa.com",
  "tenant_id": "123",
  "tenant_role": "Manager",
  "is_system_admin": "false",
  "nbf": 1738358400,
  "exp": 1738444800,
  "iat": 1738358400
}
```

### C. Referencias

- [ASP.NET Core Authorization](https://learn.microsoft.com/en-us/aspnet/core/security/authorization/)
- [JWT Best Practices](https://datatracker.ietf.org/doc/html/rfc8725)
- [OWASP Top 10 - Broken Access Control](https://owasp.org/Top10/A01_2021-Broken_Access_Control/)
- [React Context API](https://react.dev/reference/react/useContext)

---

**Fin del Documento**

Para soporte técnico: `soporte@planilla.cloud`
Para reportar vulnerabilidades de seguridad: `security@planilla.cloud`
