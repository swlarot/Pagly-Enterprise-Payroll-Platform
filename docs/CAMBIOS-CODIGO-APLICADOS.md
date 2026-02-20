# Cambios de Código Aplicados — Sesión 2026-02-19

## Resumen
Esta sesión aplicó correcciones en dos bloques:
- **FASE 1-6**: Correcciones críticas al backend (cálculos CSS, ISR, deducciones). Commit: `3fff240` (previo)
- **FASE 7-8**: Correcciones de UX/UI en frontend React (moneda, badges, símbolos). Commit: `3fff240` (mismo commit final)

---

## BLOQUE 1 — Correcciones Backend (FASE 1-6)

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

## BLOQUE 2 — Correcciones UX/UI Frontend (FASE 7-8)

## Cambio C-005: formatCurrency — Corregir USD → B/. en 10 páginas React

**Archivos corregidos** (mismo patrón en todos):

| Archivo | Línea aprox. |
|---------|-------------|
| `ReportesPage.jsx` | 82 |
| `PlanillasPage.jsx` | 325 |
| `PrestamosPage.jsx` | 82 |
| `AnticiposPage.jsx` | 65 |
| `DeduccionesPage.jsx` | 178 |
| `HorasExtraPage.jsx` | 281 |
| `AusenciasPage.jsx` | 84 |
| `PosicionesPage.jsx` | 168 |
| `MiPerfilPage.tsx` | 51 |
| `AdminDashboardPage.tsx` | 112 |

**Fix aplicado** (mismo patrón en todos los archivos JS/JSX/TSX):
```js
// ANTES:
const formatCurrency = (amount) => {
  return new Intl.NumberFormat('es-PA', { style: 'currency', currency: 'USD' }).format(amount);
};

// DESPUÉS:
const formatCurrency = (amount) => {
  return 'B/. ' + new Intl.NumberFormat('es-PA', {
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
  }).format(amount || 0);
};
```

**Fix adicional en AdminDashboardPage.tsx** — `formatCurrencyShort`:
```ts
// ANTES:
const formatCurrencyShort = (amount: number) => {
  if (amount >= 1000) { return `$${(amount / 1000).toFixed(1)}k`; }
  return formatCurrency(amount);
};

// DESPUÉS:
const formatCurrencyShort = (amount: number) => {
  if (amount >= 1000) { return `B/.${(amount / 1000).toFixed(1)}k`; }
  return formatCurrency(amount);
};
```

**Fix adicional en PosicionesPage.jsx** — guard `|| 0`:
```js
// ANTES: formatCurrency(value)
// DESPUÉS: formatCurrency(amount || 0)
```

---

## Cambio C-006: ReportesPage.jsx — Campos Consolidado por Acreedor

**Archivo:** `src/UI/Planilla.Web/ClientApp/src/pages/ReportesPage.jsx`

**Problema:** El frontend leía `item.montoTotal` pero la API retorna `item.totalAplicado`. El total general estaba en `reporteData.granTotalAplicado` (root), no en `reporteData.totales.granTotal`.

**Fix en fila de acreedor** (~línea 409):
```jsx
// ANTES:
{formatCurrency(item.montoTotal || item.monto || 0)}

// DESPUÉS:
{formatCurrency(item.totalAplicado ?? item.montoTotal ?? item.monto ?? 0)}
```

**Fix en fila de totales** (~línea 430):
```jsx
// ANTES:
{reporteData.totales && (
  <td>{reporteData.totales.totalEmpleados || ''}</td>
  <td>{formatCurrency(reporteData.totales.granTotal || reporteData.totales.totalMonto || 0)}</td>
)}

// DESPUÉS:
{(reporteData.granTotalAplicado != null || reporteData.totales) && (
  <td>{reporteData.totalAcreedores || reporteData.totales?.totalEmpleados || ''}</td>
  <td>{formatCurrency(reporteData.granTotalAplicado ?? reporteData.totales?.granTotal ?? 0)}</td>
)}
```

---

## Cambio C-007: PlanillasPage.jsx — Badges de estado en español

**Archivo:** `src/UI/Planilla.Web/ClientApp/src/pages/PlanillasPage.jsx` (~línea 332)

**Fix aplicado en `getStatusBadge()`:**
```js
const statuses = {
  0: { label: 'Borrador',  color: 'bg-gray-100 text-gray-700' },
  1: { label: 'Calculado', color: 'bg-blue-100 text-blue-700' },
  2: { label: 'Aprobado',  color: 'bg-green-100 text-green-700' },
  3: { label: 'Pagado',    color: 'bg-purple-100 text-purple-700' },
  4: { label: 'Cancelado', color: 'bg-red-100 text-red-700' },
};
```

---

## Cambio C-008: ConfiguracionPage.jsx — Símbolo "$" → "B/."

**Archivo:** `src/UI/Planilla.Web/ClientApp/src/pages/ConfiguracionPage.jsx`

**Ocurrencias corregidas:**

1. **Topes CSS** (template literals con backtick):
   ```jsx
   // ANTES: Topes: ${Number(taxConfig.cssMax...)}
   // DESPUÉS: Topes: B/.{Number(taxConfig.cssMax...)}
   ```

2. **Tabla ISR brackets**:
   ```jsx
   // ANTES: $0 - $11,000  |  $11,001 - $50,000  |  $50,001+
   // DESPUÉS: B/.0 - B/.11,000  |  B/.11,001 - B/.50,000  |  B/.50,001+
   ```

3. **Ejemplo de cálculo**:
   ```jsx
   // ANTES: Si ingreso anual es $30,000 ... base gravable = $19,000
   // DESPUÉS: Si ingreso anual es B/.30,000 ... base gravable = B/.19,000
   ```

4. **Deducciones permitidas**:
   ```jsx
   // ANTES: hasta $5,000  |  hasta $20,000
   // DESPUÉS: hasta B/.5,000  |  hasta B/.20,000
   ```

5. **Icono DollarSign** reemplazado por texto:
   ```jsx
   // ANTES: <DollarSign className="h-5 w-5 text-gray-500" />
   // DESPUÉS: <span className="text-gray-500 text-sm font-medium">B/.</span>
   ```

6. **Import limpieza** — eliminado `DollarSign` de la lista de imports de `lucide-react`.

---

## Archivos Modificados (commit 3fff240)

```
# Backend
M src/Core/Planilla.Application/Services/CssCalculationServicePortable.cs
M src/Core/Planilla.Application/Services/IncomeTaxCalculationServicePortable.cs
M src/Core/Planilla.Application/Services/PayrollCalculationOrchestratorPortable.cs
M src/Infrastructure/Planilla.Infrastructure/Data/PayrollConfigSeeder.cs
M src/Infrastructure/Planilla.Infrastructure/Services/ReportesService.cs
M src/Infrastructure/Planilla.Infrastructure/Services/PayrollProcessingService.cs
M src/UI/Planilla.Web/Controllers/PayrollHeadersController.cs
M tests/Planilla.Application.Tests/Helpers/MockPayrollConfigProvider.cs
M tests/Planilla.Application.Tests/Services/CssCalculationServiceTests.cs

# Frontend React
M src/UI/Planilla.Web/ClientApp/src/pages/AdminDashboardPage.tsx
M src/UI/Planilla.Web/ClientApp/src/pages/AnticiposPage.jsx
M src/UI/Planilla.Web/ClientApp/src/pages/AusenciasPage.jsx
M src/UI/Planilla.Web/ClientApp/src/pages/ConfiguracionPage.jsx
M src/UI/Planilla.Web/ClientApp/src/pages/DeduccionesPage.jsx
M src/UI/Planilla.Web/ClientApp/src/pages/HorasExtraPage.jsx
M src/UI/Planilla.Web/ClientApp/src/pages/MiPerfilPage.tsx
M src/UI/Planilla.Web/ClientApp/src/pages/PlanillasPage.jsx
M src/UI/Planilla.Web/ClientApp/src/pages/PosicionesPage.jsx
M src/UI/Planilla.Web/ClientApp/src/pages/PrestamosPage.jsx
M src/UI/Planilla.Web/ClientApp/src/pages/ReportesPage.jsx

# Config / Static
M src/UI/Planilla.Web/appsettings.json
M src/UI/Planilla.Web/wwwroot/app.css
M src/UI/Planilla.Web/wwwroot/app.js
```

---

---

## Sesión 2026-02-20 — Cambios adicionales

## Cambio C-009: PlanillasPage.jsx — Sincronizar selectedPlanilla tras fetchData (BUG-007)

**Archivo:** `src/UI/Planilla.Web/ClientApp/src/pages/PlanillasPage.jsx` (línea 134)

**Problema:** Después de Calcular/Aprobar/Recalcular, `fetchData()` actualizaba el array `planillas` pero `selectedPlanilla` (fuente de datos de todas las tarjetas resumen) quedaba con la versión vieja. El usuario tenía que refrescar la página para ver los resultados.

**Fix aplicado:**
```js
// ANTES:
if (enrichedPlanillas.length > 0 && !selectedPlanilla) {
    setSelectedPlanilla(enrichedPlanillas[0]);
}

// DESPUÉS:
if (enrichedPlanillas.length > 0) {
    if (!selectedPlanilla) {
        setSelectedPlanilla(enrichedPlanillas[0]);
    } else {
        // BUG-007 FIX: Sincronizar selectedPlanilla con la versión fresca del servidor.
        // Sin esto, después de Calcular/Aprobar la UI muestra datos viejos hasta refrescar.
        const updated = enrichedPlanillas.find(p => p.id === selectedPlanilla.id);
        if (updated) setSelectedPlanilla(updated);
    }
}
```

**Verificación:** Planilla 2026-003 calculada → datos aparecen inmediatamente sin refresh.

---

## PENDIENTE — C-010: Dashboard desglose CSS/SE/Riesgo Patronal (backend)

**Estado**: ❌ PENDIENTE (próxima sesión)

**Archivo backend**: Agregar campos a `PayrollHeaderDto`:
- `TotalEmployerCss` (decimal)
- `TotalEmployerSe` (decimal)
- `TotalRiskInsurance` (decimal)

**Archivo frontend**: `AdminDashboardPage.tsx` — leer los nuevos campos en el panel de "Costo Total Empleador".

**Referencia**: PENDIENTE-001 en BUGS-ENCONTRADOS.md
