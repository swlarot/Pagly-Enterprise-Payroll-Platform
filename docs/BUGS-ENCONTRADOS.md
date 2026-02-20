# Bugs Encontrados — Sistema Planilla (2026-02-19)

## BUG-001: Campos CSS/ISR de empleado no se guardan desde UI [CRÍTICO] ✅ WORKAROUND
- **Archivo**: src/UI/Planilla.Web/ClientApp/src/pages/EmpleadosPage.jsx
- **Descripción**: Los campos `yearsCotized`, `averageSalaryLast10Years`, `dependents`, `cssRiskPercentage` en el formulario de edición de empleado no persisten correctamente al hacer PUT.
- **Impacto**: Todos los empleados quedan con años=0, promedio=0, dependientes=0, resultando en cálculos de CSS sin tope variable y de ISR sin deducción de dependientes.
- **Estado**: Workaround aplicado — se actualizaron via API directa. Bug en UI pendiente de fix.
- **Evidencia**: GET /api/empleados retornó yearsCotized=0 para todos los empleados tras edición desde UI.

## BUG-002: DeduccionesAplicadas no se persisten en DB [ALTO] ✅ CORREGIDO
- **Archivos corregidos**: `src/UI/Planilla.Web/Controllers/PayrollHeadersController.cs`, `src/Infrastructure/Planilla.Infrastructure/Services/PayrollProcessingService.cs`
- **Descripción**: Cuando se calcula una planilla y se aplican deducciones fijas (pensión, préstamo, ahorro), el orquestador calculaba los montos correctamente (totalDeductions = correcto), pero NO insertaba registros en la tabla `DeduccionesAplicadas`.
- **Impacto original**:
  - Reporte "Consolidado por Acreedor" retornaba vacío
  - Reporte "Deducciones por Empleado" también vacío
  - No había auditoría de prelación de deducciones
- **Fix aplicado en PayrollHeadersController.cs**:
  - Se agregó `var detailDeduccionPairs = new List<(PayrollDetail detail, DeduccionesResult dedResult)>();` antes del loop foreach de empleados
  - Dentro del loop, después de `_context.PayrollDetails.Add(detail)`, se agrega `detailDeduccionPairs.Add((detail, deduccionesResult));`
  - Después del `await _context.SaveChangesAsync()` (que asigna los IDs de los details), se llama:
    ```csharp
    foreach (var (det, dedRes) in detailDeduccionPairs)
    {
        await _processingService.CreateDeduccionesAplicadasAsync(det, dedRes);
    }
    ```
  - Se agregó `using Vorluno.Planilla.Application.Results;` al archivo
- **Fix aplicado en PayrollProcessingService.cs** (mismo patrón, por consistencia):
  - Cambió la firma de retorno de `CalculateForEmployeeAsync` para incluir `DeduccionesResult`
  - Agregó llamada a `CreateDeduccionesAplicadasAsync` en `ProcessEmployeePayrollAsync`
- **Verificación**: GET /api/payrollheaders/9/details/{id}/deducciones retorna:
  - EmpID:16 (Carlos): Pensión Alimenticia → B/.150 ✅
  - EmpID:17 (Ana): Préstamo Banco Nacional → B/.100 ✅
  - EmpID:18 (Roberto): Ahorro Voluntario → B/.200 ✅
- **Reporte Consolidado por Acreedor ahora funciona**:
  - Total acreedores: 3 | GranTotal: B/.450
  - Juzgado 5to de Familia: B/.150 ✅
  - Banco Nacional de Panamá: B/.100 ✅
  - Sin Acreedor (ahorro voluntario): B/.200 ✅

## BUG-003: ISR proyección anual hardcoded × 24 [MEDIO] ⚠️ CONOCIDO/ACEPTADO
- **Archivo**: src/Infrastructure/Planilla.Infrastructure/Services/ReportesService.cs (~línea 169)
- **Descripción**: En el reporte ISR, la proyección anual usa `d.GrossPay * 24` para todos los empleados, independientemente de su frecuencia de pago. Para empleados mensuales (12 períodos/año) esto es incorrecto.
- **Impacto**: Solo visual en el reporte ISR. Los cálculos reales de retención son correctos.
- **Estado**: El usuario decidió NO corregirlo. Se eliminará ese reporte.
- **Fix correcto sería**: Usar `PayrollConstants.GetPeriodsPerYear(d.Empleado!.PayFrequency)` en lugar de `24`.

## BUG-004: Moneda mostrada como "USD" en lugar de "B/." [BAJO] ❌ PENDIENTE
- **Archivos**: Múltiples reportes en ReportesPage.jsx y modales
- **Descripción**: Los montos en los reportes (Horas Extra, etc.) se muestran como "USD X.XX" en lugar de "B/. X.XX".
- **Impacto**: Solo visual, pero incorrecto para Panamá donde la moneda local es el Balboa (B/.).
- **Fix**: Buscar en ReportesPage.jsx y los modales de reporte donde se formatea el currency, cambiar "USD" por "B/."

## BUG-005: Comentario desactualizado en CssCalculationServicePortable.cs [BAJO]
- **Archivo**: src/Core/Planilla.Application/Services/CssCalculationServicePortable.cs
- **Descripción**: Un comentario en el código dice "25/30 años" pero la configuración actual usa 5/10 años para los umbrales de topes CSS.
- **Impacto**: Solo confusión para desarrolladores.

## BUG-006: Backend se cae al cargar Planilla Detallada [MEDIO] ❓ INTERMITENTE
- **Descripción**: Durante las pruebas, el backend se cayó (503) al intentar generar el reporte de Planilla Detallada. Se recuperó al reiniciar.
- **Causa probable**: Fuga de memoria o excepción no manejada en la generación del reporte PDF/Excel.
- **Estado**: Intermitente, necesita más investigación.
