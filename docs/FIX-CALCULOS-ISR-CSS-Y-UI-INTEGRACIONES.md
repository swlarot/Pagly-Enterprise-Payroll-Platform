# Fix Bugs Criticos de Calculo (ISR + CSS) + 3 Integraciones UI

**Fecha:** 2026-02-17
**Impacto:** CRITICO — Todos los empleados afectados por retenciones incorrectas
**Archivos modificados:** 7

---

## Bug 1 — ISR FixedAmount se contaba doble

### Problema

En `IncomeTaxCalculationServicePortable.cs`, el metodo `ApplyTaxBracketsAsync` iteraba por TODOS los tramos de ISR y en cada iteracion sumaba `bracketTax + bracket.FixedAmount`. El `FixedAmount` es un monto acumulado que representa el impuesto de tramos anteriores, pero el loop ya calculaba esos tramos explicitamente. Resultado: doble conteo.

### Ejemplo del error

Para Juan Gonzalez con ingreso anual de $65,907.84:

| Tramo | MinIncome | Rate | FixedAmount | Bug: sumaba |
|-------|-----------|------|-------------|-------------|
| 1 | $0 | 0% | $0 | $0 |
| 2 | $11,000.01 | 15% | $0 | $5,850 (38,999.99 x 15%) |
| 3 | $50,000.01 | 25% | $5,850 | $3,976.96 + $5,850 |

- **Total BUGGY:** $5,850 + $9,826.96 = **$15,676.96/anio** ($653.21/quincena)
- **Total CORRECTO:** $5,850 + $3,976.96 = **$9,826.96/anio** ($409.46/quincena)
- **Diferencia:** $243.75/quincena de mas por empleado en ese tramo

### Fix aplicado

Reemplazo del loop iterativo por algoritmo de tramo unico:
1. Encontrar el tramo donde cae el ingreso (ultimo donde MinIncome < taxableIncome)
2. ISR = FixedAmount + (ingreso - MinIncome) x Rate

### Archivo modificado

```
src/Core/Planilla.Application/Services/IncomeTaxCalculationServicePortable.cs
```

---

## Bug 2 — CSS Tope mensual no se prorrateaba por periodo de pago

### Problema

En `CssCalculationServicePortable.cs`, los metodos `CalculateEmployeeCssAsync`, `CalculateEmployerCssAsync` y `CalculateRiskContributionAsync` comparaban `grossPay` (salario del PERIODO, ej: quincenal) contra `cap` (tope MENSUAL de la configuracion). Para un empleado quincenal con tope estandar de $1,500/mes, el tope quincenal deberia ser $750, no $1,500.

### Ejemplo del error

Juan Gonzalez, bruto quincenal $2,746.16, tope estandar $1,500/mes:

| Concepto | Bug | Fix |
|----------|-----|-----|
| Tope quincenal | $1,500 (mensual sin prorratear) | $750 ($1,500 x 12 / 24) |
| Base CSS | min($2,746.16, $1,500) = $1,500 | min($2,746.16, $750) = $750 |
| CSS 9.75% | $146.25 | $73.13 |
| Diferencia | | $73.12/quincena de mas |

### Fix aplicado

1. Agregado parametro `string payFrequency` a los 4 metodos del servicio CSS
2. Calculo de tope prorrateado: `periodCap = cap * 12 / periodsPerYear`
3. Actualizado el orchestrator para pasar `payFrequency` al servicio CSS
4. Actualizados tests unitarios con el nuevo parametro (usando "Mensual" para mantener assertions)

### Archivos modificados

```
src/Core/Planilla.Application/Services/CssCalculationServicePortable.cs       (4 metodos)
src/Core/Planilla.Application/Services/PayrollCalculationOrchestratorPortable.cs (1 llamada)
tests/Planilla.Application.Tests/Services/CssCalculationServiceTests.cs       (7 tests)
```

---

## UI 1 — Tab "Salario Minimo" en Configuracion

### Que se hizo

Se integro el contenido de `SalarioMinimoPage.jsx` como un tab dentro de `ConfiguracionPage.jsx`, posicionado despues de "Tabla ISR" y antes de "Audit Log".

### Funcionalidad

- Carga el salario minimo actual via `GET /api/configuracion/salario-minimo`
- Permite editarlo via `PUT /api/configuracion/salario-minimo`
- Campo de actividad economica (opcional)
- Notas sobre proteccion de salario minimo inembargable
- Adaptado al tema dark navy del sistema

### Archivo modificado

```
src/UI/Planilla.Web/ClientApp/src/pages/ConfiguracionPage.jsx
```

---

## UI 2 — Cards de reportes nuevos en /reportes

### Que se hizo

Se agregaron 2 cards nuevas despues de "Horas Extra" y antes del banner "Mas reportes en camino":

**Card 1: Consolidado por Acreedor**
- Icono: Building2 (emerald/green)
- Endpoints: GET/Excel/PDF `/api/reportes/consolidado-acreedor/{planillaId}`
- Modal con tabla: Acreedor | Tipo | Empleados | Monto Total

**Card 2: Deducciones por Empleado**
- Icono: Users (blue)
- Endpoints: GET/Excel/PDF `/api/reportes/deducciones-empleado/{planillaId}`
- Modal con tabla: Cedula | Nombre | Bruto | CSS+SE+ISR | Pension | Embargos | Voluntarias | Total Ded. | Neto

### Archivo modificado

```
src/UI/Planilla.Web/ClientApp/src/pages/ReportesPage.jsx
```

---

## UI 3 — Desglose de deducciones en modal "Ver Detalles" de planilla

### Que se hizo

Se amplio la tabla del modal de detalles de planilla de 7 a 10 columnas:

**Antes:**
```
EMPLEADO | BRUTO | CSS | SE | ISR | TOTAL DED. | NETO
```

**Despues:**
```
EMPLEADO | BRUTO | CSS | SE | ISR | PENSION | EMBARGOS | VOLUNT. | TOTAL DED. | NETO
```

- Columna PENSION: color rojo
- Columna EMBARGOS: color naranja
- Columna VOLUNT.: color azul
- Badge "SM" amarillo en NETO cuando hay limitacion por salario minimo
- Totales en el footer incluyen las 3 nuevas columnas

### Campos del backend usados

Los campos ya existian en la entidad `PayrollDetail`:
- `PensionAlimenticia` (decimal)
- `Embargos` (decimal)
- `DeduccionesVoluntarias` (decimal)
- `TuvoLimitacionSalarioMinimo` (bool)

### Archivo modificado

```
src/UI/Planilla.Web/ClientApp/src/pages/PlanillasPage.jsx
```

---

## Verificacion de builds

| Build | Resultado |
|-------|-----------|
| `dotnet build` | 0 errores, 0 warnings |
| `npm run build` | 0 errores |
| Linter | 0 errores en archivos modificados |

---

## Tabla de verificacion de calculos

### ISR — Empleado quincenal $2,746.16 (anual $65,907.84)

| Concepto | Valor esperado |
|----------|---------------|
| Tramo aplicable | Order=3, MinIncome=$50,000.01, Rate=25%, FixedAmount=$5,850 |
| Excedente | $65,907.84 - $50,000.01 = $15,907.83 |
| ISR tramo | $15,907.83 x 25% = $3,976.96 |
| ISR anual | $5,850 + $3,976.96 = $9,826.96 |
| ISR quincenal | $9,826.96 / 24 = **$409.46** |

### CSS — Empleado quincenal $2,746.16, tope estandar $1,500/mes

| Concepto | Valor esperado |
|----------|---------------|
| Tope quincenal | $1,500 x 12 / 24 = **$750.00** |
| Base CSS | min($2,746.16, $750) = $750.00 |
| CSS empleado 9.75% | **$73.13** |

### CSS — Empleado quincenal $348.38, tope estandar $1,500/mes

| Concepto | Valor esperado |
|----------|---------------|
| Tope quincenal | $750.00 |
| Base CSS | min($348.38, $750) = $348.38 (tope no aplica) |
| CSS empleado 9.75% | **$33.97** |
