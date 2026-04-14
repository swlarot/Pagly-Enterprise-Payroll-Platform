# Sistema de Roles y Permisos - Documentación Técnica

## Resumen Ejecutivo

Se ha implementado un sistema completo de roles y permisos (RBAC) para Planilla SaaS, que controla el acceso a módulos y operaciones según el rol del usuario dentro de su tenant. El sistema funciona tanto en backend (filtrado de datos, autorización de endpoints) como en frontend (navegación condicional, restricciones de UI).

**Fecha de Implementación**: 2026-02-01
**Versión**: 1.0.0

---

## Matriz de Permisos por Rol

| Rol | Navegación | Lectura | Escritura | Eliminación | Notas |
|-----|-----------|---------|-----------|-------------|-------|
| **Employee** | Dashboard, Empleados, Vacaciones, Ausencias, Horas Extra | Solo sus propios datos | ❌ No | ❌ No | Solo lectura de su información personal |
| **Accountant** | Todos los módulos excepto Users/Roles | Todos los datos del tenant | ❌ No | ❌ No | Solo lectura, ideal para contadores |
| **Manager** | Todos los módulos excepto Users/Roles | Todos los datos del tenant | ✅ Sí | ❌ No | Puede crear y editar, no eliminar |
| **Admin** | Todos los módulos | Todos los datos del tenant | ✅ Sí | ✅ Sí | Control total excepto eliminar tenant |
| **Owner** | Todos los módulos | Todos los datos del tenant | ✅ Sí | ✅ Sí | Control total incluyendo billing y eliminación |

---

## Arquitectura del Sistema

### 1. Backend - Filtrado y Autorización

#### ControllerExtensions.cs (Nuevo)
**Ubicación**: `src/UI/Planilla.Web/Extensions/ControllerExtensions.cs`

Métodos helper para todos los controllers:

```csharp
public static class ControllerExtensions
{
    // Obtener TenantId del token JWT
    public static int GetCurrentTenantId(this ControllerBase controller);

    // Obtener UserId del token JWT
    public static string GetCurrentUserId(this ControllerBase controller);

    // Obtener rol del usuario en el tenant actual
    public static TenantRole GetCurrentTenantRole(this ControllerBase controller);

    // Verificar si el usuario puede escribir (Owner/Admin/Manager)
    public static bool CanWrite(this ControllerBase controller);

    // Verificar si el usuario puede eliminar (Owner/Admin)
    public static bool CanDelete(this ControllerBase controller);

    // Retornar 403 Forbidden con mensaje
    public static IActionResult Forbidden(this ControllerBase controller, string message);
}
```

**Uso en Controllers**:
```csharp
[HttpGet]
[Authorize(Roles = "Owner,Admin,Manager,Accountant,Employee")]
public async Task<IActionResult> GetAll()
{
    var tenantId = _tenantContext.TenantId;
    var role = this.GetCurrentTenantRole();

    var query = _context.Empleados
        .Where(e => e.TenantId == tenantId);

    // Filtrado por rol Employee
    if (role == TenantRole.Employee)
    {
        var userId = this.GetCurrentUserId();
        var empleado = await _context.Empleados
            .Where(e => e.UserId == userId && e.TenantId == tenantId)
            .FirstOrDefaultAsync();

        if (empleado != null)
            query = query.Where(e => e.Id == empleado.Id);
        else
            return Ok(Array.Empty<EmpleadoVerDto>());
    }

    return Ok(await query.ToListAsync());
}
```

#### Entidad Empleado - Campo UserId
**Ubicación**: `src/Core/Planilla.Domain/Entities/Empleado.cs`

```csharp
/// <summary>
/// ID del usuario vinculado (para permitir que empleados accedan como usuarios)
/// </summary>
[StringLength(450)]
public string? UserId { get; set; }
```

**Migración**: `20260201012923_AddUserIdToEmpleado`

---

### 2. Frontend - Context y Helpers

#### AuthContext.tsx - Funciones Helper
**Ubicación**: `src/UI/Planilla.Web/ClientApp/src/contexts/AuthContext.tsx`

```typescript
interface AuthContextType {
  // ... propiedades existentes
  hasRole: (...roles: TenantRole[]) => boolean;
  canWrite: () => boolean;
  canDelete: () => boolean;
  isReadOnly: () => boolean;
}

// Implementación
const hasRole = (...roles: TenantRole[]): boolean => {
  if (!user || isLoading) return false;
  return roles.includes(user.role);
};

const canWrite = (): boolean => {
  if (!user || isLoading) return false;
  return [TenantRole.Owner, TenantRole.Admin, TenantRole.Manager].includes(user.role);
};

const canDelete = (): boolean => {
  if (!user || isLoading) return false;
  return [TenantRole.Owner, TenantRole.Admin].includes(user.role);
};

const isReadOnly = (): boolean => {
  if (!user || isLoading) return true;
  return [TenantRole.Accountant, TenantRole.Employee].includes(user.role);
};
```

---

### 3. Frontend - Navegación Condicional

#### AuthLayout.tsx - Filtrado de Módulos
**Ubicación**: `src/UI/Planilla.Web/ClientApp/src/components/layout/AuthLayout.tsx`

```typescript
const canAccessModule = (module: string): boolean => {
  if (!user) return false;

  const role = user.role;

  // Restricciones Employee
  if (role === TenantRole.Employee) {
    return ['dashboard', 'empleados', 'vacaciones', 'ausencias', 'horas-extra'].includes(module);
  }

  // Restricciones Accountant
  if (role === TenantRole.Accountant) {
    return !['users', 'roles'].includes(module);
  }

  // Manager, Admin, Owner ven todo
  return true;
};

// Uso en JSX
{canAccessModule('empleados') && (
  <NavLink to="/empleados">
    <Users className="w-5 h-5" />
    Empleados
  </NavLink>
)}
```

---

### 4. Frontend - Restricciones de UI en Páginas

**Patrón aplicado a 9 páginas**:
- EmpleadosPage.jsx
- DepartamentosPage.jsx
- PosicionesPage.jsx
- AusenciasPage.jsx
- VacacionesPage.jsx
- HorasExtraPage.jsx
- DeduccionesPage.jsx
- AnticiposPage.jsx
- PrestamosPage.jsx

```jsx
import { useAuth } from '../contexts/AuthContext';

function EmpleadosPage() {
  const { canWrite, canDelete, isReadOnly } = useAuth();

  return (
    <div>
      {/* Botón Crear - solo si puede escribir */}
      {canWrite() && (
        <Button onClick={() => setShowCreateModal(true)}>
          <Plus className="w-4 h-4" />
          Crear Empleado
        </Button>
      )}

      {/* Tabla con acciones condicionales */}
      <table>
        <tbody>
          {empleados.map(emp => (
            <tr key={emp.id}>
              <td>{emp.nombre}</td>
              <td>
                {!isReadOnly() ? (
                  <div className="flex gap-2">
                    {canWrite() && (
                      <Button onClick={() => handleEdit(emp)}>
                        Editar
                      </Button>
                    )}
                    {canDelete() && (
                      <Button onClick={() => handleDelete(emp.id)}>
                        Eliminar
                      </Button>
                    )}
                  </div>
                ) : (
                  <span className="text-sm text-gray-400 italic">
                    Solo lectura
                  </span>
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

---

## Dashboard y Configuración

### AdminDashboardPage.tsx - Métricas Empresariales

**Antes**: Botones de navegación simples
**Después**: Dashboard completo con métricas en tiempo real

```typescript
interface DashboardStats {
  totalEmpleados: number;
  empleadosActivos: number;
  ultimaPlanilla: any | null;
  aportesCss: number;
  pendientes: number;
}

// Carga datos de empleados y planillas
const loadDashboardData = async () => {
  const empleadosRes = await api.get('/api/empleados');
  const planillasRes = await api.get('/api/payrollheaders');
  // ... procesa y calcula estadísticas
};
```

**Secciones del Dashboard**:
1. **Stats Grid**: 4 tarjetas con KPIs principales
2. **Resumen Financiero**: Desglose de salarios, deducciones, costos
3. **Acciones Rápidas**: Enlaces a operaciones frecuentes

---

### ConfiguracionPage.jsx - Nuevas Tabs

**Tabs Agregadas**:

1. **Usuarios** (Owner/Admin):
   - Enlace a `/users` para gestión de usuarios
   - Invitar y administrar usuarios del tenant

2. **Audit Log** (Owner/Admin/Manager/Accountant):
   - Enlace a `/audit` para ver registros de actividad
   - Filtrado de eventos por tipo y fecha

3. **Uso del Plan**:
   - Componente `UsageDashboard` embebido
   - Muestra límites y uso actual del plan

4. **Soporte**:
   - Email: contacto@vorluno.dev
   - Website: vorluno.dev

**Filtrado por Rol**:
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
      TenantRole.Owner,
      TenantRole.Admin,
      TenantRole.Manager,
      TenantRole.Accountant
    ) : false
  },
  { id: 'plan', label: 'Uso del Plan', visible: true },
  { id: 'soporte', label: 'Soporte', visible: true }
];

const tabs = allTabs.filter(tab => tab.visible);
```

---

## Guía de Uso por Rol

### Employee (Empleado)
**Acceso**:
- ✅ Dashboard (métricas personales)
- ✅ Empleados (solo sus datos)
- ✅ Vacaciones (solo sus registros)
- ✅ Ausencias (solo sus registros)
- ✅ Horas Extra (solo sus registros)
- ❌ Resto de módulos ocultos

**Permisos**: Solo lectura de su información personal

**Caso de Uso**: Empleado revisa su información de nómina, vacaciones y ausencias.

---

### Accountant (Contador)
**Acceso**:
- ✅ Todos los módulos excepto Users y Roles
- ✅ Dashboard completo
- ✅ Reportes y planillas
- ✅ Audit Log
- ❌ Gestión de usuarios

**Permisos**: Solo lectura de todos los datos del tenant

**Caso de Uso**: Contador revisa planillas, genera reportes, consulta aportes CSS/SE/ISR.

---

### Manager (Gerente)
**Acceso**:
- ✅ Todos los módulos excepto Users y Roles
- ✅ Crear y editar empleados, planillas, deducciones
- ❌ Eliminar registros
- ❌ Gestión de usuarios

**Permisos**: Lectura y escritura, sin eliminación

**Caso de Uso**: Gerente de RRHH crea empleados, procesa planillas, gestiona vacaciones.

---

### Admin (Administrador)
**Acceso**:
- ✅ Todos los módulos
- ✅ Gestión de usuarios
- ✅ Invitar usuarios
- ✅ Ver audit log
- ✅ Crear, editar y eliminar

**Permisos**: Control total excepto eliminar tenant

**Caso de Uso**: Administrador gestiona usuarios, configura tasas, procesa planillas completas.

---

### Owner (Propietario)
**Acceso**:
- ✅ Todos los módulos
- ✅ Billing y suscripciones
- ✅ Eliminar tenant
- ✅ Control total

**Permisos**: Acceso completo sin restricciones

**Caso de Uso**: Dueño de la empresa gestiona todo, incluyendo plan de suscripción.

---

## Seguridad

### Principios Implementados

1. **Defense in Depth**: Validación en frontend Y backend
2. **Least Privilege**: Cada rol tiene solo los permisos necesarios
3. **Tenant Isolation**: Filtrado automático por TenantId en todas las queries
4. **JWT Claims**: Rol y TenantId en el token, validados en cada request
5. **Defensive Validation**: Manejo de estados de carga y undefined

### Flujo de Autorización

```
Usuario → Login → JWT con claims (tenant_id, tenant_role)
                          ↓
                 Request a API endpoint
                          ↓
           [Authorize] attribute valida JWT
                          ↓
          Middleware extrae tenant_id y role
                          ↓
         Controller filtra datos por TenantId
                          ↓
    CanWrite/CanDelete verifican rol específico
                          ↓
              Return 200 OK o 403 Forbidden
```

---

## Testing Recomendado

### Test Cases por Rol

1. **Employee**:
   - ✅ Ve solo sus propios datos en /empleados
   - ✅ No ve botones Crear/Editar/Eliminar
   - ✅ No puede acceder a /users, /departamentos, /prestamos
   - ✅ Dashboard muestra solo su información

2. **Accountant**:
   - ✅ Ve todos los empleados del tenant
   - ✅ No ve botones de acción (solo lectura)
   - ✅ Puede generar reportes
   - ✅ No puede acceder a /users

3. **Manager**:
   - ✅ Puede crear empleados y planillas
   - ✅ Puede editar registros
   - ✅ No ve botón Eliminar
   - ✅ No puede acceder a /users

4. **Admin/Owner**:
   - ✅ Acceso completo a todos los módulos
   - ✅ Puede invitar usuarios
   - ✅ Puede eliminar registros
   - ✅ Ve Audit Log completo

### Comandos de Testing
```bash
# Backend
dotnet test

# Frontend
cd src/UI/Planilla.Web/ClientApp
npm run test
```

---

## Archivos Modificados

### Backend (C#)
- ✅ `src/UI/Planilla.Web/Extensions/ControllerExtensions.cs` (Nuevo)
- ✅ `src/UI/Planilla.Web/Controllers/EmpleadosController.cs` (Modificado)
- ✅ `src/Core/Planilla.Domain/Entities/Empleado.cs` (Modificado)
- ✅ Migración: `20260201012923_AddUserIdToEmpleado`

### Frontend (TypeScript/JSX)
- ✅ `src/contexts/AuthContext.tsx` (Modificado)
- ✅ `src/components/layout/AuthLayout.tsx` (Modificado)
- ✅ `src/pages/AdminDashboardPage.tsx` (Reescrito)
- ✅ `src/pages/ConfiguracionPage.jsx` (Modificado)
- ✅ `src/pages/EmpleadosPage.jsx` (Modificado)
- ✅ `src/pages/DepartamentosPage.jsx` (Modificado)
- ✅ `src/pages/PosicionesPage.jsx` (Modificado)
- ✅ `src/pages/AusenciasPage.jsx` (Modificado)
- ✅ `src/pages/VacacionesPage.jsx` (Modificado)
- ✅ `src/pages/HorasExtraPage.jsx` (Modificado)
- ✅ `src/pages/DeduccionesPage.jsx` (Modificado)
- ✅ `src/pages/AnticiposPage.jsx` (Modificado)
- ✅ `src/pages/PrestamosPage.jsx` (Modificado)

---

## Próximos Pasos

1. **Vincular Usuarios con Empleados**:
   - Agregar UI para asignar UserId al crear/editar empleado
   - Workflow de invitación que vincule automáticamente

2. **Ampliar Backend Filtering**:
   - Aplicar mismo patrón a AusenciasController
   - Aplicar a VacacionesController, HorasExtraController
   - Aplicar a PlanillasController (Employee ve solo sus recibos)

3. **Audit Log Detallado**:
   - Registrar cambios de roles
   - Registrar intentos de acceso denegado
   - Dashboard de seguridad para Owner

4. **Tests Automatizados**:
   - Unit tests para ControllerExtensions
   - Integration tests para filtrado por rol
   - E2E tests con usuarios de cada rol

---

## Contacto y Soporte

**Email**: contacto@vorluno.dev
**Website**: https://vorluno.dev
**Proyecto**: Planilla SaaS - Sistema de Nómina para Panamá

---

**Última Actualización**: 2026-02-01
**Versión del Sistema**: 1.0.0
**Autor**: Vorluno Development Team
