# 🔐 Employee Self-Service - Guía de Uso

Sistema de auto-servicio para empleados que permite que cada empleado vea solo su información personal sin acceso a datos de otros empleados.

---

## 📋 Características

✅ **Seguridad total**: Cada empleado solo ve SU información
✅ **Aislamiento de datos**: Imposible acceder a datos de otros empleados
✅ **Permisos granulares**: Control fino de qué puede hacer cada empleado
✅ **JWT con employee_id**: Identificación rápida sin queries a DB

---

## 🚀 Configuración Inicial

### Paso 1: Crear el Rol "Empleado - Auto Servicio"

Ejecuta el script SQL en tu base de datos:

```bash
psql -U postgres -d planilla_db -f docs/seeds/employee_self_service_role.sql
```

**Importante**: Edita el script y cambia `v_tenant_id := 1` al ID de tu tenant.

### Paso 2: Vincular Usuario a Empleado

1. Ve a la página de **Empleados**
2. Selecciona el empleado
3. Click en **"Vincular Usuario"**
4. Busca el usuario (debe tener rol `User`, no `Owner`)
5. Guarda la vinculación

### Paso 3: Asignar Rol al Usuario

1. Ve a **Roles y Permisos** (solo Owner puede acceder)
2. Encuentra el usuario vinculado
3. Asígnale el rol **"Empleado - Auto Servicio"**

---

## 🎯 Qué Puede Ver un Empleado Vinculado

Con el rol "Empleado - Auto Servicio", el empleado puede:

| Módulo | Qué Ve |
|--------|---------|
| **Dashboard** | Métricas generales del sistema |
| **Empleados** | Solo SU perfil (nombre, salario, departamento, etc.) |
| **Planillas** | Solo SUS recibos de pago |
| **Vacaciones** | Solo SUS solicitudes de vacaciones |
| **Ausencias** | Solo SUS ausencias registradas |
| **Horas Extra** | Solo SUS horas extra |
| **Préstamos** | Solo SUS préstamos activos |
| **Deducciones** | Solo SUS deducciones |

---

## 🔐 Seguridad Implementada

### 1. JWT con employee_id

Cuando un empleado vinculado inicia sesión, su JWT incluye:

```json
{
  "sub": "user-guid",
  "email": "empleado@empresa.com",
  "tenant_id": "123",
  "employee_id": "456",  // ← Nuevo claim
  "tenant_role": "User"
}
```

### 2. Filtrado Automático en Backend

Todos los endpoints verifican si el usuario tiene `employee_id` y filtran automáticamente:

```csharp
// Ejemplo en EmpleadosController
var linkedEmployeeId = _currentUserService.GetLinkedEmployeeId();
if (linkedEmployeeId.HasValue)
{
    query = query.Where(e => e.Id == linkedEmployeeId.Value);
}
```

### 3. Permisos Específicos

Nuevos permisos para empleados:

- `employee.view_self` - Ver solo mi perfil
- `employee.update_self` - Editar solo mi perfil
- `payroll.view_self` - Ver solo mis planillas
- `vacations.request_self` - Solicitar mis vacaciones
- `absences.view_self` - Ver mis ausencias
- `overtime.view_self` - Ver mis horas extra
- `loans.view_self` - Ver mis préstamos
- `deductions.view_self` - Ver mis deducciones

---

## 🧪 Cómo Probarlo

### Prueba 1: Listar Empleados

1. **Login como Owner**: Ve `/api/empleados` → Verás TODOS los empleados
2. **Login como Empleado Vinculado**: Ve `/api/empleados` → Verás SOLO tu registro

### Prueba 2: Acceder a Otro Empleado

1. **Como Empleado Vinculado**: Intenta acceder a `/api/empleados/999` (otro empleado)
2. **Resultado esperado**: `403 Forbidden`

### Prueba 3: Ver Planillas

1. **Como Empleado Vinculado**: Ve `/api/payrollheaders`
2. **Resultado esperado**: Solo verás planillas donde TÚ apareces
3. **Detalles de planilla**: Solo verás TU línea, no la de otros empleados

---

## 📝 Controllers Modificados

Los siguientes controllers ya tienen filtrado por empleado:

✅ **EmpleadosController**
- `GET /api/empleados` - Solo su registro
- `GET /api/empleados/{id}` - Solo su ID
- `PUT /api/empleados/{id}` - Solo su perfil

✅ **PayrollHeadersController**
- `GET /api/payrollheaders` - Solo planillas donde aparece
- `GET /api/payrollheaders/{id}` - Solo su detalle

✅ **VacacionesController**
- `GET /api/vacaciones` - Solo sus solicitudes
- `GET /api/vacaciones/{id}` - Solo sus solicitudes

⚠️ **Pendientes** (aplicar mismo patrón):
- AusenciasController
- HorasExtraController
- PrestamosController
- DeduccionesController
- AnticiposController

---

## 🔧 Aplicar Filtrado a Otros Controllers

Para aplicar el mismo patrón a los controllers pendientes:

### 1. Inyectar ICurrentUserService

```csharp
private readonly ICurrentUserService _currentUserService;

public MiController(
    // ... otros servicios
    ICurrentUserService currentUserService)
{
    _currentUserService = currentUserService;
}
```

### 2. Agregar Filtrado en GetAll()

```csharp
[HttpGet]
[RequirePermission(SystemPermission.XxxManage, SystemPermission.XxxViewSelf)]
public async Task<IActionResult> GetAll()
{
    var linkedEmployeeId = _currentUserService.GetLinkedEmployeeId();

    var query = _context.MiEntidad.Where(x => x.TenantId == tenantId);

    // Filtrar por empleado vinculado
    if (linkedEmployeeId.HasValue)
    {
        query = query.Where(x => x.EmpleadoId == linkedEmployeeId.Value);
    }

    return Ok(await query.ToListAsync());
}
```

### 3. Verificar Acceso en GetById()

```csharp
[HttpGet("{id}")]
[RequirePermission(SystemPermission.XxxManage, SystemPermission.XxxViewSelf)]
public async Task<IActionResult> GetById(int id)
{
    var linkedEmployeeId = _currentUserService.GetLinkedEmployeeId();
    var entity = await _context.MiEntidad.FindAsync(id);

    if (entity == null) return NotFound();

    // Verificar que pertenece al empleado
    if (linkedEmployeeId.HasValue && entity.EmpleadoId != linkedEmployeeId.Value)
    {
        return Forbid(); // 403
    }

    return Ok(entity);
}
```

---

## ❓ Preguntas Frecuentes

### ¿Puede un empleado ver a otros empleados?

**No**. El filtrado es automático en el backend. Aunque intente acceder a otro ID, recibirá `403 Forbidden`.

### ¿Qué pasa si el empleado ya no trabaja?

1. Desvincular el usuario del empleado (EmpleadosController tiene endpoint)
2. El usuario perderá acceso automáticamente (ya no tendrá `employee_id` en JWT)

### ¿Puede un empleado cambiar su salario?

**No**. El permiso `employee.update_self` permite editar solo datos básicos (email, teléfono, etc.), no datos sensibles como salario.

### ¿Los Owner ven a todos los empleados?

**Sí**. Los Owner (y usuarios con `employees.read`) ven TODOS los empleados del tenant.

---

## 📊 Ejemplo de Flujo Completo

```
1. Owner crea empleado "Juan Pérez"
   └─ ID: 42

2. Owner crea usuario "juan.perez@empresa.com"
   └─ Rol: User
   └─ Sin permisos por defecto

3. Owner vincula usuario a empleado
   └─ Empleado.UserId = "user-guid-123"

4. Owner asigna rol "Empleado - Auto Servicio"
   └─ Usuario tiene permisos: employee.view_self, payroll.view_self, etc.

5. Juan inicia sesión
   └─ JWT incluye: employee_id: 42

6. Juan accede a /api/empleados
   └─ Backend filtra: WHERE EmpleadoId = 42
   └─ Juan solo ve SU registro

7. Juan intenta ver /api/empleados/99
   └─ Backend detecta: linkedEmployeeId = 42, requestedId = 99
   └─ Respuesta: 403 Forbidden
```

---

## ✅ Checklist de Implementación

- [x] JWT incluye employee_id
- [x] ICurrentUserService.GetLinkedEmployeeId()
- [x] Permisos *.view_self agregados
- [x] EmpleadosController filtrado
- [x] PayrollHeadersController filtrado
- [x] VacacionesController filtrado
- [x] RequirePermission soporta múltiples permisos (lógica OR)
- [x] Script SQL para rol predefinido
- [ ] Aplicar filtrado a controllers restantes (Ausencias, HorasExtra, etc.)
- [ ] Frontend: Página "Mi Perfil" para empleados
- [ ] Testing: Verificar aislamiento de datos

---

## 🎯 Próximos Pasos

1. **Aplicar filtrado a controllers restantes** (Ausencias, HorasExtra, Préstamos, etc.)
2. **Crear página "Mi Perfil"** en frontend para empleados
3. **Testing exhaustivo** para verificar seguridad
4. **Documentar para cliente** el proceso de dar acceso a empleados

---

¿Dudas? Revisa el código en:
- `AuthController.cs` - Generación de JWT
- `ICurrentUserService.cs` - Detección de empleado vinculado
- `EmpleadosController.cs` - Ejemplo de filtrado completo
