# Resumen Ejecutivo: Eliminación de Usuarios y Empleados
## Sistema Planilla SaaS - Análisis y Recomendaciones

**Fecha**: 2026-02-01
**Preparado por**: PlanillaFunctionalArchitect

---

## 1. RESUMEN DE 30 SEGUNDOS

El sistema Planilla requiere implementar funcionalidades de eliminación de usuarios y empleados que cumplan con:
- **Regulaciones panameñas** (CSS, MITRADEL, DGI): Retención de 7 años
- **Seguridad multi-tenant**: Protección de Owners, aislamiento de datos
- **Integridad de datos**: Soft delete con validaciones estrictas

**Decisión clave:** NUNCA eliminar físicamente planillas, préstamos ni registros fiscales.

---

## 2. ESTRATEGIA RECOMENDADA

### SOFT DELETE OBLIGATORIO

```
┌─────────────────────────────────────────────────────────────┐
│                    POLÍTICA DE ELIMINACIÓN                   │
├─────────────────────────────────────────────────────────────┤
│                                                              │
│  ✅ SOFT DELETE (Recomendado):                              │
│     • Empleados                                              │
│     • Usuarios (AppUser)                                     │
│     • Departamentos y Posiciones                             │
│     • Deducciones fijas                                      │
│     • Anticipos y horas extra                                │
│                                                              │
│  ❌ NUNCA ELIMINAR (Compliance legal):                      │
│     • PayrollHeader / PayrollDetail                          │
│     • Préstamos (usar estado "Cancelado" en su lugar)       │
│     • Registros de CSS, SE, ISR                              │
│                                                              │
│  📅 RETENCIÓN MÍNIMA: 7 años (DGI - ISR)                    │
│                                                              │
└─────────────────────────────────────────────────────────────┘
```

---

## 3. VALIDACIONES CRÍTICAS

### EMPLEADOS

```
┌─────────────────────────────────────────────────────────────┐
│               VALIDACIONES ANTES DE ELIMINAR                 │
├─────────────────────────────────────────────────────────────┤
│                                                              │
│  🔴 BLOQUEADORES (Impiden eliminación):                     │
│     1. Préstamos activos (Estado != Pagado)                 │
│     2. Deducciones judiciales activas                       │
│     3. Anticipos aprobados no descontados                   │
│                                                              │
│  🟡 ADVERTENCIAS (Permiten continuar):                      │
│     1. Horas extra aprobadas no pagadas                     │
│     2. Ausencias no procesadas                              │
│     3. Aparece en planillas DRAFT                           │
│                                                              │
└─────────────────────────────────────────────────────────────┘
```

### USUARIOS

```
┌─────────────────────────────────────────────────────────────┐
│              VALIDACIONES PARA USUARIOS                      │
├─────────────────────────────────────────────────────────────┤
│                                                              │
│  🔴 BLOQUEADORES:                                           │
│     1. Es el último SystemAdmin del sistema                 │
│     2. Es el único Owner de algún tenant                    │
│                                                              │
│  ✅ AL ELIMINAR:                                            │
│     • Marcar IsDeleted = true                               │
│     • Desactivar todas las membresías TenantUser            │
│     • Desvincular de empleados                              │
│     • Preservar audit log                                   │
│                                                              │
└─────────────────────────────────────────────────────────────┘
```

---

## 4. COMPLIANCE: REGULACIONES PANAMEÑAS

| Entidad | Regulación | Período Retención | Aplicable a |
|---------|-----------|-------------------|-------------|
| **CSS** | Ley 51-2005, Art. 47 | **5 años** | Planillas CSS |
| **MITRADEL** | Código de Trabajo, Art. 183 | **4 años** | Registros empleados |
| **DGI** | Código Fiscal, Art. 708 | **7 años** | Documentos ISR |
| **Seguro Educativo** | Ley 106-1972 | **5 años** | Planillas SE |
| **Judicial** | Código de Familia | **Indefinido** | Pensiones alimenticias |

**Período recomendado:** **7 años** (alineado con DGI)

---

## 5. MATRIZ DE DECISIONES: QUÉ ELIMINAR Y CÓMO

| Entidad | Estrategia | Razón | Período Retención |
|---------|-----------|-------|-------------------|
| **Empleado** | ✅ Soft Delete | Historial planillas (CSS, ISR) | 7 años |
| **AppUser** | ✅ Soft Delete | Audit log, multi-tenant | Indefinido |
| **PayrollHeader** | ❌ NO | Documento fiscal | **Permanente** |
| **PayrollDetail** | ❌ NO | Recibos de sueldo | **Permanente** |
| **Prestamo** | ❌ NO | Registro financiero | Permanente |
| **DeduccionFija** | ✅ Soft Delete | Puede ser judicial | 7 años |
| **Departamento** | ✅ Soft Delete | Estructura organizacional | 2 años |

---

## 6. IMPACTO DE ELIMINACIÓN: DIAGRAMA DE DEPENDENCIAS

### EMPLEADO

```
                    ┌─────────────┐
                    │  Empleado   │
                    └──────┬──────┘
                           │
        ┌──────────────────┼──────────────────┐
        │                  │                  │
        ▼                  ▼                  ▼
┌──────────────┐   ┌──────────────┐   ┌──────────────┐
│ PayrollDetail│   │   Prestamo   │   │ DeduccionFija│
│ (NO ELIMINAR)│   │ (NO ELIMINAR)│   │ (PRESERVAR)  │
└──────────────┘   └──────────────┘   └──────────────┘
        │                  │                  │
        │                  ▼                  │
        │          ┌──────────────┐           │
        │          │ PagoPrestamo │           │
        │          │ (NO ELIMINAR)│           │
        │          └──────────────┘           │
        │                                     │
        ▼                                     ▼
┌──────────────┐                     ┌──────────────┐
│  HoraExtra   │                     │   Anticipo   │
│ (PRESERVAR)  │                     │ (PRESERVAR)  │
└──────────────┘                     └──────────────┘
        │
        ▼
┌──────────────┐
│   Ausencia   │
│ (PRESERVAR)  │
└──────────────┘
```

**RIESGO:** Eliminar empleado físicamente = pérdida de historial fiscal = violación CSS/DGI

---

## 7. PLAN DE IMPLEMENTACIÓN

### FASES PRIORIZADAS

```
┌─────────────────────────────────────────────────────────────┐
│                     ROADMAP DE IMPLEMENTACIÓN                │
├─────────────────────────────────────────────────────────────┤
│                                                              │
│  📌 FASE 1: USUARIOS (2-3 días) - PRIORITARIO               │
│     ✓ Validación "último Owner"                             │
│     ✓ Desvinculación Usuario ↔ Empleado                     │
│     ✓ Testing                                               │
│                                                              │
│  📌 FASE 2: EMPLEADOS (5-7 días) - ALTA PRIORIDAD           │
│     • Migración BD (campos IsDeleted, DeletedAt, etc.)      │
│     • Servicio de validación                                │
│     • Endpoints DELETE /api/empleados/{id}                  │
│     • Frontend: Modal de confirmación                       │
│     • Testing exhaustivo                                    │
│                                                              │
│  📌 FASE 3: DEPARTAMENTOS/POSICIONES (2-3 días) - MEDIA     │
│     • Soft delete con validaciones de dependencias          │
│                                                              │
│  📌 FASE 4: CONCEPTOS DE PLANILLA (3-4 días) - BAJA         │
│     • Deducciones, anticipos, horas extra                   │
│                                                              │
│  📌 FASE 5: RETENCIÓN Y PURGA (3-5 días) - OPCIONAL         │
│     • Job programado de purga automática                    │
│     • Dashboard de retención en SystemAdmin                 │
│                                                              │
└─────────────────────────────────────────────────────────────┘
```

**Tiempo total estimado:** 2-3 sprints (15-21 días)

---

## 8. ENDPOINTS API - RESUMEN

### Empleados (NUEVO)

```http
# Validar eliminación (pre-check)
GET /api/empleados/{id}/deletion-validation

# Eliminar empleado (soft delete)
DELETE /api/empleados/{id}
Body: { "reason": "Renuncia", "notes": "...", "forceDelete": false }

# Reactivar empleado
POST /api/empleados/{id}/reactivate

# Desvincular usuario
POST /api/empleados/{id}/unlink-user
```

### Usuarios (MEJORAS A EXISTENTE)

```http
# Eliminar usuario del sistema (SystemAdmin only)
DELETE /api/admin/users/{userId}
# ⚠️ MEJORAR: Agregar validación de "último Owner de tenant"

# Eliminar usuario de tenant
DELETE /api/admin/tenants/{tenantId}/users/{userId}  ✅ OK

# Reactivar usuario en el sistema
POST /api/admin/users/{userId}/reactivate  ✅ OK

# Reactivar usuario en tenant
POST /api/admin/tenants/{tenantId}/users/{userId}/reactivate  ✅ OK
```

---

## 9. ROLES Y PERMISOS

| Acción | Owner | Admin | Manager | Accountant | Employee |
|--------|-------|-------|---------|------------|----------|
| **Eliminar Empleado** | ✅ Sí (force) | ✅ Sí | ❌ No | ❌ No | ❌ No |
| **Reactivar Empleado** | ✅ Sí | ✅ Sí | ❌ No | ❌ No | ❌ No |
| **Eliminar Usuario** | ✅ Sí* | ❌ No | ❌ No | ❌ No | ❌ No |
| **Ver Eliminados** | ✅ Sí | ✅ Sí | ❌ No | ❌ No | ❌ No |

*SystemAdmin puede eliminar usuarios de cualquier tenant

**Force Delete:**
- Solo disponible para **Owners**
- Permite ignorar **warnings** (no blockers)
- Requiere confirmación explícita

---

## 10. UI/UX: MODAL DE CONFIRMACIÓN

### Ejemplo: Con Bloqueadores

```
┌─────────────────────────────────────────────────────────┐
│  ⚠️  Confirmar Eliminación de Empleado                   │
├─────────────────────────────────────────────────────────┤
│                                                          │
│  Empleado: Juan Carlos Pérez González                   │
│  Cédula: 8-123-4567                                     │
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

### Ejemplo: Con Solo Warnings

```
┌─────────────────────────────────────────────────────────┐
│  ⚠️  Confirmar Eliminación de Empleado                   │
├─────────────────────────────────────────────────────────┤
│                                                          │
│  Empleado: María Elena Rodríguez                         │
│  Cédula: 9-876-5432                                     │
│                                                          │
│  ⚠️ ADVERTENCIAS:                                        │
│  • Tiene 3 horas extra aprobadas pendientes (B/. 45.00) │
│  • Aparece en planilla DRAFT del período actual          │
│                                                          │
│  Razón de eliminación:                                  │
│  [ Renuncia voluntaria ▼ ]                              │
│                                                          │
│  Observaciones:                                         │
│  [                                             ]         │
│                                                          │
│  ☑ Entiendo que esta acción no se puede deshacer        │
│                                                          │
│  [ Cancelar ]  [ Eliminar Empleado ]                   │
└─────────────────────────────────────────────────────────┘
```

---

## 11. RIESGOS Y MITIGACIÓN

| Riesgo | Impacto | Probabilidad | Mitigación |
|--------|---------|--------------|------------|
| **Pérdida de datos fiscales** | 🔴 CRÍTICO | 🟢 Baja | Soft delete + retención 7 años |
| **Violación CSS/MITRADEL** | 🔴 ALTO | 🟡 Media | Preservar planillas permanentemente |
| **Tenant sin Owner** | 🔴 ALTO | 🟡 Media | Validación estricta + audit log |
| **Eliminación accidental** | 🟡 MEDIO | 🔴 Alta | Modal confirmación + undo |
| **Datos inconsistentes** | 🟡 MEDIO | 🟡 Media | Transacciones + validaciones |

---

## 12. MÉTRICAS DE ÉXITO

**KPIs a monitorear post-implementación:**

1. **Tasa de eliminación bloqueada**: < 30% de intentos
2. **Tiempo de validación**: < 2 segundos
3. **Errores de eliminación**: < 1% de solicitudes
4. **Tasa de reactivación**: Medir cuántos empleados se reactivan
5. **Compliance**: 0 eliminaciones físicas de planillas

---

## 13. COSTOS Y RECURSOS

### Esfuerzo Estimado

| Fase | Backend | Frontend | Testing | Total |
|------|---------|----------|---------|-------|
| **Fase 1 (Usuarios)** | 1 día | 0.5 días | 0.5 días | **2 días** |
| **Fase 2 (Empleados)** | 3 días | 2 días | 2 días | **7 días** |
| **Fase 3 (Dept/Pos)** | 1 día | 1 día | 0.5 días | **2.5 días** |
| **Fase 4 (Conceptos)** | 2 días | 1 día | 1 día | **4 días** |
| **TOTAL** | **7 días** | **4.5 días** | **4 días** | **15.5 días** |

**Equipo requerido:**
- 1 Backend Developer (Senior)
- 1 Frontend Developer (Mid-Senior)
- 1 QA Tester
- 1 Functional Architect (revisiones)

**Costo aproximado:** 2-3 sprints de desarrollo

---

## 14. PRÓXIMOS PASOS INMEDIATOS

1. ✅ **Aprobar** este documento con stakeholders
2. ✅ **Priorizar** Fase 1 (Usuarios) y Fase 2 (Empleados) en próximo sprint
3. ✅ **Crear** tickets en Jira/Linear con estimaciones
4. ✅ **Asignar** recursos de desarrollo
5. ✅ **Comunicar** a usuarios finales (release notes)

---

## 15. DOCUMENTOS RELACIONADOS

📄 **DELETION-ANALYSIS-COMPLIANCE.md**: Análisis detallado de impacto y compliance
📄 **DELETION-IMPLEMENTATION-SPECS.md**: Especificaciones técnicas de implementación
📄 **CLAUDE.md**: Guía del proyecto Planilla

---

## 16. CONTACTO Y APROBACIONES

**Preparado por:**
- PlanillaFunctionalArchitect

**Requiere aprobación de:**
- [ ] Backend Architect
- [ ] Payroll Architect (compliance legal)
- [ ] Product Owner
- [ ] Legal/Compliance Team (revisión regulaciones)

**Fecha objetivo de inicio:** Sprint siguiente (2026-02-10)
**Fecha objetivo de completación:** 2026-03-15 (Fase 1 y 2)

---

## ANEXO A: PREGUNTAS FRECUENTES

### 1. ¿Por qué no hard delete?

**R:** Las regulaciones panameñas (CSS, MITRADEL, DGI) requieren retención de registros de empleados y planillas por 4-7 años. Eliminar físicamente violaría estas regulaciones.

### 2. ¿Qué sucede con el historial de planillas al eliminar un empleado?

**R:** Se **preserva completamente**. Los `PayrollDetail` nunca se eliminan, garantizando compliance fiscal.

### 3. ¿Puede un Owner eliminar un empleado con préstamo activo?

**R:** NO. Los **bloqueadores** impiden eliminación incluso con `ForceDelete`. El préstamo debe saldarse o cancelarse primero.

### 4. ¿Qué pasa si elimino el último Owner de un tenant?

**R:** El sistema **bloquea** esta acción. Debe asignar otro Owner antes de eliminar.

### 5. ¿Los empleados eliminados cuentan para el límite del plan?

**R:** NO. Solo los empleados con `IsDeleted = false` cuentan para el límite.

### 6. ¿Puedo recuperar un empleado eliminado?

**R:** SÍ. Los Owners y Admins pueden **reactivar** empleados eliminados desde la UI (si no se ha superado el límite del plan).

---

**FIN DEL RESUMEN EJECUTIVO**

---

**Recomendación final:** Proceder con la implementación priorizando Fase 1 (Usuarios) y Fase 2 (Empleados) en los próximos 2 sprints.
