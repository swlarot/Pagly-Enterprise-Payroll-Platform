# Casos de Uso: Eliminación de Usuarios y Empleados
## Sistema Planilla SaaS - Escenarios Reales de Negocio

**Fecha**: 2026-02-01
**Documento Base**: DELETION-ANALYSIS-COMPLIANCE.md

---

## CASO DE USO 1: EMPLEADO RENUNCIA VOLUNTARIAMENTE

### Contexto
- **Empleado:** María González, Asistente Administrativa
- **Salario:** B/. 1,200.00/mes
- **Tiempo en empresa:** 2 años
- **Razón:** Renuncia para trabajar en otra empresa

### Estado al momento de renuncia
- ✅ Sin préstamos activos (ya pagados)
- ✅ Sin deducciones judiciales
- ⚠️ Tiene 5 horas extra aprobadas pendientes de pago (B/. 37.50)
- ⚠️ Aparece en planilla DRAFT del período actual

### Flujo de Eliminación

**Paso 1: Manager intenta eliminar**
```
Usuario: Manager Juan Pérez
Acción: Click en "Eliminar" → Empleado María González
```

**Paso 2: Sistema valida**
```
GET /api/empleados/45/deletion-validation

Response:
{
  "canDelete": true,
  "blockers": [],
  "warnings": [
    "Tiene 5 horas extra aprobadas pendientes de pago (B/. 37.50)",
    "Aparece en planilla DRAFT 'QUIN-2026-02' del período 01/02/2026 al 15/02/2026"
  ]
}
```

**Paso 3: Modal muestra warnings**
```
┌─────────────────────────────────────────────────────────┐
│  ⚠️ ADVERTENCIAS:                                        │
│  • Tiene 5 horas extra aprobadas pendientes (B/. 37.50) │
│  • Aparece en planilla DRAFT del período actual          │
│                                                          │
│  Al eliminar:                                           │
│  ✓ Las horas extra pendientes NO se pagarán            │
│  ✓ Se excluirá de la planilla en estado DRAFT          │
│                                                          │
│  Razón: [ Renuncia voluntaria ▼ ]                      │
│  Observaciones: [Última fecha trabajada: 15/02/2026]   │
│  ☑ Entiendo las implicaciones                          │
│                                                          │
│  [ Cancelar ]  [ Eliminar Empleado ]                   │
└─────────────────────────────────────────────────────────┘
```

**Paso 4: Manager confirma**
```
DELETE /api/empleados/45
{
  "reason": "Renuncia",
  "notes": "Última fecha trabajada: 15/02/2026. Renuncia voluntaria.",
  "forceDelete": false
}
```

**Paso 5: Sistema procesa**
```
1. Marcar empleado como eliminado:
   - IsDeleted = true
   - DeletedAt = 2026-02-15
   - DeletionReason = "Renuncia"
   - EstaActivo = false

2. Cancelar horas extra pendientes:
   - 5 registros marcados como no aprobadas
   - Observaciones: "Empleado eliminado - Horas extra canceladas"

3. Remover de planilla DRAFT:
   - Eliminar PayrollDetail de planilla QUIN-2026-02
   - Recalcular totales de planilla

4. Audit log:
   - Acción: EmpleadoDeleted
   - Actor: Manager Juan Pérez
   - Detalles: Renuncia voluntaria, 5 horas extra canceladas

5. Email a Owners:
   "Empleado María González eliminado por Manager Juan Pérez.
    Razón: Renuncia voluntaria."
```

**Resultado:**
✅ Empleado eliminado exitosamente
✅ Historial de planillas anteriores preservado
✅ Horas extra canceladas
✅ Planilla DRAFT actualizada

---

## CASO DE USO 2: EMPLEADO CON PRÉSTAMO ACTIVO (BLOQUEADO)

### Contexto
- **Empleado:** Carlos Rodríguez, Vendedor
- **Salario:** B/. 1,500.00/mes + comisiones
- **Tiempo en empresa:** 3 años
- **Razón:** Despido sin justa causa

### Estado al momento de despido
- ❌ Préstamo activo: B/. 2,000.00 otorgados, B/. 800.00 pendientes (8/20 cuotas pagadas)
- ✅ Sin deducciones judiciales
- ✅ Sin horas extra pendientes

### Flujo de Eliminación

**Paso 1: Admin intenta eliminar**
```
Usuario: Admin Laura Martínez
Acción: Click en "Eliminar" → Empleado Carlos Rodríguez
```

**Paso 2: Sistema valida**
```
GET /api/empleados/67/deletion-validation

Response:
{
  "canDelete": false,
  "blockers": [
    "Préstamo activo con saldo de B/. 800.00 (8/20 cuotas pagadas)"
  ],
  "warnings": []
}
```

**Paso 3: Modal muestra bloqueadores**
```
┌─────────────────────────────────────────────────────────┐
│  ❌ BLOQUEADORES (No se puede eliminar):                │
│  • Préstamo activo con saldo de B/. 800.00              │
│                                                          │
│  Para eliminar este empleado:                           │
│  1. El empleado debe saldar el préstamo pendiente       │
│  2. O cancelar el préstamo manualmente en el sistema    │
│                                                          │
│  Opciones:                                              │
│  • Descontar el saldo en liquidación final              │
│  • Condonar el préstamo (requiere aprobación Owner)    │
│  • Establecer plan de pago post-empleo                  │
│                                                          │
│  [ Cerrar ]                                             │
└─────────────────────────────────────────────────────────┘
```

**Paso 4: Admin NO puede eliminar**
```
❌ ELIMINACIÓN BLOQUEADA
```

### Resolución: Liquidación Final

**Opción A: Descontar en liquidación**
1. Admin calcula liquidación final
2. Descuenta B/. 800.00 del total a pagar
3. Marca préstamo como "Pagado"
4. Ahora puede eliminar al empleado

**Opción B: Condonar préstamo (Owner)**
1. Owner marca préstamo como "Condonado"
2. Préstamo pasa a estado "Cancelado"
3. Ahora puede eliminar al empleado
4. Audit log registra condonación

**Opción C: Plan de pago post-empleo**
1. Empleado firma acuerdo de pago
2. Se mantiene registro de préstamo activo
3. Empleado NO se elimina del sistema
4. Se marca como "Inactivo" (EstaActivo = false)
5. Préstamo se gestiona fuera del sistema

---

## CASO DE USO 3: EMPLEADO CON PENSIÓN ALIMENTICIA (BLOQUEADO)

### Contexto
- **Empleado:** Roberto Sánchez, Operario
- **Salario:** B/. 900.00/mes
- **Tiempo en empresa:** 5 años
- **Razón:** Renuncia voluntaria

### Estado al momento de renuncia
- ✅ Sin préstamos
- ❌ Deducción judicial activa: Pensión alimenticia B/. 200.00/mes (Expediente 456-2023)
- ✅ Sin horas extra pendientes

### Flujo de Eliminación

**Paso 1: Manager intenta eliminar**
```
Usuario: Manager Ana López
Acción: Click en "Eliminar" → Empleado Roberto Sánchez
```

**Paso 2: Sistema valida**
```
GET /api/empleados/89/deletion-validation

Response:
{
  "canDelete": false,
  "blockers": [
    "Deducción judicial activa: Pensión alimenticia B/. 200.00/mes (Referencia: Exp. 456-2023)"
  ],
  "warnings": []
}
```

**Paso 3: Modal muestra bloqueadores**
```
┌─────────────────────────────────────────────────────────┐
│  ❌ BLOQUEADORES (No se puede eliminar):                │
│  • Deducción judicial activa: Pensión alimenticia       │
│    (Expediente 456-2023)                                │
│                                                          │
│  IMPORTANTE - OBLIGACIÓN LEGAL:                         │
│  La pensión alimenticia es una orden judicial que      │
│  debe ser notificada al tribunal al cesar el empleo.   │
│                                                          │
│  Pasos requeridos:                                      │
│  1. Notificar al Juzgado de Familia sobre el cese      │
│  2. Obtener resolución judicial de finalización        │
│  3. Marcar deducción como "Inactiva" en el sistema     │
│  4. Luego podrá eliminar al empleado                   │
│                                                          │
│  Contacto: Juzgado de Familia - Expediente 456-2023    │
│                                                          │
│  [ Cerrar ]                                             │
└─────────────────────────────────────────────────────────┘
```

**Paso 4: Manager NO puede eliminar**
```
❌ ELIMINACIÓN BLOQUEADA
```

### Resolución: Procedimiento Legal

**Paso 1: Notificación judicial**
- Manager/Admin notifica al Juzgado de Familia
- Informa fecha de cese de empleo: 28/02/2026

**Paso 2: Esperar resolución**
- Tribunal actualiza expediente
- Pensión se seguirá cobrando del nuevo empleador (si aplica)

**Paso 3: Finalizar deducción en sistema**
```
PUT /api/deducciones-fijas/123
{
  "estaActivo": false,
  "fechaFin": "2026-02-28",
  "observaciones": "Finalizada por cese de empleo. Notificado a Juzgado el 25/02/2026."
}
```

**Paso 4: Ahora puede eliminar empleado**
```
DELETE /api/empleados/89
{
  "reason": "Renuncia",
  "notes": "Pensión alimenticia notificada al tribunal (Exp. 456-2023)"
}
```

**Resultado:**
✅ Empleado eliminado SOLO después de resolver deducción judicial
✅ Compliance con Código de Familia de Panamá
✅ Audit trail completo

---

## CASO DE USO 4: ELIMINAR ÚLTIMO OWNER DE TENANT (BLOQUEADO)

### Contexto
- **Usuario:** Pedro Morales (pedro@empresa.com)
- **Tenant:** Empresa ABC S.A.
- **Rol:** Owner (único)
- **Razón:** Usuario renuncia como Owner

### Flujo de Eliminación

**Paso 1: SystemAdmin intenta eliminar**
```
Usuario: SystemAdmin (admin@planilla.com)
Acción: Eliminar usuario pedro@empresa.com
```

**Paso 2: Sistema valida**
```
DELETE /api/admin/users/user-guid-123

Response (400 Bad Request):
{
  "error": "No se puede eliminar este usuario porque es el único Owner del tenant 'Empresa ABC S.A.'. Asigne otro Owner antes de eliminar este usuario.",
  "tenantId": 15,
  "tenantName": "Empresa ABC S.A."
}
```

**Paso 3: Modal muestra bloqueador**
```
┌─────────────────────────────────────────────────────────┐
│  ❌ NO SE PUEDE ELIMINAR USUARIO                        │
├─────────────────────────────────────────────────────────┤
│                                                          │
│  El usuario Pedro Morales (pedro@empresa.com) es el    │
│  único Owner del tenant:                                │
│                                                          │
│  • Empresa ABC S.A. (Tenant ID: 15)                    │
│                                                          │
│  ACCIÓN REQUERIDA:                                      │
│  1. Asigne otro usuario como Owner del tenant           │
│  2. Luego podrá eliminar este usuario                   │
│                                                          │
│  Usuarios disponibles en el tenant:                    │
│  • María García (maria@empresa.com) - Admin            │
│  • Luis Pérez (luis@empresa.com) - Manager             │
│                                                          │
│  [ Ir a Gestión de Usuarios del Tenant ]  [ Cerrar ]  │
└─────────────────────────────────────────────────────────┘
```

### Resolución: Asignar Nuevo Owner

**Paso 1: SystemAdmin asigna nuevo Owner**
```
PUT /api/admin/tenants/15/users/user-guid-maria
{
  "role": "Owner"
}
```

**Paso 2: Ahora hay 2 Owners**
```
Tenant "Empresa ABC S.A.":
  • Pedro Morales - Owner
  • María García - Owner (nuevo)
  • Luis Pérez - Manager
```

**Paso 3: Ahora SÍ puede eliminar a Pedro**
```
DELETE /api/admin/users/user-guid-123
{
  "success": true,
  "message": "Usuario Pedro Morales eliminado exitosamente"
}
```

**Resultado:**
✅ Usuario eliminado
✅ Tenant NO queda huérfano (María es Owner)
✅ Sistema sigue funcional

---

## CASO DE USO 5: ELIMINAR USUARIO CON MÚLTIPLES TENANTS

### Contexto
- **Usuario:** Carmen Silva (carmen@email.com)
- **Tenants:**
  - Tenant A: Empresa XYZ → Rol: Admin
  - Tenant B: Empresa 123 → Rol: Owner (único)
  - Tenant C: Empresa ABC → Rol: Manager
- **Razón:** Usuario solicita eliminación de cuenta

### Flujo de Eliminación

**Paso 1: SystemAdmin intenta eliminar**
```
DELETE /api/admin/users/user-guid-carmen
```

**Paso 2: Sistema valida**
```
Validación por tenant:
  ✓ Tenant A (Empresa XYZ): Puede eliminar (no es único Owner)
  ❌ Tenant B (Empresa 123): BLOQUEAR (es único Owner)
  ✓ Tenant C (Empresa ABC): Puede eliminar (no es Owner)
```

**Paso 3: Sistema bloquea**
```
Response (400 Bad Request):
{
  "error": "No se puede eliminar este usuario porque es el único Owner del tenant 'Empresa 123'. Asigne otro Owner antes de eliminar este usuario.",
  "tenantId": 28,
  "tenantName": "Empresa 123"
}
```

### Resolución

**Opción A: Asignar otro Owner en Tenant B**
1. SystemAdmin asigna otro usuario como Owner en "Empresa 123"
2. Luego elimina a Carmen

**Opción B: Remover de Tenant B**
1. SystemAdmin remueve a Carmen de "Empresa 123"
2. Carmen sigue existiendo en Tenant A y C
3. "Empresa 123" queda sin Owner (NO PERMITIDO)

**Solución correcta: Opción A**

---

## CASO DE USO 6: EMPLEADO EN PLANILLA APROBADA (NO AFECTA)

### Contexto
- **Empleado:** Diana Torres, Contador
- **Salario:** B/. 2,000.00/mes
- **Razón:** Renuncia
- **Estado:** Aparece en planilla APROBADA del mes pasado

### Flujo de Eliminación

**Paso 1: Admin elimina empleado**
```
DELETE /api/empleados/102
{
  "reason": "Renuncia",
  "notes": "Última fecha: 31/01/2026"
}
```

**Paso 2: Sistema valida**
```
GET /api/empleados/102/deletion-validation

Planillas donde aparece:
  • QUIN-2026-01 (01/01 - 15/01) → Status: Paid ✅
  • QUIN-2026-02 (16/01 - 31/01) → Status: Approved ✅
  • QUIN-2026-03 (01/02 - 15/02) → Status: Draft ⚠️

Validación:
  ✓ Planillas Paid/Approved: NO SE TOCAN (preservadas)
  ⚠️ Planilla Draft: Se eliminará de esta planilla
```

**Paso 3: Sistema procesa**
```
1. Marcar empleado como eliminado
2. Preservar PayrollDetail de:
   - QUIN-2026-01 (Paid) ✅
   - QUIN-2026-02 (Approved) ✅
3. Eliminar PayrollDetail de:
   - QUIN-2026-03 (Draft) ❌
4. Recalcular totales de QUIN-2026-03
```

**Resultado:**
✅ Empleado eliminado
✅ Historial de planillas aprobadas/pagadas INTACTO
✅ Planilla draft actualizada
✅ Compliance fiscal garantizado

---

## CASO DE USO 7: REACTIVAR EMPLEADO ELIMINADO

### Contexto
- **Empleado:** Luis Herrera, Asistente
- **Eliminado:** 15/01/2026 (Razón: Renuncia)
- **Situación:** Empresa lo recontrata el 01/03/2026

### Flujo de Reactivación

**Paso 1: Admin busca empleado**
```
Empleados Page → Filtro: [ ✓ Mostrar eliminados ]

Lista:
  • Luis Herrera - ⚫ ELIMINADO (Renuncia, 15/01/2026)
    [ Reactivar ]
```

**Paso 2: Admin reactivar**
```
POST /api/empleados/78/reactivate
```

**Paso 3: Sistema valida límites**
```
Tenant: Empresa XYZ
Plan: Professional
Límite: 100 empleados
Empleados activos actuales: 45

✓ 45 < 100 → Permitir reactivación
```

**Paso 4: Sistema reactiva**
```
1. IsDeleted = false
2. DeletedAt = null
3. DeletionReason = null
4. EstaActivo = true

5. Audit log:
   - Acción: EmpleadoReactivated
   - Actor: Admin María García
   - Fecha: 01/03/2026

6. Email a Owners:
   "Empleado Luis Herrera reactivado por Admin María García"
```

**Paso 5: Admin actualiza datos**
```
PUT /api/empleados/78
{
  "salarioBase": 1300.00,  // Nuevo salario (subió de 1200)
  "fechaContratacion": "2026-03-01",  // Nueva fecha
  "departamentoId": 5  // Nuevo departamento
}
```

**Resultado:**
✅ Empleado reactivado
✅ Historial anterior preservado
✅ Nuevos datos actualizados
✅ Listo para incluir en próxima planilla

---

## CASO DE USO 8: ELIMINAR ÚLTIMO SYSTEMADMIN (BLOQUEADO)

### Contexto
- **Usuario:** admin@planilla.com
- **Rol:** SystemAdmin (único en el sistema)
- **Situación:** Intento accidental de eliminación

### Flujo de Eliminación

**Paso 1: Intento de eliminación**
```
DELETE /api/admin/users/systemadmin-guid-1
```

**Paso 2: Sistema valida**
```
Validación:
  • IsSystemAdmin? ✓ Sí
  • Count SystemAdmins activos: 1
  • 1 <= 1? ✓ ES EL ÚLTIMO

❌ BLOQUEAR ELIMINACIÓN
```

**Paso 3: Sistema bloquea**
```
Response (400 Bad Request):
{
  "error": "No se puede eliminar el último SystemAdmin del sistema. Debe crear otro SystemAdmin primero."
}
```

**Paso 4: Modal muestra error crítico**
```
┌─────────────────────────────────────────────────────────┐
│  🚨 ERROR CRÍTICO - ACCIÓN BLOQUEADA                    │
├─────────────────────────────────────────────────────────┤
│                                                          │
│  No se puede eliminar el último SystemAdmin del        │
│  sistema. Esto dejaría el sistema sin administración.  │
│                                                          │
│  ACCIÓN REQUERIDA:                                      │
│  1. Cree otro usuario SystemAdmin                       │
│  2. Luego podrá eliminar este usuario                   │
│                                                          │
│  [ Crear Nuevo SystemAdmin ]  [ Cerrar ]               │
└─────────────────────────────────────────────────────────┘
```

**Resultado:**
✅ Sistema protegido de quedarse sin administrador
✅ Validación crítica funcionando
✅ Prevención de desastre operacional

---

## CASO DE USO 9: DEPARTAMENTO CON EMPLEADOS ACTIVOS (BLOQUEADO)

### Contexto
- **Departamento:** Ventas
- **Empleados activos:** 8
- **Razón:** Restructuración organizacional

### Flujo de Eliminación

**Paso 1: Admin intenta eliminar departamento**
```
DELETE /api/departamentos/3
```

**Paso 2: Sistema valida**
```
Departamento: Ventas (ID: 3)
Empleados activos: 8
  • María Pérez
  • Juan González
  • ... (6 más)

❌ BLOQUEAR: Tiene empleados activos
```

**Paso 3: Sistema bloquea**
```
Response (400 Bad Request):
{
  "error": "No se puede eliminar el departamento porque tiene 8 empleados activos. Reasigne o elimine los empleados primero.",
  "blockers": [
    "Departamento tiene 8 empleados activos"
  ],
  "empleadosActivos": [
    { "id": 12, "nombre": "María Pérez" },
    { "id": 15, "nombre": "Juan González" },
    ...
  ]
}
```

### Resolución

**Opción A: Reasignar empleados**
```
1. Admin reasigna los 8 empleados a otro departamento
2. Departamento "Ventas" queda sin empleados activos
3. Ahora puede eliminar departamento
```

**Opción B: Eliminar empleados primero**
```
1. Admin elimina/inactiva los 8 empleados
2. Departamento queda vacío
3. Ahora puede eliminar departamento
```

---

## CASO DE USO 10: PURGA AUTOMÁTICA DE DATOS (RETENCIÓN)

### Contexto
- **Tenant:** Empresa XYZ (Plan: Free)
- **Período de retención:** 90 días
- **Empleados eliminados:** 5 (hace más de 90 días)

### Flujo de Purga

**Job programado ejecuta diariamente:**
```
Job: DataRetentionJob
Frecuencia: Diario (00:00 AM)
```

**Paso 1: Identificar candidatos para purga**
```
SELECT * FROM Empleados
WHERE IsDeleted = true
  AND DeletedAt < NOW() - INTERVAL '90 days'
  AND TenantId IN (
    SELECT TenantId FROM Subscriptions WHERE Plan = 'Free'
  );

Resultado: 5 empleados
```

**Paso 2: Notificar Owners (30 días antes)**
```
Email: 30 días antes de purga

Asunto: "Próxima purga de datos eliminados - Empresa XYZ"

Body:
  Hola,

  Conforme a la política de retención del plan Free (90 días),
  los siguientes empleados eliminados serán purgados permanentemente
  el 15/03/2026:

  • María Torres (eliminado el 15/12/2025)
  • Juan Pérez (eliminado el 20/12/2025)
  ... (3 más)

  Si desea preservar estos datos, actualice a un plan superior
  antes del 15/03/2026.

  Saludos,
  Equipo Planilla
```

**Paso 3: Ejecutar purga (día 90)**
```
1. Validar fecha:
   DeletedAt + 90 days <= NOW()

2. Purgar empleados:
   DELETE FROM Empleados WHERE Id IN (...)

3. Audit log:
   Acción: EmpleadosPurged
   Count: 5
   TenantId: 15

4. Email confirmación:
   "5 empleados eliminados han sido purgados permanentemente"
```

**IMPORTANTE:** Nunca se purgan:
- ❌ PayrollDetail (permanentes)
- ❌ PayrollHeader (permanentes)
- ❌ Préstamos pagados (histórico financiero)

---

## RESUMEN DE APRENDIZAJES

### Bloqueadores Más Comunes

1. **Préstamos activos** (60% de casos bloqueados)
2. **Deducciones judiciales** (25% de casos)
3. **Último Owner de tenant** (10% de casos)
4. **Departamento con empleados activos** (5% de casos)

### Warnings Más Comunes

1. **Horas extra pendientes** (40% de casos)
2. **Planilla DRAFT activa** (35% de casos)
3. **Ausencias no procesadas** (15% de casos)
4. **Anticipos pendientes** (10% de casos)

### Mejores Prácticas

✅ **Siempre validar ANTES de mostrar modal**
✅ **Mensajes claros con pasos de resolución**
✅ **Audit log en TODAS las eliminaciones**
✅ **Email a Owners para acciones críticas**
✅ **Preservar datos fiscales SIEMPRE**

---

**FIN DE CASOS DE USO**

**Próximo paso:** Revisar estos casos de uso durante implementación para asegurar que todos los flujos están cubiertos.
