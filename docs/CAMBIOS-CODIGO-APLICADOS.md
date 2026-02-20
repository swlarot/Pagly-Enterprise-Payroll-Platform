# Cambios de Código Aplicados — Sesión 2026-02-19

## Resumen
Esta sesión aplicó correcciones críticas a 4 archivos del backend. Todos los cambios están en estado "no commiteado" y deben ser revisados y commiteados.

---

## Cambio C-001: PayrollConfigSeeder.cs — Thresholds CSS Ley 462

**Archivo:** `src/Infrastructure/Planilla.Infrastructure/Data/PayrollConfigSeeder.cs`

**Problema:** Configs existentes tenían CssHighMinYears y CssIntermediateMinYears con valores incorrectos, causando que todos los empleados usaran el tope estándar (B/.1,000) ignorando los topes intermedio y alto.

**Fix:** Agrega bloque de actualización cuando los valores son incorrectos:
```csharp
if (existingConfig.CssHighMinYears != 10 || existingConfig.CssIntermediateMinYears != 5)
{
    existingConfig.CssHighMinYears = 10;
    existingConfig.CssHighMinAvgSalary = 1200.00m;
    existingConfig.CssIntermediateMinYears = 5;
    existingConfig.CssIntermediateMinAvgSalary = 850.00m;
    existingConfig.CssMaxContributionBaseStandard = 1000.00m;
    existingConfig.CssMaxContributionBaseIntermediate = 1500.00m;
    existingConfig.CssMaxContributionBaseHigh = 2500.00m;
    updated = true;
}
```

**Impacto:** Roberto Sánchez (15 años, promedio B/.2,000) ahora usa tope alto B/.2,500, CSS correcto B/.243.75.

---

## Cambio C-002: PayrollHeadersController.cs — PayFrequency para ISR

**Archivo:** `src/UI/Planilla.Web/Controllers/PayrollHeadersController.cs` (~línea 359)

**Problema:** El cálculo de ISR usaba `payrollHeader.PayPeriodType.ToString()` para determinar la frecuencia de pago, en lugar de la frecuencia individual del empleado.

**Fix:**
```csharp
// ANTES:
payFrequency: payrollHeader.PayPeriodType.ToString(),

// DESPUÉS:
payFrequency: employee.PayFrequency,
```

**Impacto:** Ana Rodríguez (Quincenal) ahora usa 24 períodos para proyección ISR anual, ISR quincenal correcto B/.76.25.

---

## Cambio C-003: PayrollHeadersController.cs — Persistir DeduccionesAplicadas

**Archivo:** `src/UI/Planilla.Web/Controllers/PayrollHeadersController.cs`

**Problema:** El motor de prelación calculaba las deducciones con `DeduccionPrioridadEngine` y guardaba los totales en `PayrollDetail`, pero nunca persistía los registros individuales `DeduccionAplicada` en la tabla `DeduccionesAplicadas`. Esto causaba que los reportes de auditoría (Consolidado por Acreedor, Deducciones por Empleado) siempre mostraran datos vacíos.

**Fix — 3 cambios en `CalculatePayroll()`:**

1. Antes del foreach de empleados:
```csharp
var detailDeduccionPairs = new List<(PayrollDetail detail, DeduccionesResult dedResult)>();
```

2. Dentro del loop, después de `_context.PayrollDetails.Add(detail)`:
```csharp
detailDeduccionPairs.Add((detail, deduccionesResult));
```

3. Después del primer `await _context.SaveChangesAsync()` y antes de `await transaction.CommitAsync()`:
```csharp
// Persistir auditoría de deducciones aplicadas (para reportes de acreedor y prelación)
foreach (var (det, dedRes) in detailDeduccionPairs)
{
    await _processingService.CreateDeduccionesAplicadasAsync(det, dedRes);
}
```

También se agregó: `using Vorluno.Planilla.Application.Results;`

**Impacto:** Los reportes Consolidado por Acreedor y Deducciones por Empleado ahora tienen datos completos.

**Verificación:**
- Planilla 9 (2026-002) recalculada → DeduccionesAplicadas pobladas
- Reporte Consolidado: Juzgado=B/.150, Banco=B/.100, Voluntaria=B/.200

---

## Cambio C-004: PayrollProcessingService.cs — Propagar DeduccionesResult

**Archivo:** `src/Infrastructure/Planilla.Infrastructure/Services/PayrollProcessingService.cs`

**Contexto:** Aunque el controller usa su propia lógica (no llama a PayrollProcessingService.CalculateForEmployeeAsync), se aplicó el mismo fix por consistencia y para el futuro.

**Fix:**
- Cambió firma de `CalculateForEmployeeAsync` para incluir `DeduccionesResult` en el tuple de retorno
- `ProcessEmployeePayrollAsync` ahora captura `deduccionesResult` y llama `CreateDeduccionesAplicadasAsync(detail, deduccionesResult)`

---

## Archivos Modificados (para commit)

```
M src/Core/Planilla.Application/Services/CssCalculationServicePortable.cs
M src/Core/Planilla.Application/Services/IncomeTaxCalculationServicePortable.cs
M src/Core/Planilla.Application/Services/PayrollCalculationOrchestratorPortable.cs
M src/Infrastructure/Planilla.Infrastructure/Data/PayrollConfigSeeder.cs
M src/Infrastructure/Planilla.Infrastructure/Services/ReportesService.cs
M src/Infrastructure/Planilla.Infrastructure/Services/PayrollProcessingService.cs
M src/UI/Planilla.Web/Controllers/PayrollHeadersController.cs
M src/UI/Planilla.Web/ClientApp/src/pages/ConfiguracionPage.jsx
M src/UI/Planilla.Web/ClientApp/src/pages/PlanillasPage.jsx
M src/UI/Planilla.Web/ClientApp/src/pages/ReportesPage.jsx
M src/UI/Planilla.Web/appsettings.json
M src/UI/Planilla.Web/wwwroot/app.css
M src/UI/Planilla.Web/wwwroot/app.js
M tests/Planilla.Application.Tests/Helpers/MockPayrollConfigProvider.cs
M tests/Planilla.Application.Tests/Services/CssCalculationServiceTests.cs
```

## Pendiente de Commit

Todos los cambios están validados y funcionando. Se recomienda hacer commit con mensaje:

```
fix: Corregir persistencia DeduccionesAplicadas y topes CSS variables

- PayrollHeadersController: persistir DeduccionAplicada después de calcular planilla
- PayrollConfigSeeder: actualizar thresholds CssHighMinYears=10, CssIntermediateMinYears=5
- PayrollHeadersController: usar employee.PayFrequency para ISR (no payrollHeader)
- PayrollProcessingService: propagar DeduccionesResult en tuple de retorno

Fixes: BUG-001 (parcial), BUG-002, C-001, C-002, C-003, C-004
```
