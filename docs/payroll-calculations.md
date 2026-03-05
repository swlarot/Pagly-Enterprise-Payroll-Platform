# Cálculo de Planilla — Documentación Técnica

**Versión**: 2.0.0
**Última actualización**: 04/03/2026
**Autor**: Planilla Documentation Team
**Aplica a**: República de Panamá — Ley 51 de 2005 (CSS), Ley 462 de 2024 (Reforma CSS), Ley 37 de 2018 (ISR), Código de Trabajo

---

## Tabla de Contenidos

1. [Visión General del Motor de Cálculo](#1-visión-general-del-motor-de-cálculo)
2. [Tasas Vigentes](#2-tasas-vigentes)
3. [Fases de la Reforma CSS — Ley 462](#3-fases-de-la-reforma-css--ley-462)
4. [Riesgo Profesional — Acuerdo N°2 de 1995](#4-riesgo-profesional--acuerdo-n2-de-1995)
5. [Topes de Pensión CSS](#5-topes-de-pensión-css)
6. [Frecuencias de Pago y Prorrateo](#6-frecuencias-de-pago-y-prorrateo)
7. [Fórmulas de Cálculo Detalladas](#7-fórmulas-de-cálculo-detalladas)
8. [Motor de Deducciones Adicionales](#8-motor-de-deducciones-adicionales)
9. [Ejemplo Práctico Completo](#9-ejemplo-práctico-completo)
10. [Configuración en Base de Datos](#10-configuración-en-base-de-datos)
11. [Implementación en Código](#11-implementación-en-código)
12. [Notas Legales y Referencias](#12-notas-legales-y-referencias)

---

## 1. Visión General del Motor de Cálculo

El motor de cálculo de planilla está compuesto por cuatro servicios portables que operan en cascada:

```
GrossPay (salario bruto ajustado)
    │
    ├─► CssCalculationServicePortable       → CSS empleado + CSS patronal + Riesgo
    ├─► EducationalInsuranceServicePortable → SE empleado + SE patronal
    ├─► IncomeTaxCalculationServicePortable → ISR (proyección anual → período)
    │
    └─► PayrollCalculationOrchestratorPortable
            │
            └─► DeduccionPrioridadEngine
                    ├─ Pensión alimenticia (prioridad absoluta)
                    ├─ Embargos judiciales (sobre excedente s/salario mínimo)
                    └─ Voluntarias — préstamos, anticipos (límite 50% bruto)
```

El orquestador principal es `PayrollCalculationOrchestratorPortable`, que invoca los tres servicios de deducciones legales y devuelve `PayrollCalculationResult`. El `PayrollProcessingService` extiende ese resultado agregando asistencia (horas extra, ausencias, vacaciones) y las deducciones adicionales con prelación legal.

### Flujo secuencial de `PayrollProcessingService`

| Paso | Acción | Descripción |
|------|--------|-------------|
| 1 | Calcular asistencia | Horas extra aprobadas, ausencias injustificadas, vacaciones |
| 2 | Ajustar GrossPay | `SalarioPeriodo + MontoHorasExtra − DescuentoAusencias` |
| 3 | Calcular deducciones legales | CSS + SE + ISR sobre GrossPay ajustado |
| 4 | Motor de prelación | Pensión alimenticia → embargos → voluntarias |
| 5 | Calcular NetPay | `GrossPay − TotalDeducciones` |

---

## 2. Tasas Vigentes

### 2.1 Resumen de Tasas Actuales (Vigente desde 01/01/2026)

| Concepto | Empleado | Patronal | Base | Tope |
|----------|----------|----------|------|------|
| CSS | 9.75% | 13.25%* | Salario bruto completo | Sin tope de cotización |
| Riesgo profesional | — | 0.56% al 5.67% | Salario bruto completo | Sin tope |
| Seguro Educativo | 1.25% | 1.50% | Salario bruto completo | Sin tope |
| ISR | Escalonado | — | Ingreso anual neto gravable | — |

_*Tasa patronal CSS escalonada según Reforma CSS — ver seccion 3._

### 2.2 Caja de Seguro Social (CSS)

**Base legal**: Ley 462 de 2024, Art. 178

- **Empleado**: 9.75% sobre el salario bruto completo del período.
- **No existe tope de cotización CSS**: La cotización se calcula sobre el total del salario bruto, sin aplicar ningún techo. El comentario en el código fuente es explícito:

```csharp
// No existe tope de cotización CSS (Art. 178 Ley 462 — tope aplica solo a pensión)
var contributionBase = grossPay;
```

> **Distinción fundamental**: Los valores B/.1,500, B/.2,000 y B/.2,500 son **topes de la pensión** que el asegurado podrá recibir al jubilarse, no topes para calcular la cotización mensual. Ver sección 5.

### 2.3 Seguro Educativo (SE)

**Base legal**: Decreto de Gabinete N°144 de 1971

- **Empleado**: 1.25% sobre salario bruto completo.
- **Patronal**: 1.50% sobre salario bruto completo.
- **Sin tope máximo**: Se aplica siempre sobre el salario total del período.

```csharp
// Seguro Educativo NO tiene tope máximo, se aplica sobre el salario total
var rate = config.EducationalInsuranceEmployeeRate; // 1.25%
var amount = RoundingPolicy.CalculatePercentage(grossPay, rate);
```

### 2.4 Impuesto Sobre la Renta (ISR)

**Base legal**: Ley 37 de 2018 + Decreto Ejecutivo 368 de 2018

Tramos anuales vigentes:

| Tramo | Ingreso Anual Gravable | Tasa | Monto Fijo |
|-------|------------------------|------|------------|
| Exento | B/.0.00 — B/.11,000.00 | 0% | B/.0.00 |
| Intermedio | B/.11,000.01 — B/.50,000.00 | 15% | B/.0.00 |
| Superior | Más de B/.50,000.00 | 25% | B/.5,850.00 |

El monto fijo de B/.5,850.00 en el tramo superior equivale al 15% acumulado sobre B/.39,000 (el rango entre B/.11,000 y B/.50,000).

---

## 3. Fases de la Reforma CSS — Ley 462

La Ley 462 de 2024 establece un incremento gradual de la cuota patronal de CSS en tres fases. El sistema detecta automáticamente la fase correcta según la fecha de cálculo de la planilla, consultando la tabla `PayrollTaxConfigurations`.

### 3.1 Tabla de Fases

| Fase | Período | Tasa Patronal | Registro en BD |
|------|---------|---------------|----------------|
| Fase 1 | Hasta 28/02/2027 | **13.25%** | `EffectiveStartDate = 2026-01-01`, `EffectiveEndDate = 2027-02-28` |
| Fase 2 | 01/03/2027 — 28/02/2029 | **14.25%** | `EffectiveStartDate = 2027-03-01`, `EffectiveEndDate = 2029-02-28` |
| Fase 3 | Desde 01/03/2029 | **15.25%** | `EffectiveStartDate = 2029-03-01`, `EffectiveEndDate = null` (indefinido) |

### 3.2 Selección de Configuración Vigente

El sistema selecciona la configuración activa aplicando el siguiente filtro en la base de datos:

```csharp
var config = await _context.PayrollTaxConfigurations
    .Where(c => c.IsActive &&
                c.EffectiveStartDate <= fechaPlanilla &&
                (c.EffectiveEndDate == null || c.EffectiveEndDate >= fechaPlanilla))
    .OrderByDescending(c => c.EffectiveStartDate)
    .FirstOrDefaultAsync();
```

Si no existe una configuración activa para la fecha de cálculo, el servicio lanza `InvalidOperationException` — no existe ningún valor por defecto silencioso.

### 3.3 Seeding Automático

Al arrancar la aplicación (`Program.cs` → `MigrateAsync()`), el `PayrollConfigSeeder` crea las tres fases para cada tenant activo de forma idempotente. Al crear un nuevo tenant, se invoca `PayrollConfigSeeder.SeedForNewTenantAsync()`.

---

## 4. Riesgo Profesional — Acuerdo N°2 de 1995

El riesgo profesional es una cuota patronal cuya tasa depende de la actividad económica y el nivel de riesgo del puesto del empleado. Está regulado por el Acuerdo N°2 de 1995 de la Junta Directiva de la CSS.

### 4.1 Cinco Clases de Riesgo

| Clase | Tasa | Actividades Típicas |
|-------|------|---------------------|
| I — Riesgo Mínimo | **0.56%** | Oficinas, administración, comercio, servicios financieros |
| II | **0.98%** | — |
| III — Riesgo Medio | **2.10%** | Transporte, manufactura, industria ligera |
| IV | **3.64%** | — |
| V — Riesgo Máximo | **5.67%** | Construcción, maquinaria pesada, minería, actividades de alto riesgo |

### 4.2 Almacenamiento en el Empleado

La clase de riesgo se almacena directamente como porcentaje en la entidad `Empleado`:

```csharp
/// <summary>
/// Porcentaje de riesgo profesional CSS (Acuerdo N°2 de 1995):
/// 0.56 = Clase I (Riesgo Mínimo), 0.98 = Clase II,
/// 2.10 = Clase III (Riesgo Medio), 3.64 = Clase IV, 5.67 = Clase V (Riesgo Máximo)
/// </summary>
[Column(TypeName = "decimal(5, 2)")]
public decimal CssRiskPercentage { get; set; } = 0.56m;
```

El valor por defecto es 0.56% (Clase I — Riesgo Mínimo).

### 4.3 Cálculo sin Lookup en BD

A diferencia de versiones anteriores que consultaban la tasa en la base de datos, el cálculo ahora usa directamente el porcentaje almacenado en el empleado:

```csharp
// Usar la tasa del empleado directamente (Acuerdo N°2 de 1995)
// El campo CssRiskPercentage almacena el valor como porcentaje (ej: 2.10 = 2.10%)
decimal riskRate = cssRiskPercentage;
var amount = RoundingPolicy.CalculatePercentage(contributionBase, riskRate);
```

### 4.4 Opciones en el Frontend

El selector en la interfaz (`CSS_RISK_OPTIONS` en `payroll.ts`) expone las cinco clases:

```typescript
export const CSS_RISK_OPTIONS = [
  { value: 0.56, label: '0.56% — Clase I (Riesgo Mínimo: oficinas, administración, comercio)' },
  { value: 0.98, label: '0.98% — Clase II' },
  { value: 2.10, label: '2.10% — Clase III (Riesgo Medio: transporte, manufactura)' },
  { value: 3.64, label: '3.64% — Clase IV' },
  { value: 5.67, label: '5.67% — Clase V (Riesgo Máximo: construcción, maquinaria, minería)' },
];
```

---

## 5. Topes de Pensión CSS

Los topes de pensión determinan el **monto máximo de jubilación mensual** que un asegurado puede recibir. No afectan el cálculo de cotización del período.

### 5.1 Estructura de Topes

| Nivel | Años Cotizados Mínimo | Salario Promedio Mínimo (10 años) | Tope Pensión Mensual |
|-------|----------------------|----------------------------------|----------------------|
| Estándar | Menos de 25 años | Cualquiera | B/.1,500.00 |
| Intermedio | 25 años o más | B/.2,000.00 o más | B/.2,000.00 |
| Alto | 30 años o más | B/.2,500.00 o más | B/.2,500.00 |

### 5.2 Campos del Empleado Relacionados

```csharp
// Años cotizados en CSS. Mínimo 20 años (240 cuotas) para jubilación.
public int YearsCotized { get; set; } = 0;

// Salario promedio últimos 10 años (para determinar tope CSS alto)
public decimal AverageSalaryLast10Years { get; set; } = 0;
```

### 5.3 Parámetros en Configuración

```
CssIntermediateMinYears = 25        // Años mínimos para tope intermedio
CssIntermediateMinAvgSalary = 2000  // Salario promedio mínimo para tope intermedio
CssHighMinYears = 30                // Años mínimos para tope alto
CssHighMinAvgSalary = 2500          // Salario promedio mínimo para tope alto
```

### 5.4 Uso en el Sistema

El tope de pensión se reporta en `CssCalculationResult.MaxContributionBase` como referencia informativa para el registro del recibo de nómina. **No limita el importe calculado de la cotización CSS**.

---

## 6. Frecuencias de Pago y Prorrateo

El sistema admite cuatro frecuencias de pago. El `SalarioBase` del empleado siempre se almacena como valor **mensual**. La conversión al período se realiza mediante:

```
SalarioPeriodo = SalarioBase × 12 / PeríodosAño
```

### 6.1 Tabla de Frecuencias

| Frecuencia | Enum | Períodos/Año | Horas/Período (48 h/sem) |
|------------|------|--------------|--------------------------|
| Semanal | `PayPeriodType.Semanal` (0) | 52 | 48 |
| Bisemanal | `PayPeriodType.Bisemanal` (1) | 26 | 96 |
| Quincenal | `PayPeriodType.Quincenal` (2) | 24 | ~104 |
| Mensual | `PayPeriodType.Mensual` (3) | 12 | ~208 |

### 6.2 Prorrateo del ISR

Para calcular el ISR, el ingreso del período se **anualiza** antes de aplicar los tramos:

```
IngresoAnual = SalarioPeriodo × PeríodosAño
```

Luego el impuesto anual calculado se divide entre los períodos del año para obtener la retención del período:

```
RetenciónPeriodo = ImpuestoAnual / PeríodosAño
```

### 6.3 Sincronización de Campos Legacy

La entidad `Empleado` mantiene tanto `PayPeriodType` (enum) como `PayFrequency` (string legacy). Deben mantenerse sincronizados mediante `SyncPayFrequencyFromType()`:

```csharp
public void SyncPayFrequencyFromType()
{
    PayFrequency = PayPeriodType switch
    {
        PayPeriodType.Semanal    => "Semanal",
        PayPeriodType.Bisemanal  => "Bisemanal",
        PayPeriodType.Quincenal  => "Quincenal",
        PayPeriodType.Mensual    => "Mensual",
        _ => "Quincenal"
    };
}
```

---

## 7. Fórmulas de Cálculo Detalladas

### 7.1 Salario Bruto Ajustado

```
GrossPay = SalarioPeriodo + MontoHorasExtra − DescuentoAusencias
```

Donde:
- `SalarioPeriodo = SalarioBase × 12 / PeríodosAño`
- `MontoHorasExtra`: suma de horas extra aprobadas valoradas por tipo (ver sección horas extra)
- `DescuentoAusencias`: días de ausencia injustificada × (SalarioMensual / 30)

### 7.2 CSS del Empleado

```
CSS_Empleado = GrossPay × 9.75%
```

No se aplica ningún tope. El cálculo siempre opera sobre el GrossPay completo.

### 7.3 CSS Patronal (tasa según fase)

```
CSS_Patronal = GrossPay × TasaFase
```

Donde `TasaFase` es 13.25%, 14.25% o 15.25% según la fecha de la planilla (ver sección 3).

### 7.4 Riesgo Profesional

```
Riesgo = GrossPay × CssRiskPercentage
```

`CssRiskPercentage` es el valor almacenado en el empleado (0.56, 0.98, 2.10, 3.64 o 5.67).

### 7.5 Seguro Educativo

```
SE_Empleado = GrossPay × 1.25%
SE_Patronal = GrossPay × 1.50%
```

Sin tope máximo en ninguno de los dos casos.

### 7.6 ISR — Impuesto Sobre la Renta

**Paso 1 — Anualizar ingreso:**

```
IngresoAnual = GrossPay × PeríodosAño
```

**Paso 2 — Calcular deducción por dependientes:**

```
DeducciónDependientes = NúmeroDependientes × B/.800
```

No existe límite legal en el número de dependientes (Ley 37/2018 + Decreto 368/2018). `MaxDependents = 99` en la configuración actúa únicamente como validación de entrada.

**Paso 3 — Ingreso neto gravable:**

```
IngresoNetoGravable = MAX(0, IngresoAnual − DeducciónDependientes)
```

**Paso 4 — Aplicar tramos progresivos:**

```
Si IngresoNetoGravable ≤ 11,000:
    ImpuestoAnual = 0

Si 11,000 < IngresoNetoGravable ≤ 50,000:
    ImpuestoAnual = (IngresoNetoGravable − 11,000) × 15%

Si IngresoNetoGravable > 50,000:
    ImpuestoAnual = 5,850 + (IngresoNetoGravable − 50,000) × 25%
```

**Paso 5 — Retención del período:**

```
RetenciónPeriodo = REDONDEAR(ImpuestoAnual / PeríodosAño, 2 decimales)
```

### 7.7 Neto a Pagar

```
TotalDeducciones = CSS_Empleado + SE_Empleado + ISR
                 + PensiónAlimenticia + Embargos + Voluntarias

NetPay = GrossPay − TotalDeducciones
```

### 7.8 Costo Total Patronal

```
CostoPatronal = CSS_Patronal + Riesgo + SE_Patronal
```

---

## 8. Motor de Deducciones Adicionales

El `DeduccionPrioridadEngine` aplica las deducciones voluntarias y judiciales después de calcular las deducciones legales (CSS, SE, ISR), siguiendo el orden de prelación establecido en el Código de Trabajo de Panamá.

### 8.1 Orden de Prelación

| Prioridad | Categoría | Límite de Aplicación |
|-----------|-----------|----------------------|
| 1 | Pensión Alimenticia | Puede reducir el neto por debajo del salario mínimo por orden judicial |
| 2 | Embargos Judiciales | Solo sobre el excedente por encima del salario mínimo del período |
| 3 | Voluntarias (préstamos, anticipos, sindicato, cooperativas) | Máximo 50% del salario bruto del período |

### 8.2 Cálculo del Excedente Embargable

```
ExcedenteEmbargable = MAX(0, SaldoDisponiblePostPensión − SalarioMínimoPeriodo)
```

Donde `SalarioMínimoPeriodo = SalarioMínimoMensual × 12 / PeríodosAño`.

### 8.3 Límite para Deducciones Voluntarias

```
LímiteVoluntarias = MIN(SaldoDisponible, GrossPay × 50%)
```

La ley panameña prohíbe que las deducciones voluntarias superen el 50% del salario bruto del período. Este límite aplica al total acumulado de todas las deducciones voluntarias, no a cada una individualmente.

### 8.4 Tipos de Deducciones Registradas

| Tipo | Categoría | Fuente |
|------|-----------|--------|
| Pensión Alimenticia | PensionAlimenticia | DeduccionFija (TipoDeduccion.PensionAlimenticia) |
| Embargo Judicial | EmbargoJudicial | DeduccionFija (TipoDeduccion.Embargo) |
| Préstamo Bancario con expediente | EmbargoJudicial | DeduccionFija (PrestamoBancario + NumeroExpediente) |
| Préstamo Interno | Voluntaria | Entidad Prestamo |
| Anticipo | Voluntaria | Entidad Anticipo |
| Deducción fija voluntaria | Voluntaria | DeduccionFija (otros tipos) |

---

## 9. Ejemplo Práctico Completo

### Datos del empleado

| Campo | Valor |
|-------|-------|
| Nombre | María González |
| Salario mensual | B/.2,000.00 |
| Frecuencia de pago | Quincenal (24 períodos/año) |
| Clase de riesgo | III — 2.10% (manufactura) |
| Años cotizados | 10 |
| Dependientes | 2 |
| CSS | Sujeto (Sí) |
| SE | Sujeto (Sí) |
| ISR | Sujeto (Sí) |
| Fase CSS activa | Fase 1 — 13.25% patronal |

### Paso 1 — Salario del período (quincenal)

```
SalarioPeriodo = B/.2,000.00 × 12 / 24 = B/.1,000.00
```

Sin horas extra ni ausencias en este período, `GrossPay = B/.1,000.00`.

### Paso 2 — CSS del empleado

```
CSS_Empleado = B/.1,000.00 × 9.75% = B/.97.50
```

### Paso 3 — CSS patronal

```
CSS_Patronal = B/.1,000.00 × 13.25% = B/.132.50
```

### Paso 4 — Riesgo profesional

```
Riesgo = B/.1,000.00 × 2.10% = B/.21.00
```

### Paso 5 — Seguro Educativo

```
SE_Empleado = B/.1,000.00 × 1.25% = B/.12.50
SE_Patronal = B/.1,000.00 × 1.50% = B/.15.00
```

### Paso 6 — ISR (proyección anual)

```
IngresoAnual        = B/.1,000.00 × 24       = B/.24,000.00
DeducciónDependientes = 2 × B/.800.00        = B/.1,600.00
IngresoNetoGravable = B/.24,000.00 − B/.1,600.00 = B/.22,400.00
```

Tramo aplicable: 15% sobre excedente de B/.11,000

```
ImpuestoAnual = (B/.22,400.00 − B/.11,000.00) × 15%
              = B/.11,400.00 × 15%
              = B/.1,710.00

RetenciónPeriodo = B/.1,710.00 / 24 = B/.71.25
```

### Paso 7 — Resumen del recibo de nómina

| Concepto | Empleado (descuento) | Patronal (costo) |
|----------|---------------------|------------------|
| Salario bruto | B/.1,000.00 | — |
| CSS | −B/.97.50 | B/.132.50 |
| Seguro Educativo | −B/.12.50 | B/.15.00 |
| Riesgo profesional | — | B/.21.00 |
| ISR | −B/.71.25 | — |
| **Total deducciones empleado** | **−B/.181.25** | — |
| **Neto a pagar** | **B/.818.75** | — |
| **Costo total patronal** | — | **B/.168.50** |

### Paso 8 — Verificación de tope de pensión

Con 10 años cotizados, el tope de pensión aplicable es el **Estándar (B/.1,500.00)**. Este valor se registra en el recibo como referencia pero no altera los montos calculados.

---

## 10. Configuración en Base de Datos

### 10.1 Tabla PayrollTaxConfigurations

Almacena las configuraciones vigentes por fecha. Se usa un registro por fase de la Reforma CSS y por tenant.

| Campo | Tipo | Descripción |
|-------|------|-------------|
| `TenantId` | int | Tenant al que pertenece (multi-tenancy) |
| `EffectiveStartDate` | datetime | Inicio de vigencia de la configuración |
| `EffectiveEndDate` | datetime? | Fin de vigencia (null = indefinido) |
| `CssEmployeeRate` | decimal | Tasa CSS empleado (9.75) |
| `CssEmployerBaseRate` | decimal | Tasa CSS patronal (13.25 / 14.25 / 15.25) |
| `CssRiskRateLow` | decimal | Riesgo Clase I (0.56) |
| `CssRiskRateMedium` | decimal | Riesgo Clase III (2.10) |
| `CssRiskRateHigh` | decimal | Riesgo Clase V (5.67) |
| `CssMaxContributionBaseStandard` | decimal | Tope pensión estándar (1500) |
| `CssMaxContributionBaseIntermediate` | decimal | Tope pensión intermedio (2000) |
| `CssMaxContributionBaseHigh` | decimal | Tope pensión alto (2500) |
| `CssIntermediateMinYears` | int | Años mínimos tope intermedio (25) |
| `CssHighMinYears` | int | Años mínimos tope alto (30) |
| `EducationalInsuranceEmployeeRate` | decimal | SE empleado (1.25) |
| `EducationalInsuranceEmployerRate` | decimal | SE patronal (1.50) |
| `DependentDeductionAmount` | decimal | Deducción ISR por dependiente (800) |
| `MaxDependents` | int | Máximo de dependientes aceptados (99) |
| `SalarioMinimoLegal` | decimal | Salario mínimo mensual legal (700.00 por defecto) |
| `IsActive` | bool | Si la configuración está activa |

### 10.2 Tabla TaxBrackets

Almacena los tramos del ISR por año fiscal y tenant.

| Campo | Tipo | Descripción |
|-------|------|-------------|
| `TenantId` | int | Tenant al que pertenece |
| `Year` | int | Año fiscal (ej: 2026) |
| `Order` | int | Orden del tramo (1, 2, 3) |
| `MinIncome` | decimal | Ingreso mínimo del tramo |
| `MaxIncome` | decimal? | Ingreso máximo (null = sin límite superior) |
| `Rate` | decimal | Tasa del tramo en porcentaje (0, 15, 25) |
| `FixedAmount` | decimal | Monto fijo acumulado de tramos anteriores (0, 0, 5850) |

### 10.3 Campo CssRiskPercentage en Empleado

```csharp
[Column(TypeName = "decimal(5, 2)")]
public decimal CssRiskPercentage { get; set; } = 0.56m;
```

Almacena directamente el porcentaje de la clase de riesgo del empleado. Valores válidos: 0.56, 0.98, 2.10, 3.64, 5.67.

---

## 11. Implementación en Código

### 11.1 Archivos Clave

| Archivo | Responsabilidad |
|---------|----------------|
| `src/Core/Planilla.Application/Services/CssCalculationServicePortable.cs` | Cálculo CSS (empleado, patronal, riesgo) |
| `src/Core/Planilla.Application/Services/EducationalInsuranceServicePortable.cs` | Cálculo Seguro Educativo |
| `src/Core/Planilla.Application/Services/IncomeTaxCalculationServicePortable.cs` | Cálculo ISR con brackets progresivos |
| `src/Core/Planilla.Application/Services/PayrollCalculationOrchestratorPortable.cs` | Orquestador (CSS + SE + ISR) |
| `src/Core/Planilla.Application/Services/DeduccionPrioridadEngine.cs` | Motor de prelación de deducciones adicionales |
| `src/Infrastructure/Planilla.Infrastructure/Services/PayrollProcessingService.cs` | Servicio de procesamiento completo de planilla |
| `src/Infrastructure/Planilla.Infrastructure/Data/PayrollConfigSeeder.cs` | Seeding de configuraciones (3 fases + ISR brackets) |
| `src/Core/Planilla.Domain/Entities/Empleado.cs` | Entidad empleado con campos de cálculo |
| `src/UI/Planilla.Web/ClientApp/src/constants/payroll.ts` | Constantes del frontend (frecuencias, riesgo) |

### 11.2 Redondeo

Todos los cálculos monetarios usan `RoundingPolicy.CalculatePercentage()` y `RoundingPolicy.Round()`, que aplican redondeo a 2 decimales con lógica `MidpointRounding.AwayFromZero`.

### 11.3 Manejo de Errores de Configuración

Los servicios no implementan ningún fallback silencioso. Si no existe configuración vigente para la fecha de cálculo, se lanza una excepción explícita:

```csharp
throw new InvalidOperationException(
    $"No se encontró configuración de CSS activa para companyId={companyId} " +
    $"en fecha {calculationDate:yyyy-MM-dd}. " +
    "Verifique que exista una configuración vigente en PayrollTaxConfigurations.");
```

Para el ISR, si no existen tramos configurados para el año fiscal, se lanza `PayrollConfigurationException`.

### 11.4 Tasa Horaria y Horas Extra

La tasa horaria del empleado se calcula con base mensual, independientemente de la frecuencia de pago:

```csharp
// Semanas por mes: 52 / 12 ≈ 4.3333
public const decimal WeeksPerMonth = 52m / 12m;

// TasaHora = SalarioMensual / (HorasSemanales × 4.3333)
HourlyRate = SalarioBase / ((decimal)HoursPerWeek * WeeksPerMonth);
```

Los tipos de horas extra y sus factores se gestionan en `OvertimeFactorService` (ver documentación de horas extra).

---

## 12. Notas Legales y Referencias

### 12.1 Distinción Cotización vs. Pensión CSS (DEV-19)

Esta es la diferencia más importante del módulo de CSS post-reforma:

- **Cotización CSS**: Se calcula sobre el **100% del salario bruto**, sin ningún tope. Así lo establece el Art. 178 de la Ley 462 de 2024.
- **Tope de Pensión**: B/.1,500, B/.2,000 o B/.2,500 son los **techos de la pensión mensual** que el asegurado recibirá al jubilarse, condicionados a sus años cotizados y salario promedio.

Versiones anteriores del sistema aplicaban `Math.Min(grossPay, periodCap)` antes de calcular la cotización, lo que reducía incorrectamente la base de cálculo. Esto fue corregido en DEV-19.

### 12.2 Dependientes Sin Límite Legal (DEV-20)

La Ley 37 de 2018 (reforma ISR) y el Decreto Ejecutivo 368 de 2018 no establecen un límite en el número de dependientes que puede declarar un contribuyente. El campo `MaxDependents = 99` en la configuración es una validación operacional del sistema, no un requisito legal.

### 12.3 Tasas de Riesgo Profesional — Acuerdo N°2 de 1995 (DEV-20/DEV-21)

Las tasas corregidas son las oficiales del Acuerdo N°2 de 1995 de la Junta Directiva de la CSS:

| Clase anterior (incorrecta) | Clase actual (correcta) |
|-----------------------------|------------------------|
| 0.56% | 0.56% (Clase I — sin cambio) |
| 2.50% (incorrecto) | 2.10% (Clase III — corregido) |
| 5.39% (incorrecto) | 5.67% (Clase V — corregido) |

El sistema ahora expone las 5 clases completas en lugar de solo 3 (DEV-21).

### 12.4 Jubilación — Requisitos Mínimos

- Mínimo 20 años cotizados (240 cuotas mensuales) para tener derecho a jubilación.
- Con 25+ años cotizados y promedio salarial de B/.2,000 o más: tope de pensión B/.2,000.
- Con 30+ años cotizados y promedio salarial de B/.2,500 o más: tope de pensión B/.2,500.

### 12.5 Referencias Legales

| Norma | Descripción |
|-------|-------------|
| Ley 51 de 2005 (CSS) | Ley orgánica de la Caja de Seguro Social |
| Ley 462 de 2024 | Reforma CSS — cuotas escalonadas, Art. 178 (topes pensión) |
| Acuerdo N°2 de 1995 | Reglamento de Riesgo Profesional — 5 clases y tasas |
| Decreto de Gabinete N°144 de 1971 | Seguro Educativo — tasas empleado (1.25%) y patrono (1.50%) |
| Ley 37 de 2018 | Reforma ISR — tramos 0% / 15% / 25%, deducción por dependiente B/.800 |
| Decreto Ejecutivo 368 de 2018 | Reglamentación de Ley 37/2018 — dependientes sin límite legal |
| Código de Trabajo de Panamá | Art. 31 (jornada máxima 48 h/semana), prelación de deducciones |

---

_Documento generado con base en la implementación en producción. Para consultas técnicas, revisar los archivos fuente referenciados en la sección 11. Para consultas legales, consultar directamente el texto de las normas vigentes de la República de Panamá._
