# Pendientes de Verificación y Corrección — Planilla (2026-02-19)

## PENDIENTES FASE 7 (Reportes)

### 7.1 Consolidado por Acreedor ✅ VERIFICADO Y CORREGIDO
- BUG-002 corregido
- Verificado via API: 3 acreedores, B/.450 total

### 7.2 Deducciones por Empleado — PENDIENTE VERIFICAR
- Probablemente también vacío por el mismo BUG-002
- Verificar después de corregir BUG-002

---

## PENDIENTES FASE 8 (UX/UI)

### 8.1 Moneda "USD" → "B/." en reportes
- Archivos afectados: ReportesPage.jsx (modales de reporte)
- Prioridad: Media

### 8.2 Formulario de empleado no guarda campos CSS/ISR
- Archivo: EmpleadosPage.jsx
- Campos: yearsCotized, averageSalaryLast10Years, dependents, cssRiskPercentage
- Prioridad: ALTA — afecta cálculos de producción

### 8.3 Verificar responsividad de tablas
- Tablas de planilla y reportes en pantallas pequeñas

---

## CORRECCIONES YA APLICADAS (esta sesión)

### C-001: CssHighMinYears y CssIntermediateMinYears en seeder ✅
- **Archivo**: PayrollConfigSeeder.cs
- **Cambio**: Agrega lógica para actualizar thresholds en configs existentes
- **Commit**: No commiteado aún

### C-002: PayFrequency en ISR calculation ✅
- **Archivo**: PayrollHeadersController.cs línea ~359
- **Cambio**: `payFrequency: employee.PayFrequency` (era `payrollHeader.PayPeriodType.ToString()`)
- **Efecto**: Ana (Quincenal) ahora usa 24 períodos para ISR, no 12
- **Commit**: No commiteado aún

### C-003: DeduccionesAplicadas ahora se persisten en DB ✅
- **Archivos**: PayrollHeadersController.cs, PayrollProcessingService.cs
- **Cambio**: Se agrega llamada a CreateDeduccionesAplicadasAsync() después del SaveChangesAsync() en CalculatePayroll
- **Efecto**: Reportes Consolidado por Acreedor y Deducciones por Empleado ahora tienen datos
- **Commit**: No commiteado aún

---

## PRÓXIMOS PASOS PRIORITARIOS

1. **FIX BUG-002**: Corregir PayrollCalculationOrchestratorPortable.cs para persistir DeduccionesAplicadas
2. **FIX BUG-001**: Corregir EmpleadosPage.jsx para guardar campos CSS/ISR correctamente
3. **FIX BUG-004**: Cambiar "USD" por "B/." en reportes
4. Verificar reporte "Deducciones por Empleado" (depende de fix BUG-002)
5. Verificar reporte "Consolidado por Acreedor" (depende de fix BUG-002)
6. Commit de todos los cambios probados y validados
