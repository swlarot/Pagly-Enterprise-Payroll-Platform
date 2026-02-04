# Análisis de Impacto y Recomendaciones: Eliminación de Usuarios y Empleados
## Sistema Planilla SaaS - Cumplimiento Normativo Panamá

**Fecha**: 2026-02-01
**Versión**: 1.0
**Autor**: PlanillaFunctionalArchitect

---

## EXECUTIVE SUMMARY

Este documento analiza el impacto funcional, técnico y legal de implementar funcionalidades de eliminación de usuarios y empleados en el sistema Planilla, un SaaS de nómina para empresas panameñas. Se proporcionan recomendaciones basadas en:

- **Regulaciones laborales panameñas** (Código de Trabajo, CSS, MITRADEL, DGI)
- **Arquitectura multi-tenant SaaS** (aislamiento de datos, seguridad)
- **Mejores prácticas de retención de datos** (compliance, auditoría)
- **Integridad referencial** (relaciones complejas entre entidades)

---

## 1. ANÁLISIS DE IMPACTO: ELIMINACIÓN DE EMPLEADOS

### 1.1 Dependencias Identificadas

Al eliminar un **Empleado**, las siguientes entidades están afectadas:

#### A. Historial de Planillas (CRÍTICO - NO ELIMINAR)
```
Empleado (1) ───< (N) PayrollDetail
                        │
                        └─── PayrollHeader (status: Approved/Paid)
```

**Impacto:**
- Recibos de sueldo históricos
- Cálculos CSS, SE, ISR por período
- Deducciones aplicadas (préstamos, anticipos)
- Horas extra pagadas
- Ausencias procesadas

**Regulación Panameña:**
- **CSS (Caja de Seguro Social)**: Retención mínima de **5 años** de planillas CSS
- **MITRADEL (Ministerio de Trabajo)**: Retención de **4 años** de registros de empleados
- **DGI (Dirección General de Ingresos)**: Retención de **7 años** para ISR

**RECOMENDACIÓN:** **NEVER DELETE** - Los `PayrollDetail` son registros fiscales permanentes.

---

#### B. Préstamos y Pagos (CRÍTICO - REGULATORIO)
```
Empleado (1) ───< (N) Prestamo
                        │
                        └───< PagoPrestamo
```

**Impacto:**
- Préstamos activos (con saldo pendiente)
- Préstamos pagados (historial completo)
- Cuotas programadas vs pagadas
- Referencias en planillas (descuentos aplicados)

**Casos de uso:**
1. **Préstamo activo**: Empleado renuncia con deuda pendiente
2. **Préstamo pagado**: Historial para auditoría interna
3. **Cuota pendiente**: Vinculada a planilla futura

**RECOMENDACIÓN:**
- **SOFT DELETE** para empleado
- **PRESERVAR** todos los registros de préstamos (activos y pagados)
- **VALIDACIÓN**: Bloquear eliminación física si hay préstamos activos

---

#### C. Deducciones Fijas (CRÍTICO - LEGAL)
```
Empleado (1) ───< (N) DeduccionFija
```

**Tipos críticos:**
- **Pensión alimenticia**: Orden judicial (expediente)
- **Embargos judiciales**: Mandato de autoridad
- **Seguros privados**: Contrato vigente
- **Aportes sindicales**: Obligación legal (si aplica)

**Regulación Panameña:**
- **Código de Familia**: Retención de pensiones alimenticias es obligatoria e indelegable
- Las deducciones judiciales tienen **referencia de expediente** que debe preservarse

**RECOMENDACIÓN:**
- **SOFT DELETE** para empleado
- **PRESERVAR** deducciones con referencias judiciales (Referencia != null)
- **ADVERTENCIA**: Notificar al tenant si el empleado tiene deducciones judiciales activas

---

#### D. Anticipos de Salario (MEDIANO IMPACTO)
```
Empleado (1) ───< (N) Anticipo
                        │
                        └─ Estado: Pendiente | Aprobado | Descontado | Rechazado
```

**Estados críticos:**
- **Aprobado**: Ya fue entregado el dinero, pero no descontado
- **Descontado**: Ya se descontó en planilla (PlanillaId != null)

**RECOMENDACIÓN:**
- **VALIDACIÓN**: Bloquear eliminación si hay anticipos aprobados pendientes de descuento
- **PRESERVAR**: Anticipos descontados (vinculados a planillas)

---

#### E. Horas Extra (MEDIANO IMPACTO - FISCAL)
```
Empleado (1) ───< (N) HoraExtra
                        │
                        └─ EstaAprobada: true/false
                        └─ PlanillaDetailId: null (pendiente) | ID (pagada)
```

**Impacto fiscal:**
- Las horas extra **pagadas** forman parte del salario bruto gravable para ISR
- La CSS y SE se calculan sobre el salario **incluyendo horas extra**

**RECOMENDACIÓN:**
- **SOFT DELETE** empleado
- **PRESERVAR** horas extra pagadas (PlanillaDetailId != null)
- **VALIDACIÓN**: Advertir si hay horas extra aprobadas pendientes de pago

---

#### F. Ausencias (BAJO IMPACTO - OPERATIVO)
```
Empleado (1) ───< (N) Ausencia
                        │
                        └─ AfectaSalario: true/false
                        └─ PlanillaDetailId: null (no procesada) | ID (descontada)
```

**RECOMENDACIÓN:**
- **SOFT DELETE** empleado
- **PRESERVAR** ausencias procesadas (descontadas en planilla)
- **ELIMINAR**: Ausencias pendientes (no procesadas aún)

---

#### G. Vinculación con Usuario (CRÍTICO - ACCESO AL SISTEMA)
```
Empleado (1) ─── (0..1) AppUser
                           │
                           └─ UserId: string? (nullable)
```

**Casos de uso:**
- Empleado **con** usuario: Puede acceder al sistema (rol Employee)
- Empleado **sin** usuario: Solo registro de nómina

**RECOMENDACIÓN:**
- **AL ELIMINAR EMPLEADO**:
  - Si tiene UserId, **desvincular** (UserId = null) pero NO eliminar el usuario
  - El usuario puede seguir existiendo en el tenant con otro rol
- **AL ELIMINAR USUARIO**:
  - Si está vinculado a empleado, **desvincular** pero NO eliminar el empleado

---

#### H. Departamento y Posición (BAJO IMPACTO)
```
Empleado (N) ───> (1) Departamento
Empleado (N) ───> (1) Posicion
```

**RECOMENDACIÓN:**
- **NO AFECTA**: El empleado simplemente pierde referencia
- **VALIDACIÓN**: Permitir soft delete sin restricción

---

### 1.2 Recomendación Final: EMPLEADOS

#### ESTRATEGIA: SOFT DELETE con Validaciones

```csharp
public class Empleado
{
    public bool EstaActivo { get; set; } = true;  // YA EXISTE

    // AGREGAR CAMPOS:
    public bool IsDeleted { get; set; } = false;
    public DateTime? DeletedAt { get; set; }
    public string? DeletedBy { get; set; }  // UserId del actor
    public string? DeletionReason { get; set; }  // Razón: Renuncia, Despido, Fin de contrato
}
```

#### VALIDACIONES ANTES DE ELIMINAR

1. **BLOQUEAR** si hay:
   - Préstamos activos (Estado != Pagado)
   - Anticipos aprobados no descontados
   - Deducciones judiciales activas

2. **ADVERTIR** si hay:
   - Horas extra aprobadas pendientes de pago
   - Ausencias no procesadas
   - Planillas en estado Draft que incluyen al empleado

3. **PERMITIR** si:
   - Solo tiene registros históricos (todo ya procesado y pagado)
   - No tiene obligaciones pendientes

#### ENDPOINT PROPUESTO

```http
DELETE /api/empleados/{id}
Authorization: Bearer {token}
Content-Type: application/json

Request Body:
{
  "reason": "Renuncia voluntaria",
  "effectiveDate": "2026-01-31",
  "forceDelete": false  // Solo Owners pueden forzar
}

Response (200 OK):
{
  "success": true,
  "message": "Empleado Juan Pérez marcado como eliminado",
  "warnings": [
    "Empleado tiene 2 horas extra aprobadas pendientes de pago (B/. 45.00)",
    "Empleado aparece en planilla DRAFT del período 2026-01-16 al 2026-01-31"
  ]
}

Response (400 Bad Request):
{
  "success": false,
  "error": "No se puede eliminar el empleado",
  "blockers": [
    "Empleado tiene un préstamo activo con saldo pendiente de B/. 1,200.00",
    "Empleado tiene deducción judicial activa (Pensión alimenticia - Exp. 123-2024)"
  ]
}
```

#### ROLES Y PERMISOS

| Rol | Puede Eliminar | Puede Forzar | Notas |
|-----|----------------|--------------|-------|
| **Owner** | Sí | Sí | Puede forzar eliminación incluso con bloqueos |
| **Admin** | Sí | No | Respeta todas las validaciones |
| **Manager** | No | No | Solo puede desactivar (EstaActivo = false) |
| **Accountant** | No | No | Solo lectura |
| **Employee** | No | No | No tiene acceso |

---

## 2. ANÁLISIS DE IMPACTO: ELIMINACIÓN DE USUARIOS

### 2.1 Dependencias Identificadas

#### A. Membresías de Tenant (CRÍTICO - MULTI-TENANT)
```
AppUser (1) ───< (N) TenantUser
                       │
                       └─ Role: Owner | Admin | Manager | Accountant | Employee
```

**Casos críticos:**
- **Último Owner del tenant**: NO SE PUEDE ELIMINAR (tenant quedaría huérfano)
- **Multiple tenants**: Usuario puede pertenecer a varios tenants

**RECOMENDACIÓN:**
- **VALIDACIÓN**: Bloquear eliminación si es el único Owner de algún tenant
- **SOFT DELETE**: Marcar IsDeleted = true en AppUser
- **DESACTIVAR**: Todas las membresías TenantUser (IsActive = false)

---

#### B. Vinculación con Empleado (MEDIANO IMPACTO)
```
AppUser (1) ───< (0..N) Empleado
                         │
                         └─ UserId: string?
```

**RECOMENDACIÓN:**
- **DESVINCULAR**: Establecer Empleado.UserId = null
- **PRESERVAR**: El registro de empleado (historial de planillas)

---

#### C. Historial de Auditoría (CRÍTICO - COMPLIANCE)
```
AppUser.Id ───< AuditLogEntry.ActorUserId
```

**Impacto:**
- Todas las acciones del usuario en el sistema están registradas
- Logs de aprobación de planillas, modificaciones de empleados, etc.

**RECOMENDACIÓN:**
- **PRESERVAR**: Todos los logs de auditoría (no modificar ActorUserId)
- **SOFT DELETE ONLY**: Nunca hacer hard delete de usuarios con historial

---

#### D. Invitaciones Enviadas (BAJO IMPACTO)
```
AppUser (1) ───< (N) TenantInvitation.InvitedBy
```

**RECOMENDACIÓN:**
- **PRESERVAR**: Invitaciones históricas (campo InvitedBy no se modifica)

---

### 2.2 Recomendación Final: USUARIOS

#### ESTRATEGIA: SOFT DELETE con Protección de Owners

**YA IMPLEMENTADO** en `AdminController.cs` (líneas 1062-1160):

```csharp
public class AppUser : IdentityUser
{
    public bool IsDeleted { get; set; } = false;  // ✅ YA EXISTE
    public DateTime? DeletedAt { get; set; }      // ✅ YA EXISTE
    public string? DeletedBy { get; set; }        // ✅ YA EXISTE
}
```

#### VALIDACIONES ACTUALES (YA IMPLEMENTADAS)

1. **BLOQUEAR** si:
   - Es el último SystemAdmin del sistema
   - (FALTA) Es el último Owner de algún tenant

2. **AL ELIMINAR**:
   - Marca `IsDeleted = true`
   - Desactiva todas las membresías `TenantUser.IsActive = false`
   - Registra en audit log
   - **NO** elimina físicamente al usuario

#### MEJORAS RECOMENDADAS

**AGREGAR VALIDACIÓN: Último Owner de Tenant**

```csharp
// En AdminController.DeleteUser()
// DESPUÉS DE línea 1091

// Verificar si es Owner de algún tenant
var ownerships = await _context.TenantUsers
    .Where(tu => tu.UserId == userId && tu.Role == TenantRole.Owner && tu.IsActive)
    .ToListAsync();

foreach (var ownership in ownerships)
{
    var ownersCount = await _context.TenantUsers
        .Where(tu => tu.TenantId == ownership.TenantId
                  && tu.Role == TenantRole.Owner
                  && tu.IsActive
                  && tu.UserId != userId)
        .CountAsync();

    if (ownersCount == 0)
    {
        var tenant = await _context.Tenants.FindAsync(ownership.TenantId);
        return BadRequest(new
        {
            error = $"No se puede eliminar este usuario porque es el único Owner del tenant '{tenant.Name}'. " +
                    $"Asigne otro Owner antes de eliminar este usuario."
        });
    }
}
```

---

## 3. CASOS DE USO ADICIONALES: OTRAS ENTIDADES

### 3.1 Departamentos

#### Dependencias:
```
Departamento (1) ───< (N) Empleado
Departamento (1) ───< (N) Posicion
Departamento (1) ─── (0..1) Empleado (Manager)
```

#### RECOMENDACIÓN: SOFT DELETE

```csharp
// YA EXISTE: EstaActivo = true/false

// VALIDACIONES:
// - Bloquear si tiene empleados activos
// - Advertir si tiene posiciones activas
// - Permitir si todos los empleados están inactivos/eliminados
```

**Endpoint:**
```http
DELETE /api/departamentos/{id}

Blocker:
- "Departamento tiene 5 empleados activos. Reasigne o elimine los empleados primero."

Warning:
- "Departamento tiene 3 posiciones definidas. Considere reasignarlas a otro departamento."
```

---

### 3.2 Posiciones

#### Dependencias:
```
Posicion (1) ───< (N) Empleado
Posicion (N) ───> (1) Departamento
```

#### RECOMENDACIÓN: SOFT DELETE

```csharp
// YA EXISTE: EstaActivo = true/false

// VALIDACIONES:
// - Bloquear si tiene empleados activos
// - Permitir si todos los empleados están inactivos/eliminados
```

---

### 3.3 Préstamos

#### Dependencias:
```
Prestamo (1) ───< (N) PagoPrestamo
Prestamo (N) ───> (1) Empleado
```

#### RECOMENDACIÓN: NO PERMITIR ELIMINACIÓN

**Razón:** Registros financieros con implicaciones legales.

**Alternativa:**
- Cambiar estado a `Cancelado` o `Condonado`
- Preservar registro histórico completo

---

### 3.4 Deducciones Fijas

#### RECOMENDACIÓN: SOFT DELETE con Validación Judicial

```csharp
// VALIDACIONES:
// - Si TipoDeduccion == PensionAlimenticia: Advertir que es orden judicial
// - Si TipoDeduccion == EmbargoJudicial: Advertir que es mandato de autoridad
// - Requiere confirmación explícita del Owner/Admin
```

---

### 3.5 Planillas Completas (PayrollHeader)

#### RECOMENDACIÓN: **NO PERMITIR ELIMINACIÓN DIRECTA**

**Alternativa:** Proceso de **Anulación**

```
Estado: Draft → Calculated → Approved → Paid
                                          │
                                          └──> Anulado (nuevo estado)
```

**Flujo de anulación:**
1. Solo se puede anular una planilla en estado `Paid`
2. Requiere justificación (error de cálculo, duplicación, etc.)
3. Crea una **planilla de reversión** (PayrollHeader con valores negativos)
4. Registra en audit log
5. Preserva ambas planillas (original + reversión) para trazabilidad fiscal

**Razón:** Las planillas aprobadas y pagadas son **documentos fiscales** que deben preservarse según CSS, MITRADEL y DGI.

---

## 4. COMPLIANCE: RETENCIÓN DE DATOS EN PANAMÁ

### 4.1 Regulaciones Aplicables

| Entidad Reguladora | Documento | Período de Retención |
|-------------------|-----------|---------------------|
| **CSS** | Ley 51-2005 (Art. 47) | **5 años** - Planillas CSS |
| **MITRADEL** | Código de Trabajo (Art. 183) | **4 años** - Registros de empleados |
| **DGI** | Código Fiscal (Art. 708) | **7 años** - Documentos ISR |
| **Seguro Educativo** | Ley 106-1972 | **5 años** - Planillas SE |
| **Pensiones Alimenticias** | Código de Familia | **Indefinido** - Mientras exista obligación |

### 4.2 Período de Retención Recomendado: **7 AÑOS**

**Justificación:** Alineado con el período más largo (DGI - ISR).

### 4.3 Implementación de Retención

```csharp
public class PlanFeatures
{
    public static Dictionary<SubscriptionPlan, PlanLimits> Limits = new()
    {
        [SubscriptionPlan.Free] = new PlanLimits
        {
            RetentionDays = 90,  // 3 meses
        },
        [SubscriptionPlan.Starter] = new PlanLimits
        {
            RetentionDays = 365,  // 1 año
        },
        [SubscriptionPlan.Professional] = new PlanLimits
        {
            RetentionDays = 730,  // 2 años
        },
        [SubscriptionPlan.Enterprise] = new PlanLimits
        {
            RetentionDays = 2555,  // 7 años (compliance total)
        }
    };
}
```

**Proceso de purga automática:**
- Job nocturno que elimina registros **soft-deleted** más antiguos que el período de retención
- Excluir planillas (never delete)
- Excluir deducciones judiciales (legal obligation)
- Notificar al tenant antes de purgar (email 30 días antes)

---

## 5. CONSIDERACIONES DE SEGURIDAD Y AUDITORÍA

### 5.1 Registro Obligatorio en Audit Log

**Todos** los endpoints de eliminación deben registrar:

```csharp
var auditLog = new AuditLogEntry
{
    TenantId = tenantId,
    ActorUserId = currentUserId,
    ActorEmail = currentUserEmail,
    Action = "EmpleadoDeleted",  // o "UserDeleted", etc.
    EntityType = "Empleado",
    EntityId = empleadoId.ToString(),
    IpAddress = httpContext.Connection.RemoteIpAddress?.ToString(),
    UserAgent = httpContext.Request.Headers["User-Agent"],
    MetadataJson = JsonSerializer.Serialize(new
    {
        Reason = deletionReason,
        DeletedAt = DateTime.UtcNow,
        ForceDelete = wasForced,
        Warnings = warnings,
        Blockers = blockers
    }),
    CreatedAt = DateTime.UtcNow
};
```

### 5.2 Notificaciones por Email

**Enviar email a Owners del tenant cuando:**
- Se elimina un empleado con obligaciones legales pendientes
- Se elimina un usuario con rol Owner/Admin
- Se elimina el último empleado activo del tenant
- Se alcanza el límite de retención de datos (antes de purgar)

---

## 6. PROPUESTA DE IMPLEMENTACIÓN PRIORIZADA

### FASE 1: ELIMINACIÓN DE USUARIOS (2-3 días) - PRIORITARIO

**Ya implementado parcialmente** en `AdminController.cs`.

**Tareas pendientes:**
1. Agregar validación de "último Owner de tenant"
2. Implementar endpoint de desvinculación Empleado ↔ Usuario
3. Agregar endpoint de reactivación de usuario en tenant (ya existe a nivel sistema)
4. Testing exhaustivo de validaciones

**Archivos afectados:**
- `src/UI/Planilla.Web/Controllers/AdminController.cs` (modificar)
- `src/Core/Planilla.Application/DTOs/Admin/DeleteUserDto.cs` (crear)
- Frontend: `src/UI/Planilla.Web/ClientApp/src/pages/SystemUsersPage.tsx` (agregar botón delete)

---

### FASE 2: ELIMINACIÓN DE EMPLEADOS (5-7 días) - ALTA PRIORIDAD

**Componentes a implementar:**

1. **Backend: EmpleadosController**
   ```http
   DELETE /api/empleados/{id}
   POST /api/empleados/{id}/reactivate
   ```

2. **Backend: Servicio de validación**
   ```csharp
   public class EmpleadoValidationService
   {
       public Task<DeletionValidationResult> ValidateForDeletionAsync(int empleadoId);
   }
   ```

3. **Backend: DTOs**
   ```csharp
   public class DeleteEmpleadoDto
   {
       public string Reason { get; set; }
       public DateTime? EffectiveDate { get; set; }
       public bool ForceDelete { get; set; }
   }

   public class DeletionValidationResult
   {
       public bool CanDelete { get; set; }
       public List<string> Blockers { get; set; }
       public List<string> Warnings { get; set; }
   }
   ```

4. **Frontend: Modal de confirmación**
   - Mostrar blockers (errores que impiden eliminación)
   - Mostrar warnings (advertencias que permiten continuar)
   - Campo de razón (dropdown: Renuncia, Despido, Fin de contrato, Otro)
   - Checkbox "Entiendo las implicaciones" (si hay warnings)

**Archivos a crear/modificar:**
- `src/UI/Planilla.Web/Controllers/EmpleadosController.cs` (agregar DELETE endpoint)
- `src/Core/Planilla.Application/Services/EmpleadoValidationService.cs` (crear)
- `src/Core/Planilla.Application/DTOs/EmpleadoDtos.cs` (agregar DTOs)
- Frontend: `src/UI/Planilla.Web/ClientApp/src/pages/EmpleadosPage.jsx` (agregar botón/modal)

---

### FASE 3: SOFT DELETE DE DEPARTAMENTOS Y POSICIONES (2-3 días) - MEDIA PRIORIDAD

**Endpoints:**
```http
DELETE /api/departamentos/{id}
POST /api/departamentos/{id}/reactivate

DELETE /api/posiciones/{id}
POST /api/posiciones/{id}/reactivate
```

**Validaciones:**
- Departamento: Bloquear si tiene empleados activos
- Posición: Bloquear si tiene empleados activos

---

### FASE 4: SOFT DELETE DE CONCEPTOS (3-4 días) - BAJA PRIORIDAD

**Entidades:**
- Deducciones Fijas (con validación judicial)
- Anticipos (solo si estado = Pendiente o Rechazado)
- Horas Extra (solo si no están aprobadas)

**NO IMPLEMENTAR eliminación de:**
- Préstamos (cambiar estado a Cancelado/Condonado en su lugar)
- Planillas (implementar Anulación en su lugar)

---

### FASE 5: POLÍTICAS DE RETENCIÓN Y PURGA (3-5 días) - OPCIONAL

**Componentes:**
1. Job programado (cron): `DataRetentionJob.cs`
2. Servicio: `DataRetentionService.cs`
3. Notificaciones por email antes de purgar
4. Dashboard de retención en SystemAdmin panel

---

## 7. MATRIZ DE DECISIONES: SOFT vs HARD DELETE

| Entidad | Estrategia | Razón | Período Retención |
|---------|-----------|-------|-------------------|
| **Empleado** | Soft Delete | Historial de planillas (CSS, ISR) | 7 años |
| **AppUser** | Soft Delete | Audit log, multi-tenant | Indefinido |
| **TenantUser** | Soft Delete (IsActive) | Historial de membresía | Indefinido |
| **PayrollHeader** | **NO PERMITIR** | Documento fiscal | **Permanente** |
| **PayrollDetail** | **NO PERMITIR** | Recibos de sueldo | **Permanente** |
| **Prestamo** | **NO PERMITIR** | Registro financiero | Permanente |
| **DeduccionFija** | Soft Delete | Puede ser judicial | 7 años |
| **Anticipo** | Soft Delete | Si ya descontado | 2 años |
| **HoraExtra** | Soft Delete | Si ya pagada | 7 años (ISR) |
| **Ausencia** | Soft Delete | Si ya descontada | 4 años (MITRADEL) |
| **Departamento** | Soft Delete | Estructura organizacional | 2 años |
| **Posicion** | Soft Delete | Estructura organizacional | 2 años |
| **Tenant** | Soft Delete (IsActive) | Datos del cliente SaaS | Según plan |

---

## 8. ENDPOINTS API PROPUESTOS - RESUMEN

### 8.1 Empleados

```http
# Eliminación (soft delete)
DELETE /api/empleados/{id}
Content-Type: application/json
Body: { "reason": "Renuncia voluntaria", "effectiveDate": "2026-01-31" }

# Reactivación
POST /api/empleados/{id}/reactivate

# Validar eliminación (pre-check sin ejecutar)
GET /api/empleados/{id}/deletion-validation
```

### 8.2 Usuarios (YA IMPLEMENTADOS - MEJORAS PENDIENTES)

```http
# Eliminación de usuario del sistema (SystemAdmin only)
DELETE /api/admin/users/{userId}  ✅ YA EXISTE

# Eliminación de usuario de un tenant (SystemAdmin only)
DELETE /api/admin/tenants/{tenantId}/users/{userId}  ✅ YA EXISTE

# Reactivación de usuario en el sistema
POST /api/admin/users/{userId}/reactivate  ✅ YA EXISTE

# Reactivación de usuario en un tenant
POST /api/admin/tenants/{tenantId}/users/{userId}/reactivate  ✅ YA EXISTE

# MEJORA PENDIENTE: Agregar validación de "último Owner"
```

### 8.3 Departamentos y Posiciones

```http
DELETE /api/departamentos/{id}
POST /api/departamentos/{id}/reactivate

DELETE /api/posiciones/{id}
POST /api/posiciones/{id}/reactivate
```

### 8.4 Conceptos de Planilla

```http
DELETE /api/deducciones-fijas/{id}
POST /api/deducciones-fijas/{id}/reactivate

DELETE /api/anticipos/{id}  # Solo si estado = Pendiente/Rechazado

DELETE /api/horas-extra/{id}  # Solo si no están aprobadas
```

---

## 9. CONSIDERACIONES DE UI/UX

### 9.1 Modal de Confirmación de Eliminación

**Diseño recomendado:**

```
┌─────────────────────────────────────────────────────────┐
│  ⚠️  Confirmar Eliminación de Empleado                   │
├─────────────────────────────────────────────────────────┤
│                                                          │
│  Empleado: Juan Carlos Pérez González                   │
│  Cédula: 8-123-4567                                     │
│  Departamento: Ventas                                    │
│                                                          │
│  ❌ BLOQUEADORES (No se puede eliminar):                │
│  • Préstamo activo con saldo de B/. 1,200.00            │
│  • Deducción judicial activa (Pensión alimenticia)      │
│                                                          │
│  Para eliminar este empleado:                           │
│  1. Cancele o transfiera el préstamo activo             │
│  2. Contacte al tribunal para finalizar la deducción    │
│                                                          │
│  [ Cancelar ]                                           │
└─────────────────────────────────────────────────────────┘
```

**Con solo warnings (eliminación permitida):**

```
┌─────────────────────────────────────────────────────────┐
│  ⚠️  Confirmar Eliminación de Empleado                   │
├─────────────────────────────────────────────────────────┤
│                                                          │
│  Empleado: María Elena Rodríguez                         │
│  Cédula: 9-876-5432                                     │
│  Departamento: Administración                            │
│                                                          │
│  ⚠️ ADVERTENCIAS:                                        │
│  • Tiene 3 horas extra aprobadas pendientes (B/. 45.00) │
│  • Aparece en planilla DRAFT del período actual          │
│                                                          │
│  Al eliminar este empleado:                             │
│  ✓ Se marcará como inactivo en el sistema              │
│  ✓ Se preservará su historial de planillas             │
│  ✓ Las horas extra pendientes NO se pagarán            │
│  ✓ Se excluirá de la planilla en estado DRAFT          │
│                                                          │
│  Razón de eliminación:                                  │
│  [ Seleccionar ▼ ]                                      │
│    - Renuncia voluntaria                                │
│    - Despido justificado                                │
│    - Despido sin justa causa                            │
│    - Fin de contrato temporal                           │
│    - Jubilación                                         │
│    - Otro                                               │
│                                                          │
│  Observaciones (opcional):                              │
│  [                                             ]         │
│                                                          │
│  ☑ Entiendo que esta acción no se puede deshacer        │
│                                                          │
│  [ Cancelar ]  [ Eliminar Empleado ]                   │
└─────────────────────────────────────────────────────────┘
```

### 9.2 Indicadores Visuales

**En lista de empleados:**

```
┌─────────────────────────────────────────────────────────┐
│  Empleados (45 activos, 3 eliminados)                   │
├─────────────────────────────────────────────────────────┤
│                                                          │
│  Mostrar: [ ✓ Activos ] [ Inactivos ] [ Eliminados ]   │
│                                                          │
│  [ Juan Pérez     ]  Ventas     B/. 1,500  [🗑️ Eliminar] │
│  [ María Gómez    ]  Admin      B/. 1,800  [🗑️ Eliminar] │
│  [ Pedro López    ]  🔴 INACTIVO (desde 2025-12-01)      │
│  [ Ana Martínez   ]  ⚫ ELIMINADO (Renuncia, 2025-11-15) │
│                                                          │
└─────────────────────────────────────────────────────────┘
```

**Colores sugeridos:**
- 🟢 Verde: Activo (EstaActivo = true, IsDeleted = false)
- 🔴 Rojo: Inactivo (EstaActivo = false, IsDeleted = false)
- ⚫ Negro/Gris: Eliminado (IsDeleted = true)

---

## 10. CHECKLIST DE IMPLEMENTACIÓN

### Backend

- [ ] Agregar campos `IsDeleted`, `DeletedAt`, `DeletedBy`, `DeletionReason` a `Empleado.cs`
- [ ] Crear `EmpleadoValidationService.cs`
- [ ] Agregar endpoint `DELETE /api/empleados/{id}` en `EmpleadosController.cs`
- [ ] Agregar endpoint `POST /api/empleados/{id}/reactivate` en `EmpleadosController.cs`
- [ ] Agregar validación de "último Owner" en `AdminController.DeleteUser()`
- [ ] Crear DTOs: `DeleteEmpleadoDto`, `DeletionValidationResult`
- [ ] Agregar filtros `IsDeleted` en queries de empleados
- [ ] Implementar audit logging en todos los endpoints de eliminación
- [ ] Crear migración de base de datos
- [ ] Testing unitario de validaciones
- [ ] Testing de integración de endpoints

### Frontend

- [ ] Crear componente `DeleteEmpleadoModal.tsx`
- [ ] Agregar botón de eliminación en `EmpleadosPage.jsx`
- [ ] Agregar filtro "Mostrar eliminados" en lista de empleados
- [ ] Agregar indicadores visuales (colores por estado)
- [ ] Agregar toast notifications para confirmación
- [ ] Agregar loading states durante validación
- [ ] Testing manual de flujos completos

### Documentación

- [ ] Actualizar API documentation (Swagger)
- [ ] Crear guía de usuario para eliminación de empleados
- [ ] Documentar compliance y retención de datos
- [ ] Agregar ejemplos de uso en README

---

## 11. RIESGOS Y MITIGACIÓN

| Riesgo | Impacto | Probabilidad | Mitigación |
|--------|---------|--------------|------------|
| **Pérdida de datos fiscales** | CRÍTICO | Baja | Soft delete obligatorio + retención 7 años |
| **Violación CSS/MITRADEL** | ALTO | Media | Preservar planillas permanentemente |
| **Tenant sin Owner** | ALTO | Media | Validación estricta antes de eliminar |
| **Datos inconsistentes** | MEDIO | Media | Validaciones exhaustivas + transacciones |
| **Eliminación accidental** | MEDIO | Alta | Modal de confirmación + audit log |
| **No cumplir derecho al olvido** | BAJO | Baja | Implementar purga automática post-retención |

---

## 12. CONCLUSIONES Y PRÓXIMOS PASOS

### Conclusiones Clave

1. **NO SE DEBE ELIMINAR FÍSICAMENTE:**
   - Planillas (PayrollHeader/PayrollDetail)
   - Préstamos
   - Registros fiscales (CSS, ISR, SE)

2. **SOFT DELETE OBLIGATORIO PARA:**
   - Empleados (con validaciones estrictas)
   - Usuarios (con protección de Owners)
   - Departamentos y Posiciones (con validaciones de dependencias)

3. **RETENCIÓN MÍNIMA:** 7 años (alineado con DGI - ISR)

4. **VALIDACIONES CRÍTICAS:**
   - Préstamos activos
   - Deducciones judiciales
   - Último Owner de tenant
   - Último SystemAdmin del sistema

### Próximos Pasos Inmediatos

1. **Aprobar** este documento con stakeholders
2. **Priorizar** Fase 1 (Usuarios) y Fase 2 (Empleados)
3. **Asignar** recursos de desarrollo (Backend + Frontend)
4. **Crear** tickets en sistema de gestión de proyectos
5. **Estimar** tiempos de implementación (2-3 sprints)
6. **Comunicar** a usuarios finales sobre nueva funcionalidad

---

## APÉNDICE A: REFERENCIAS LEGALES

### Panamá - Código de Trabajo
- **Artículo 183**: Retención de registros de empleados (4 años)
- **Artículo 225**: Liquidaciones finales de empleados

### Panamá - Ley 51-2005 (CSS)
- **Artículo 47**: Retención de planillas CSS (5 años)

### Panamá - Código Fiscal
- **Artículo 708**: Retención de documentos tributarios (7 años)

### Panamá - Código de Familia
- Pensiones alimenticias: Obligación mientras exista la orden judicial

---

## APÉNDICE B: DIAGRAMAS DE FLUJO

### Flujo de Eliminación de Empleado

```
┌─────────────────────────────────────────────────────────────┐
│                FLUJO DE ELIMINACIÓN DE EMPLEADO              │
└─────────────────────────────────────────────────────────────┘

Usuario: Click "Eliminar" en empleado
   ↓
Sistema: GET /api/empleados/{id}/deletion-validation
   ↓
   ├─ ¿Tiene préstamos activos? ────> SÍ ──> BLOQUEAR
   ├─ ¿Tiene deducciones judiciales? ─> SÍ ──> BLOQUEAR
   ├─ ¿Tiene anticipos aprobados? ───> SÍ ──> BLOQUEAR
   │
   ├─ ¿Tiene horas extra pendientes? ─> SÍ ──> ADVERTIR
   ├─ ¿Aparece en planilla DRAFT? ───> SÍ ──> ADVERTIR
   │
   └─ Sin bloqueadores ──> PERMITIR
                               ↓
                          Mostrar modal con warnings
                               ↓
                          Usuario confirma + razón
                               ↓
                          DELETE /api/empleados/{id}
                               ↓
                          ├─ Marcar IsDeleted = true
                          ├─ Establecer DeletedAt = NOW
                          ├─ Guardar DeletedBy = UserId
                          ├─ Guardar DeletionReason
                          ├─ Desvincular usuario (UserId = null)
                          ├─ Registrar en audit log
                          └─ Enviar email a Owners
                               ↓
                          Actualizar lista de empleados
                               ↓
                          Mostrar toast "Empleado eliminado exitosamente"
```

---

**FIN DEL DOCUMENTO**

---

**Aprobaciones:**

| Rol | Nombre | Firma | Fecha |
|-----|--------|-------|-------|
| Functional Architect | PlanillaFunctionalArchitect | ______ | 2026-02-01 |
| Backend Architect | PlanillaBackendArchitect | ______ | ______ |
| Payroll Architect | PlanillaPayrollArchitect | ______ | ______ |
| Legal Compliance | ______ | ______ | ______ |
| Product Owner | ______ | ______ | ______ |
