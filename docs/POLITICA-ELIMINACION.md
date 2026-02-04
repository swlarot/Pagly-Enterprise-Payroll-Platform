# Política de eliminación de datos

Este documento describe la política de **eliminación física** (hard delete) adoptada en Planilla y su implementación en cada área del sistema.

---

## 1. Principio general

**Regla:** *"Si existe es para que se elimine"*.

- Las acciones de **eliminar** en la aplicación realizan **eliminación física**: el registro se borra de la base de datos.
- Los elementos eliminados **no deben aparecer en ninguna lista**; al eliminarlos, dejan de existir en el sistema.
- Se evita la mezcla de eliminaciones lógicas (marcar como inactivo/eliminado) con eliminaciones físicas, salvo donde las dependencias de datos lo exigen (ver excepciones).

---

## 2. Resumen por área

| Área | Comportamiento | Ubicación |
|------|----------------|-----------|
| Remover usuario del tenant | Hard delete + desvincular empleados | `TenantManagementService.RemoveTenantUserAsync` |
| Empleados (lista) | Filtro `!IsDeleted`; eliminados no se listan | `EmpleadosController.GetAll`, `GetById` |
| Empleados (ForceDelete) | **Soft delete** (FKs complejas) | `EmpleadosController.ForceDelete` |
| Rol personalizado | Hard delete | `CustomRolesController.Delete` |
| Departamento | Hard delete (desasigna posiciones antes) | `DepartamentosController.Delete` |
| Posición | Hard delete (desasigna empleados antes) | `PosicionesController.Delete` |
| Deducción fija | Hard delete | `DeduccionesController.Desactivar` |
| Invitación (revocar) | Hard delete | `InvitationService.RevokeInvitationAsync` |

---

## 3. Detalle por área

### 3.1 Remover usuario del tenant

- **Servicio:** `Planilla.Infrastructure/Services/TenantManagementService.cs` → `RemoveTenantUserAsync`.
- **Comportamiento:**
  1. Se buscan todos los `Empleado` del mismo tenant con `UserId == tenantUser.UserId`.
  2. Se desvincula cada empleado: `Empleado.UserId = null` (el empleado permanece, sin usuario asignado).
  3. Se registra auditoría.
  4. Se elimina físicamente el registro `TenantUser`.
- **Motivo de desvincular:** Al eliminar un usuario del tenant no se elimina a los empleados; solo se quita la asociación para que el empleado siga existiendo y pueda asignarse a otro usuario después.

### 3.2 Empleados

- **Controlador:** `Planilla.Web/Controllers/EmpleadosController.cs`.
- **Listas:** En `GetAll()` y `GetById()` se filtra por `!e.IsDeleted` para que los empleados eliminados no aparezcan.
- **Recuentos:** En `PlanUsageService`, `PlanLimitService` y recuentos de admin se cuentan solo empleados con `!e.IsDeleted`.
- **ForceDelete:** Sigue siendo **soft delete** (`IsDeleted = true`) por dependencias de claves foráneas (planillas, detalles, historial). La eliminación física requeriría definir cascadas o limpieza de datos históricos; por eso se mantiene esta excepción documentada en código.

### 3.3 Rol personalizado

- **Controlador:** `Planilla.Web/Controllers/CustomRolesController.cs` → acción `Delete`.
- **Comportamiento:** Eliminación física del rol (entidad correspondiente en el contexto). Los permisos asociados se gestionan según la configuración del DbContext (cascada o eliminación explícita según el diseño actual).

### 3.4 Departamento

- **Controlador:** `Planilla.Web/Controllers/DepartamentosController.cs` → acción `Delete`.
- **Comportamiento:**
  1. Si el departamento tiene posiciones, se devuelve `BadRequest` (o se desasignan las posiciones según la implementación actual).
  2. Se desasignan las posiciones del departamento (`Posicion.DepartamentoId = null`).
  3. Se elimina físicamente el `Departamento`.

### 3.5 Posición

- **Controlador:** `Planilla.Web/Controllers/PosicionesController.cs` → acción `Delete`.
- **Comportamiento:**
  1. Se desasignan los empleados que tenían esta posición (`Empleado.PosicionId = null`).
  2. Se elimina físicamente la `Posicion`.

### 3.6 Deducción fija

- **Controlador:** `Planilla.Web/Controllers/DeduccionesController.cs` → acción `Desactivar` (ruta `DELETE /api/deducciones/{id}`).
- **Comportamiento:** Eliminación física del registro en `DeduccionesFijas`. No hay FK desde otras tablas; `PayrollDetail.DeduccionesFijas` es un monto calculado, no una relación.

### 3.7 Invitación (revocar)

- **Servicio:** `Planilla.Infrastructure/Services/InvitationService.cs` → `RevokeInvitationAsync`.
- **Comportamiento:**
  1. Validaciones (tenant, permisos, invitación no aceptada).
  2. Registro de auditoría "InviteRevoked".
  3. Eliminación física del registro `TenantInvitation`.
- Las invitaciones revocadas dejan de existir y no aparecen en listados.

---

## 4. Excepciones a la eliminación física

- **Empleados (ForceDelete):** Se mantiene soft delete por las muchas FKs (planillas, detalles, reportes, etc.). La eliminación física implicaría políticas de cascada o borrado de historial; por ahora solo se ocultan con `!IsDeleted` en listas y recuentos.

Cualquier otra excepción futura debe documentarse aquí y en el código.

---

## 5. Auditoría

Donde aplica, el **audit log** se registra **antes** de la eliminación física, incluyendo identificador y datos relevantes (por ejemplo email y rol en invitaciones), para conservar trazabilidad aunque el registro ya no exista.

---

## 6. Referencia rápida de archivos

| Archivo | Responsabilidad |
|---------|-----------------|
| `Infrastructure/Services/TenantManagementService.cs` | Remover usuario del tenant, desvincular empleados, hard delete TenantUser |
| `Infrastructure/Services/InvitationService.cs` | Revocar invitación, hard delete TenantInvitation |
| `Web/Controllers/EmpleadosController.cs` | Lista/GetById con `!IsDeleted`, ForceDelete (soft) |
| `Web/Controllers/CustomRolesController.cs` | Eliminar rol (hard delete) |
| `Web/Controllers/DepartamentosController.cs` | Eliminar departamento (hard delete) |
| `Web/Controllers/PosicionesController.cs` | Eliminar posición (hard delete) |
| `Web/Controllers/DeduccionesController.cs` | Eliminar deducción fija (hard delete) |

---

*Última actualización: febrero 2025.*
