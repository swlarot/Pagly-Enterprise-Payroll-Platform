# Auditoría de Nómina — Pagly contra Talento (fuente de verdad)

> **Para:** el agente que va a arreglar el sistema de nómina de Pagly (`C:\Planilla`).
> **Objetivo:** contrastar cada cálculo de planilla de Pagly contra el motor de **Talento**
> (`C:\Grupo Urbis\rrhh-urbis-api`), detectar qué sacar / arreglar / completar, y dejar
> cada cálculo respaldado por su artículo de ley.
> **Generado:** 2026-06-18.

---

## 0. Por qué Talento es la fuente de verdad (y la dirección del contraste)

El motor de nómina de **Talento es un PORT directo del motor .NET de Pagly** (decisión 2026-05-09).
El archivo `payroll-orchestrator.ts:19` de Talento lo dice literal:
*"Port de PayrollCalculationOrchestratorPortable (Pagly)"*. Por eso el mapeo es casi 1:1.

La diferencia: **después del port, Talento fue auditado contra las fuentes normativas primarias
(3 agentes en paralelo, auditoría 2026-05-15) y recibió correcciones que Pagly probablemente
todavía NO tiene.** El caso más importante:

- **Bug del Seguro Educativo deducible del ISR** — corregido en Talento el 2026-05-26 con cita
  literal del **Art. 709 numeral 4 del Código Fiscal**. El SE del empleado (1.25%) ES deducible
  de la base imponible del ISR y el motor original (Pagly) **no lo descontaba**. Casi seguro Pagly
  todavía calcula el ISR sin restar el SE → **ISR sobreestimado**. Este es el primer punto a verificar.

**Por lo tanto el contraste va Pagly → Talento:** Talento es el "patrón oro" ya validado; se revisa
qué de Pagly difiere y se arregla Pagly para que coincida (salvo donde Pagly tenga una mejora que
Talento no tenga — ver §7, divergencias arquitectónicas, que pueden ir en cualquier dirección).

---

## 1. Recurso legal #1: la skill `panama-payroll`

Antes de tocar cualquier cálculo, el agente debe invocar la skill:

```
C:\Users\gjose\.claude\skills\panama-payroll\
```

Es la **fuente de verdad legal consolidada**, con citas textuales validadas contra los PDFs oficiales.
Contiene:

| Tema | Archivo en la skill |
|------|---------------------|
| Resumen + tasas vigentes 2026 + tramos ISR + 12 tipos de horas extra | `SKILL.md` (índice) |
| Código de Trabajo (jornada, HE, vacaciones, liquidaciones) | `leyes/codigo-trabajo-resumen.md` |
| CSS (Ley 51/2005) | `leyes/ley-51-2005-css.md` |
| Reforma CSS (Ley 462/2025) | `leyes/ley-462-2025.md` |
| ISR (Código Fiscal Art. 700) | `leyes/codigo-fiscal-art-700-isr.md` |
| Planilla 03 mensual (DGI 201-4853) | `leyes/resolucion-201-4853-planilla03.md` |
| 12 tipos de horas extra | `calculadores/horas-extra-12-tipos.md` |
| Liquidaciones | `calculadores/liquidaciones.md` |
| Vacaciones (Art. 54) | `calculadores/vacaciones-art-54.md` |
| Topes CSS Ley 462 | `calculadores/css-topes-ley-462.md` |
| Tramos ISR | `calculadores/isr-bracket.md` |
| Mapeo código → ley | `mapping/codigo-a-ley.md` |
| **Errores comunes a evitar** | sección final de `SKILL.md` |

**Regla de la skill:** todo cálculo debe llevar (a) cita textual del artículo en el comentario del
código, (b) test unitario con al menos un caso del artículo, (c) referencia en `mapping/codigo-a-ley.md`.

---

## 2. PDFs de ley primaria (para verificar a la fuente)

Talento tiene los PDFs oficiales completos. Cuando la skill o el código no basten, ir al texto literal:

| Documento | Ruta (repo Talento) | Cubre |
|-----------|---------------------|-------|
| Código de Trabajo | `C:\Grupo Urbis\rrhh-urbis-api\docs\articles\código-detrabajo.pdf` | Arts. 30, 33, 36, 48-50 (HE), 54 (vacaciones), 140-142, 149, 161-162 (salario), 210-226 (liquidación), 291-330 (riesgo prof.) |
| Código Fiscal | `...\docs\articles\codigo-fiscal.pdf` | Arts. 700 (tramos ISR), 704 (retención planilla), 709 (deducciones: dependientes num. 3, **SE num. 4**) |
| Texto Único Ley 51/2005 (modif. Ley 462) | `...\docs\articles\TEXTO-UNICO-DE-LA-LEY-51-DE-2005-CSS-GACETA-OFICIAL-22-5-25.pdf` | Art. 96.1-96.5 (CSS IVM, SE, topes, décimo CSS reducida, tasas patronales 2026/2027/2029) |
| Ley 462 de 2025 | `...\docs\articles\ley 462.pdf` | Reforma CSS: tasas escalonadas, topes de cotización |
| Decreto Ejecutivo 170/1993 | `...\docs\articles\Decreto-Ejecutivo-170-de-1993 (1).pdf` | Reglamento ISR (proyección a 13 meses) |
| Resolución DGI 201-4853 | `...\docs\articles\RES_201-4853.pdf` | Planilla 03 mensual (e-Tax 2.0) |
| Ley 49 de 2009 | `...\docs\articles\Ley-49 de 17 de septiembre de 2009...pdf` | (disolución de oficio; contexto fiscal) |
| Ley 53 de 1975 | `...\docs\articles\ley_53_de_1975_reclamaciones_laborales.pdf` | Reclamaciones laborales (jurisdicción) — **NO regula décimo** (error común) |

Pagly también tiene documentación propia de compliance que conviene cruzar:
`C:\Planilla\docs\payroll\` y `C:\Planilla\docs\compliance\`.

---

## 3. Mapa de equivalencias — Pagly (.NET) ↔ Talento (TS)

Esta es la tabla maestra. Para cada concepto, abrir **ambos** archivos en paralelo y comparar
fórmula, constantes y orden de operaciones.

| Concepto | Pagly (`C:\Planilla\src\...`) | Talento (`C:\Grupo Urbis\rrhh-urbis-api\src\domain\payroll\...`) |
|----------|-------------------------------|------------------------------------------------------------------|
| **Orquestador** | `Core\Planilla.Application\Services\PayrollCalculationOrchestratorPortable.cs` | `payroll-orchestrator.ts` |
| **CSS** | `Core\Planilla.Application\Services\CssCalculationServicePortable.cs` | `css-calculator.ts` |
| **ISR** | `Core\Planilla.Application\Services\IncomeTaxCalculationServicePortable.cs` | `isr-calculator.ts` |
| **Seguro Educativo** | `Core\Planilla.Application\Services\EducationalInsuranceServicePortable.cs` | `educational-insurance-calculator.ts` |
| **Liquidación** | `Core\Planilla.Application\Services\LiquidacionCalculationService.cs` (monolítico) | `liquidacion-calculator.ts` **+** `cesantia-calculator.ts` **+** `preaviso-calculator.ts` **+** `vacation-proportional-calculator.ts` **+** `decimo-proportional-calculator.ts` (descompuesto) |
| **Décimo** | `Infrastructure\Planilla.Infrastructure\Services\DecimoCalculationService.cs` | `decimo-proportional-calculator.ts` (+ módulo `src\modules\thirteenth-month`) |
| **Horas extra (factores)** | `Infrastructure\Planilla.Infrastructure\Services\OvertimeFactorService.cs` (+ `IOvertimeFactorService`) | `overtime-calculator.ts` (+ `overtime-type-resolver.ts`, `overtime-hours-calculator.ts`) |
| **Redondeo** | `Core\Planilla.Application\Helpers\RoundingPolicy.cs` | `rounding-policy.ts` |
| **Constantes legales** | `Core\Planilla.Application\Helpers\PayrollConstants.cs` | `payroll.constants.ts` |
| **Config tributaria (provider)** | `Core\Planilla.Application\Services\StaticPayrollConfigProvider.cs` (+ `IPayrollConfigProvider`) | `src\modules\payroll-tax-config\payroll-tax-config.service.ts` (`getActiveConfig`) |
| **Config tributaria (entidad)** | `Core\Planilla.Domain\Entities\PayrollTaxConfiguration.cs` | modelo `PayrollTaxConfiguration` en `prisma\schema.prisma` |
| **TaxBracket** | `Core\Planilla.Domain\Entities\TaxBracket.cs` | modelo `TaxBracket` en `prisma\schema.prisma` |
| **Motor de deducciones** | `Core\Planilla.Application\Services\DeduccionPrioridadEngine.cs` | (no existe equivalente directo — Talento maneja anticipos/préstamos como servicios separados, ver §7) |

**Results de Pagly** (`Core\Planilla.Application\Results\`): `CssCalculationResult`, `CssFullCalculationResult`,
`EducationalInsuranceResult`, `IncomeTaxResult`, `PayrollCalculationResult`, `DeduccionesResult` →
en Talento son las interfaces `*Result` exportadas por cada calculador (`CssDetailedResult`, `SeResult`,
`IsrResult`, `OrchestratorResult`, etc.).

---

## 4. Orden de lectura recomendado en Talento (el "patrón oro")

Leer en este orden para entender el motor correcto antes de tocar Pagly:

1. **`docs\modules\payroll.md`** (`C:\Grupo Urbis\rrhh-urbis-api\docs\modules\payroll.md`) — 294 líneas.
   Es el resumen del módulo completo. **La sección 6 "Decisiones interpretativas (6.1-6.6)" es lo más
   importante de todo este informe** (ver §5 abajo).
2. **`src\domain\payroll\payroll-orchestrator.ts`** — el pipeline maestro (CSS + SE + ISR + totales).
3. **`src\domain\payroll\css-calculator.ts`** — topes escalonados Ley 462.
4. **`src\domain\payroll\isr-calculator.ts`** — proyección 13 meses + deducción SE (lo corregido).
5. **`src\domain\payroll\educational-insurance-calculator.ts`** — SE sin tope.
6. **`src\domain\payroll\overtime-calculator.ts`** — 12 tipos de HE con factores.
7. **`src\domain\payroll\liquidacion-calculator.ts`** + las 4 sub-calculadoras (cesantia, preaviso,
   vacation-proportional, decimo-proportional).
8. **`src\domain\payroll\payroll.constants.ts`** — tasas legacy (referencia).
9. **Los tests `*.spec.ts`** de cada calculador (mismo directorio) — **son la mejor fuente de números
   validados con casos reales** (ver §6, casos concretos).
10. **`prisma\seed.ts`** (líneas ~165-198) — las tasas/topes 2026 seedeados, y los 3 tramos ISR.

---

## 5. Decisiones interpretativas validadas — el corazón del contraste

Estas 6+ decisiones están documentadas en `docs\modules\payroll.md §6` y en comentarios de código de
Talento con cita de ley. **Por cada una: verificar qué hace Pagly y alinearlo (o documentar por qué
difiere).**

| # | Decisión (cómo debe ser) | Ley / artículo | Dónde está en Talento | Qué revisar en Pagly |
|---|--------------------------|----------------|------------------------|----------------------|
| 1 | **ISR se calcula sobre el bruto, SIN restar la CSS.** La CSS del empleado NO está listada en el Art. 709, así que no es deducible de la base del ISR. | Código Fiscal **Art. 700 + 704 + 709** | `isr-calculator.ts:111-115` | Confirmar que `IncomeTaxCalculationServicePortable.cs` tampoco resta la CSS. (Probablemente ya lo hace bien.) |
| 2 | **El Seguro Educativo (1.25% empleado) SÍ se descuenta de la base imponible del ISR.** Solo si `isSubjectToCss = true`. | Código Fiscal **Art. 709 numeral 4** | `isr-calculator.ts:141-146` | **⚠️ PUNTO CRÍTICO.** Verificar si Pagly resta el SE antes de aplicar tramos. Si no → ISR sobreestimado. Este fue el bug que Talento corrigió y Pagly probablemente conserva. |
| 3 | **ISR proyectado a 13 meses:** `anual = bruto × periodos_por_año / 12 × 13` (distribuye el décimo uniformemente). | Decreto Ejecutivo 170/1993 (reglamento ISR) | `isr-calculator.ts:137-139` | Verificar el factor de proyección en Pagly. |
| 4 | **Décimo usa CSS REDUCIDA: 7.25% empleado / 10.75% patronal** (NO 9.75%/13.25%). El SE NO se reduce (sigue 1.25%/1.50%). | Ley 51/2005 **Art. 96.4-96.5** | `payroll.constants.ts:97` (`DECIMO_CSS_EMPLOYEE_RATE = 0.0725`) + `decimo-proportional-calculator.ts` | Verificar `DecimoCalculationService.cs` y `PayrollConstants.cs`. Talento tuvo un bug donde el cálculo era correcto pero la UI mostraba 9.75% — revisar ambos lados en Pagly. |
| 5 | **Topes CSS escalonados Ley 462:** STANDARD $1,500 / INTERMEDIATE $2,000 (≥25 años cotizados + promedio ≥$2,000) / HIGH $2,500 (≥30 años + promedio ≥$2,500). | Ley 51/2005 Art. 96 + Ley 462 | `css-calculator.ts:105-129` (`determineCssCap`) | Verificar que `CssCalculationServicePortable.cs` implemente los 3 topes con esos umbrales exactos. |
| 6 | **Prima de antigüedad Art. 224: 1 semana/año SIN escalones.** Base salarial = promedio de los últimos **5 años** (Art. 226). | Código de Trabajo **Art. 224 + 226** | `liquidacion-calculator.ts:211-214` | **⚠️** La skill advierte: Pagly tenía un escalón erróneo de "25 años × 1.5 semanas" que NO existe en la ley. Verificar que se haya eliminado en `LiquidacionCalculationService.cs`. |
| 7 | **Indemnización despido injustificado Art. 225:** `semanas = min(años,10)×3.4 + max(0,años-10)×1`. Base salarial = promedio de **6 meses o 30 días, lo más favorable** (Art. 149) — **NO** el promedio de 5 años. | Código de Trabajo **Art. 225 + 149** | `liquidacion-calculator.ts:232-235` | Verificar que Pagly use la base 6m/30d para indemnización (distinta de la base 5 años de la prima). Error común: usar 5 años para ambas. |
| 8 | **Riesgo profesional: porcentaje manual por EMPRESA**, no 3 niveles fijos por empleado. Lo asigna la CSS (Carnet Patronal / SIPE). | Código de Trabajo Arts. 300, 304 + Reglamento CSS | `Tenant.cssRiskPercentage` (manual, default 0.0056) | Verificar si Pagly tiene riesgo por empleado o un enum BAJO/MEDIO/ALTO. Si sí → migrar a porcentaje manual por tenant/empresa. |
| 9 | **Preaviso absorbido en la indemnización Art. 225** cuando aplica (despido injustificado, causa económica) — no se compensa por separado. | Código de Trabajo Arts. 211, 225 | `preaviso-calculator.ts:80-87` | Verificar que Pagly no sume preaviso DOBLE cuando ya paga indemnización. |
| 10 | **Cesantía 6% × meses** (Decreto Ej. 60/1995) sustituye la prima Art. 224 SOLO en contratos DEFINIDO / POR_OBRA. | Decreto Ejecutivo 60/1995 + Art. 229 | `cesantia-calculator.ts:37-52` | Verificar el manejo de contratos definidos vs indefinidos en la liquidación de Pagly. |

---

## 6. Casos numéricos validados (tests de Talento como oráculo)

Los `*.spec.ts` de Talento tienen casos con números reales. Replicarlos como tests de regresión en
Pagly (`C:\Planilla\tests\`) — si Pagly da otro número, ahí está el bug.

**`isr-calculator.spec.ts`:**
- Bruto quincenal **$350** → anual $9,100 → **exento** (< $11k).
- Bruto **$1,500** quincenal → anual $39,000 → base imponible **$38,512.50** (tras restar SE $487.50)
  → ISR anual **$4,126.88** → **$172.79 por quincena**. ← *Si Pagly no resta el SE, dará ~$176-177; ahí se ve el bug #2.*
- Bruto $1,500 + 2 dependientes → base $36,912.50 → anual $3,886.88.
- Bruto **$2,500** quincenal → anual $65,000 → tramo 25% → ISR anual $9,396.875.

**`css-calculator.spec.ts`:**
- Salario quincenal **$750** → CSS empleado **$73.13** (9.75%) / patronal **$99.38** (13.25%) / riesgo **$4.20** (0.56%).
- Bruto $2,000 → cap STANDARD **$1,500**.
- Senior 26 años + promedio $2,100 → cap INTERMEDIATE **$2,000**.

**`liquidacion-calculator.spec.ts`:**
- Renuncia simple, 5 años → prima **$1,153.85** (5 × $230.77/semana), **sin** indemnización.
- Despido injustificado, 5 años → prima $1,153.85 + indemnización **17 sem × $230.77 = $3,922.09**.
- 15 años causa económica → prima 15 sem + indemnización **39 sem** (10×3.4 + 5×1).

**`decimo` / `cesantia` / `vacation-proportional`:** ver specs respectivos para casos MITRADEL (A, B, C, D, 10).

> **Nota de paridad numérica:** Talento replica `MidpointRounding.AwayFromZero` de .NET en
> `rounding-policy.ts` justo para coincidir con Pagly. Si aparecen diferencias de centavos, el redondeo
> NO debería ser la causa (ambos usan away-from-zero) — buscar el bug en la fórmula, no en el redondeo.

---

## 7. Divergencias arquitectónicas detectadas (decidir dirección caso por caso)

Estas no son necesariamente bugs — son diferencias de diseño. El agente debe decidir si Pagly adopta
el patrón de Talento o si Pagly ya tiene algo mejor:

1. **Liquidación monolítica vs descompuesta.** Pagly tiene un solo `LiquidacionCalculationService.cs`;
   Talento la partió en 5 calculadoras puras (`liquidacion`, `cesantia`, `preaviso`,
   `vacation-proportional`, `decimo-proportional`). **Riesgo en Pagly:** que casos como cesantía
   (contratos definidos), preaviso absorbido, y vacaciones proporcionales del ciclo de 11 meses estén
   mezclados o incompletos. Contrastar uno por uno contra las sub-calculadoras de Talento.

2. **Config estática vs effective-dating en DB.** Pagly usa `StaticPayrollConfigProvider.cs` (tasas
   hardcodeadas); Talento usa `PayrollTaxConfigService.getActiveConfig(tenantId, date)` que resuelve la
   `PayrollTaxConfiguration` vigente por fecha, con fallback a config global. **Esto importa para las
   transiciones Ley 462:** CSS patronal sube a **14.25% en mar-2027** y **15.25% en mar-2029**
   (Art. 96.2.b/c). Con config estática, Pagly necesitará un cambio de código en cada transición; con
   effective-dating, basta una fila nueva en DB. Evaluar migrar Pagly al patrón de Talento.

3. **`DeduccionPrioridadEngine.cs` (solo Pagly).** Pagly tiene un motor de priorización de deducciones
   (`DeduccionAplicada`, `DeduccionFija`, `BaseCalculoDeduccion`, `TipoDeduccion`, `CategoriaDeduccion`,
   `Acreedor`, embargos/órdenes judiciales con `EstadoOrdenJudicial`). Talento **no** tiene esto — maneja
   anticipos y préstamos como servicios separados (`SalaryAdvancesDeductionService`, `LoansDeductionService`).
   Aquí **Pagly puede ser más completo**; no borrar sin entender. Verificar que el orden de prelación de
   deducciones (CSS/SE/ISR primero, luego embargos, luego anticipos/préstamos) respete el Código de Trabajo
   Arts. 161-162 (inembargabilidad parcial del salario).

4. **Décimo en Infrastructure vs Domain.** Pagly tiene `DecimoCalculationService.cs` en *Infrastructure*
   (acoplado a datos); Talento lo tiene como función pura en *domain* + módulo. Si Pagly quiere testear el
   décimo de forma aislada, conviene extraer la fórmula a una clase pura en `Planilla.Application`.

---

## 8. Errores comunes a cazar (de la skill `panama-payroll`)

Pasar este checklist sobre el código de Pagly — son los errores que más se cometen en nómina panameña:

- [ ] **Período diurno:** debe ser **6:00am–6:00pm** (Art. 30), NO 6am–10pm. Verificar `OvertimeFactorService.cs`.
- [ ] **Jornada mixta con >3h nocturnas → se considera NOCTURNA** (1.50x), Art. 30 párrafo 2.
- [ ] **Prima de antigüedad:** 1 semana/año plano, **sin** escalón de 25 años (Art. 224).
- [ ] **Indemnización:** base 6m/30d más favorable (Art. 149), **no** promedio de 5 años.
- [ ] **Riesgo profesional:** por empresa (Arts. 300, 304), no por empleado.
- [ ] **Vacaciones:** proporcional diario 1 día / 11 días (Art. 54.1), no en bloques de 11 meses.
      Fórmula: `diasVacacion = floor(diasServicio / 11)`. 30 días por cada 11 meses continuos.
- [ ] **Décimo:** CSS reducida **7.25%/10.75%** (Art. 96.4-96.5), no la normal 9.75%/13.25%.
- [ ] **Pago de vacaciones (Art. 54.2):** promedio de los últimos 11 meses (sueldo + bonos + HE, Art. 142)
      si es más favorable que el último salario.
- [ ] **Ley 53/1975 NO regula el décimo** (regula reclamaciones laborales / jurisdicción).
- [ ] **Planilla 03 es MENSUAL** desde sept-2022 (Resolución 201-4853), no anual.
- [ ] **12 tipos de horas extra** con factores 1.25x → 3.75x. Cotejar el enum `TipoHoraExtra.cs` de Pagly
      contra la tabla de la skill (`calculadores/horas-extra-12-tipos.md`) — especialmente las combinaciones
      dominical/feriado × diurna/nocturna (1.875x, 2.25x, 3.125x, 3.75x).

---

## 9. Plan de trabajo sugerido para el agente de Pagly

1. **Invocar la skill `panama-payroll`** y leer `docs\modules\payroll.md §6` de Talento (las decisiones).
2. **Calculador por calculador** (CSS → SE → ISR → décimo → HE → liquidación), abrir el par
   Pagly/Talento de la tabla §3 y comparar fórmula + constantes + orden.
3. **Empezar por el ISR (bug #2 del SE):** es el hallazgo más probable y de mayor impacto.
4. **Portar los casos numéricos de §6 como tests** en `C:\Planilla\tests\` antes de arreglar
   (TDD: test rojo que reproduce la discrepancia → fix → verde).
5. **Para cada fix:** comentar el artículo de ley en el código + test + actualizar
   `docs\compliance\` o `docs\payroll\` de Pagly.
6. **Las divergencias de §7 NO se borran a ciegas** — Pagly puede ser más completo (deducciones,
   embargos). Decidir dirección caso por caso y dejar registro.
7. **Verificar paridad:** un mismo empleado/período debe dar el mismo neto en Pagly y en Talento
   (salvo donde una de las dos esté declaradamente mal). El redondeo es away-from-zero en ambos.

---

## 10. Resumen de rutas (copy-paste)

**Talento (fuente de verdad):**
- Doc del módulo: `C:\Grupo Urbis\rrhh-urbis-api\docs\modules\payroll.md`
- Calculadoras: `C:\Grupo Urbis\rrhh-urbis-api\src\domain\payroll\`
- Tests (oráculo): `C:\Grupo Urbis\rrhh-urbis-api\src\domain\payroll\*.spec.ts`
- Seed de tasas: `C:\Grupo Urbis\rrhh-urbis-api\prisma\seed.ts`
- Schema: `C:\Grupo Urbis\rrhh-urbis-api\prisma\schema.prisma`
- PDFs de ley: `C:\Grupo Urbis\rrhh-urbis-api\docs\articles\`

**Pagly (a arreglar):**
- Calculadores: `C:\Planilla\src\Core\Planilla.Application\Services\*Portable.cs` + `LiquidacionCalculationService.cs`
- Décimo / HE: `C:\Planilla\src\Infrastructure\Planilla.Infrastructure\Services\{DecimoCalculationService,OvertimeFactorService}.cs`
- Constantes / redondeo: `C:\Planilla\src\Core\Planilla.Application\Helpers\{PayrollConstants,RoundingPolicy}.cs`
- Config tributaria: `C:\Planilla\src\Core\Planilla.Domain\Entities\PayrollTaxConfiguration.cs` + `StaticPayrollConfigProvider.cs`
- Tests: `C:\Planilla\tests\`
- Docs propias: `C:\Planilla\docs\payroll\`, `C:\Planilla\docs\compliance\`

**Skill legal:** `C:\Users\gjose\.claude\skills\panama-payroll\`
