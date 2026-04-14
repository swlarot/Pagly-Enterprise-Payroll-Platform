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

---

## FASE 8 — Bugs de UX/UI encontrados y corregidos (2026-02-19)

## BUG-003: Moneda mostrada como "USD" en lugar de "B/." en todas las páginas [BAJO] ✅ CORREGIDO
- **Archivos corregidos**: 10 archivos React (ver lista completa abajo)
- **Descripción**: Todas las páginas del frontend definían su función `formatCurrency` usando `{style: 'currency', currency: 'USD'}` que producía "USD 1,000.00" en lugar de "B/. 1,000.00".
- **Impacto**: Visual — incorrecto para Panamá donde la moneda es el Balboa (B/.). Potencialmente confuso para usuarios.
- **Archivos afectados y corregidos**:
  - `ReportesPage.jsx` (línea 82)
  - `PlanillasPage.jsx` (línea 325)
  - `PrestamosPage.jsx` (línea 82)
  - `AnticiposPage.jsx` (línea 65)
  - `DeduccionesPage.jsx` (línea 178)
  - `HorasExtraPage.jsx` (línea 281)
  - `AusenciasPage.jsx` (línea 84)
  - `PosicionesPage.jsx` (línea 168) — también agregó `|| 0` guard
  - `MiPerfilPage.tsx` (línea 51)
  - `AdminDashboardPage.tsx` (líneas 112-120) — también corrigió `formatCurrencyShort` de `$Xk` a `B/.Xk`
- **Fix aplicado** (mismo patrón en todos):
  ```js
  // ANTES:
  return new Intl.NumberFormat('es-PA', { style: 'currency', currency: 'USD' }).format(amount);
  // DESPUÉS:
  return 'B/. ' + new Intl.NumberFormat('es-PA', {
    minimumFractionDigits: 2, maximumFractionDigits: 2
  }).format(amount || 0);
  ```

## BUG-004: Reporte "Consolidado por Acreedor" mostraba B/.0.00 en todos los montos [MEDIO] ✅ CORREGIDO
- **Archivo corregido**: `src/UI/Planilla.Web/ClientApp/src/pages/ReportesPage.jsx` (líneas ~409, ~430)
- **Descripción**: El reporte Consolidado por Acreedor mostraba USD 0.00 para todos los acreedores, aunque los cálculos en DB eran correctos.
- **Causa raíz**: La API retorna el campo `totalAplicado` pero el frontend leía `item.montoTotal || item.monto`. También, el total general estaba en `reporteData.granTotalAplicado` (root del response), no en `reporteData.totales.granTotal`.
- **Evidencia** — Response real de `/api/reportes/consolidado-acreedor/9`:
  ```json
  { "acreedores": [{ "totalAplicado": 200.0, ... }, ...], "granTotalAplicado": 450.0 }
  ```
- **Fix aplicado** (línea ~409):
  ```js
  // ANTES:
  {formatCurrency(item.montoTotal || item.monto || 0)}
  // DESPUÉS:
  {formatCurrency(item.totalAplicado ?? item.montoTotal ?? item.monto ?? 0)}
  ```
- **Fix en totales** (línea ~430):
  ```js
  // ANTES:
  {reporteData.totales && (
    <td>{formatCurrency(reporteData.totales.granTotal || reporteData.totales.totalMonto || 0)}</td>
  )}
  // DESPUÉS:
  {(reporteData.granTotalAplicado != null || reporteData.totales) && (
    <td>{formatCurrency(reporteData.granTotalAplicado ?? reporteData.totales?.granTotal ?? 0)}</td>
  )}
  ```
- **Verificación**: Reporte muestra correctamente: Juzgado B/.150, Banco B/.100, Sin Acreedor B/.200, Total B/.450 ✅

## BUG-005: Badges de estado de planilla en inglés [BAJO] ✅ CORREGIDO
- **Archivo corregido**: `src/UI/Planilla.Web/ClientApp/src/pages/PlanillasPage.jsx` (línea ~332)
- **Descripción**: La función `getStatusBadge()` tenía etiquetas en inglés: "Draft", "Calculated", "Approved", "Paid", "Cancelled".
- **Impacto**: Visual — inconsistente con el idioma español de la aplicación.
- **Fix aplicado**:
  ```js
  // ANTES → DESPUÉS
  0: { label: 'Draft', ... }      → { label: 'Borrador', ... }
  1: { label: 'Calculated', ... } → { label: 'Calculado', ... }
  2: { label: 'Approved', ... }   → { label: 'Aprobado', ... }
  3: { label: 'Paid', ... }       → { label: 'Pagado', ... }
  4: { label: 'Cancelled', ... }  → { label: 'Cancelado', ... }
  ```
- **Verificación**: Badge de planilla 2026-002 muestra "Calculado" en color azul ✅

## BUG-006: Símbolo "$" en lugar de "B/." en ConfiguracionPage [BAJO] ✅ CORREGIDO
- **Archivo corregido**: `src/UI/Planilla.Web/ClientApp/src/pages/ConfiguracionPage.jsx`
- **Descripción**: La página de Configuración mostraba el símbolo dólar (`$`) en múltiples lugares: tabla ISR, topes CSS, ejemplos de cálculo, deducciones permitidas, e icono DollarSign de Lucide.
- **Impacto**: Visual — incorrecto para Panamá.
- **Ocurrencias corregidas**:
  - Topes CSS en template literals: `${Number(...)}` → `B/.{Number(...)}`
  - Tabla ISR brackets: `$0 - $11,000` → `B/.0 - B/.11,000`, `$11,001 - $50,000` → `B/.11,001 - B/.50,000`, `$50,001+` → `B/.50,001+`
  - Ejemplo de cálculo: `$30,000` → `B/.30,000`, `$11,000` → `B/.11,000`
  - Deducciones permitidas: `hasta $5,000` → `hasta B/.5,000`, `hasta $20,000` → `hasta B/.20,000`
  - Icono DollarSign Lucide reemplazado por texto:
    ```jsx
    // ANTES: <DollarSign className="h-5 w-5 text-gray-500" />
    // DESPUÉS: <span className="text-gray-500 text-sm font-medium">B/.</span>
    ```
  - Import de `DollarSign` eliminado de la lista de imports de lucide-react
- **Verificación**: Configuración → Salario Mínimo muestra "B/." en el campo, tabla ISR con "B/." ✅

---

## PENDIENTE-001: Dashboard muestra B/.0.00 en desglose CSS/SE/Riesgo Patronal [BAJO] ✅ CORREGIDO
- **Archivos corregidos**:
  - `src/Core/Planilla.Domain/Entities/PayrollHeader.cs`
  - `src/UI/Planilla.Web/Controllers/PayrollHeadersController.cs`
  - Migración: `20260220171856_AddPayrollHeaderEmployerBreakdown`
- **Descripción**: El panel del Dashboard mostraba el "Costo Total Empleador" correcto (B/.721.70), pero los 3 sub-items (CSS Patronal, SE Patronal, Riesgo Profesional) mostraban B/.0.00.
- **Causa raíz**: La entidad `PayrollHeader` no tenía los campos de desglose (`TotalEmployerCss`, `TotalEmployerSe`, `TotalRiskInsurance`), y el controller no los calculaba ni persistía.
- **Fix aplicado**:
  - Agregadas 3 propiedades a `PayrollHeader`: `TotalEmployerCss`, `TotalEmployerSe`, `TotalRiskInsurance`
  - Agregados 3 acumuladores en el loop de cálculo del controller, usando `calculationResult.CssEmployer`, `calculationResult.EducationalInsuranceEmployer`, `calculationResult.RiskContribution`
  - Migración EF Core agrega 3 columnas `numeric(18,2)` con DEFAULT 0 a la tabla `PayrollHeaders`
  - `AdminDashboardPage.tsx` ya leía los campos correctamente — no requirió cambios en frontend
- **Verificación** (planilla 2026-003, 3 empleados):
  - TotalEmployerCss:   B/.596.25 ✅ (132.50 + 132.50 + 331.25)
  - TotalEmployerSe:    B/.90.00 ✅ (15.00 + 15.00 + 60.00)
  - TotalRiskInsurance: B/.35.45 ✅ (4.10 + 4.10 + 27.25)
  - TotalEmployerCost:  B/.721.70 ✅ (suma correcta)

---

## Sesión 2026-02-20 — Bugs adicionales encontrados y corregidos

## BUG-007: Al calcular planilla la UI no se actualiza hasta refrescar la página [MEDIO] ✅ CORREGIDO
- **Archivo corregido**: `src/UI/Planilla.Web/ClientApp/src/pages/PlanillasPage.jsx` (línea 135)
- **Descripción**: Al hacer clic en "Calcular Planilla" (desde estado Borrador), el backend calculaba correctamente y retornaba éxito, pero la UI seguía mostrando los guiones "—" en las tarjetas resumen (Salario Bruto, Neto, CSS, SE, ISR). Solo después de refrescar la página con F5 aparecían los datos calculados.
- **Causa raíz**: En la función `fetchData()`, el bloque de actualización de `selectedPlanilla` tenía la condición `if (!selectedPlanilla)` — es decir, solo se asignaba si no había ninguna planilla seleccionada. Cuando el usuario ya tenía una planilla seleccionada (Borrador), `fetchData()` actualizaba el array `planillas` con datos frescos pero **nunca actualizaba `selectedPlanilla`**, que es la variable que alimenta todas las tarjetas resumen y el stepper de estado.
- **Fix aplicado**:
  ```js
  // ANTES — solo asignaba si no había selección:
  if (enrichedPlanillas.length > 0 && !selectedPlanilla) {
      setSelectedPlanilla(enrichedPlanillas[0]);
  }

  // DESPUÉS — siempre sincroniza con datos frescos:
  if (enrichedPlanillas.length > 0) {
      if (!selectedPlanilla) {
          setSelectedPlanilla(enrichedPlanillas[0]);
      } else {
          // Sincronizar selectedPlanilla con la versión fresca del servidor
          const updated = enrichedPlanillas.find(p => p.id === selectedPlanilla.id);
          if (updated) setSelectedPlanilla(updated);
      }
  }
  ```
- **Impacto del fix**: Afecta también el botón "Aprobar Planilla" y cualquier otra acción que llame `fetchData()` (misma causa raíz).
- **Verificación**:
  - Planilla 2026-003 (abr 2026, Mensual) creada en Borrador
  - Click "Calcular Planilla" → inmediatamente mostró:
    - Estado: Calculado ✅ (sin refresh)
    - Salario Bruto: B/.6,000.00 ✅
    - Neto a Pagar: B/.4,505.00 ✅
    - CSS: B/.1,035.00 ✅
    - SE: B/.165.00 ✅
    - ISR: B/.531.25 ✅
  - Botones cambiaron a "Aprobar Planilla" / "Recalcular" ✅
  - "Ver Detalles" mostró 3 empleados con valores correctos ✅

---

## BUG-008: RegularPay con artefacto B/.0.01 al usar Auto-llenar Regulares [BAJO] ✅ CORREGIDO
- **Archivo corregido**: `src/UI/Planilla.Web/Controllers/PayrollHeadersController.cs` (línea ~340)
- **Descripción**: Al usar el flujo correcto de planilla (Auto-llenar Regulares → Calcular), el Salario Bruto total mostraba B/.6,000.01 en lugar de B/.6,000.00. Sin Auto-llenar (cálculo directo por salario exacto), no había problema.
- **Causa raíz**: Cuando `PayrollEmployeeHours` existen, el controller calcula `RegularPay = RegularHours × HourlyRate`. El `HourlyRate` se almacena con 4 decimales: `Math.Round(4000/208, 4) = 19.2308`. Al multiplicar: `208 × 19.2308 = 4000.0064`, que al acumularse con los otros empleados (`1000.0016` Carlos + `1000.0016` Ana + `4000.0064` Roberto) suma `6000.0096` → mostrado como `B/.6,000.01`.
- **Flujo sin bug**: Sin `PayrollEmployeeHours`, el sistema usa `GetSalarioPeriodo()` = `Math.Round(4000 × 12 / 12, 2) = 4000.00` — exacto, sin error.
- **Fix aplicado** en `PayrollHeadersController.cs` (después de calcular `hours.RegularPay`):
  ```csharp
  hours.RegularPay = hours.RegularHours * hourlyRate;
  // BUG-008 FIX: Evitar artefactos de redondeo de tasa horaria en pago regular.
  // Ej: 208h × 19.2308 = 4,000.0064 → se redondea a B/.4,000.01 en lugar de B/.4,000.00.
  // Si la diferencia con el salario exacto del período es trivial (< B/.0.05), usar el exacto.
  var salarioPeriodoExacto = employee.GetSalarioPeriodo();
  if (Math.Abs(hours.RegularPay - salarioPeriodoExacto) < 0.05m)
  {
      hours.RegularPay = salarioPeriodoExacto;
  }
  ```
- **Lógica del fix**: Si las horas regulares representan el período completo (sin ausencias/extras que cambien el monto), la diferencia entre `RegularHours × HourlyRate` y `GetSalarioPeriodo()` siempre será < B/.0.05. El umbral de B/.0.05 es suficientemente seguro: ningún error de redondeo de tasa horaria con 4 decimales puede llegar a ese valor.
- **Verificación**: Planilla 2026-003 recalculada tras fix (binario recompilado):
  - Bruto total: B/.6,000.00 ✅ (era B/.6,000.01)
  - Carlos (EmpID=16): grossPay=B/.1,000.00 ✅
  - Ana (EmpID=17): grossPay=B/.1,000.00 ✅
  - Roberto (EmpID=18): grossPay=B/.4,000.00 ✅
  - Neto total: B/.4,505.00 ✅

