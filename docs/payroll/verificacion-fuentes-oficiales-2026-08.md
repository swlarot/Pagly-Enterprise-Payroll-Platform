# Verificación contra fuentes oficiales — agosto 2026

Auditoría del motor de nómina contra el texto legal primario, disparada por una
planilla real de cliente (quincena 18-01-26, 7 empleados).

**Fuentes consultadas** (texto primario, no interpretaciones de terceros):

| Fuente | Documento | Uso |
|---|---|---|
| Gaceta Oficial 30284-B, 22-may-2025 | Texto Único de la Ley 51 de 2005 con reformas de la Ley 462 de 2025 | CSS, Seguro Educativo |
| Decreto de Gabinete 252/1971, consolidado hasta Ley 44/1995 | Código de Trabajo | Horas extra, domingos, feriados |
| Código Fiscal, Art. 700 | Tarifa ISR persona natural | Tramos de ISR |
| Decreto Ejecutivo N.° 13 (G.O. 06-ene-2026, vigente 16-ene-2026) | Salario mínimo 2026-2027 | Salario mínimo |

---

## 1. Validación contra la planilla real del cliente

Quincena 18-01-26. Salario mensual B/.713.44, jornada de 48 h/semana.

| Concepto | Planilla del cliente | Sistema | Estado |
|---|---|---|---|
| Tarifa horaria | 3.43 | `713.44 / (48 × 52/12)` = 3.43 | ✅ |
| Tarifa dominical | **5.145** | `3.43 × 1.50` = 5.145 | ✅ |
| Domingo de 8 h | **41.16** | `5.145 × 8` = 41.16 | ✅ |
| CSS empleado (sobre 397.88) | 38.79 | 9.75% → 38.79 | ✅ |
| Seguro Educativo | 4.97 | 1.25% → 4.97 | ✅ |
| Neto | 354.12 | 397.88 − 43.76 | ✅ |

Fijado en `OvertimeFactorConfigServiceTests.PlanillaReal__DomingoDe8Horas__Paga41Con16`.

**Observación sobre la planilla, no sobre el sistema:** al empleado de la fila 25
(ARISTÓBULO RIVERA) se le registran 397.88 de asignación y 397.88 de neto, sin CSS
ni Seguro Educativo. Faltarían 43.76 de deducciones. Se entiende que responde a la
nota "el cálculo es por separado", pero si se paga así hay subdeclaración ante la CSS.

---

## 2. CORREGIDO — El tope de cotización de CSS no existe

### Lo que hacía el código

`CssCalculationServicePortable` aplicaba, para empleado y empleador:

```csharp
var contributionBase = Math.Min(grossPay, periodCap);  // tope 1,500 / 2,000 / 2,500
```

### Lo que dice la ley

Ley 51 de 2005, **Art. 96**, numerales 1 y 2 (Texto Único con reformas de la Ley 462
de 2025), textualmente:

> "1. La cuota pagada por los empleados, la cual será el equivalente a 9.75 % de sus
> sueldos.
> 2. La cuota pagada por los empleadores, la cual será:
> a. A partir de la entrada en vigencia de la presente Ley y hasta el 28 de febrero
> de 2027, el equivalente a 13.25 % de los sueldos que paguen a sus empleados."

**No existe tope, techo ni base máxima de cotización en el Art. 96 ni en ningún otro
artículo de la ley.** La cuota se calcula sobre "sus sueldos", sin límite.

### De dónde venía la confusión

Los montos 1,500 / 2,000 / 2,500 sí existen en la ley, pero en el **Art. 193**, cuyo
título literal es:

> "Artículo 193. **Monto máximo de las pensiones** de invalidez y vejez. El monto
> máximo de las pensiones de invalidez y de vejez […] será hasta mil quinientos
> balboas (B/.1 500.00) mensuales, salvo que: 1. El asegurado tenga por lo menos
> veinticinco años de cotizaciones […] y un salario promedio mensual no menor de dos
> mil balboas (B/.2 000.00) […] en cuyo caso **la pensión** podrá ser de un monto de
> hasta dos mil balboas (B/.2 000.00) mensuales."

Las condiciones "25 años + B/.2,000" y "30 años + B/.2,500" del Art. 193 son
exactamente las que el código usaba como `CssIntermediateMinYears` /
`CssHighMinAvgSalary`. Es decir: **se implementó un tope de PENSIÓN como si fuera un
tope de COTIZACIÓN.**

El Art. 178, que el comentario del código citaba como fundamento, trata de las
**edades de acceso a la pensión de vejez** y no menciona topes de ningún tipo.

### Impacto

Todo empleado con sueldo superior a B/.1,500 mensuales cotizaba de menos:

| Sueldo mensual | CSS empleado (antes) | CSS empleado (correcto) | Diferencia |
|---|---|---|---|
| 2,000 | 146.25 | 195.00 | **−48.75/mes** |
| 3,000 | 146.25 | 292.50 | **−146.25/mes** |

Lo mismo del lado patronal (13.25%). Era subdeclaración ante la CSS.

### Corrección aplicada

```csharp
var contributionBase = grossPay;
```

`periodCap` y `TipoTope` se conservan como dato informativo del resultado. Tests
`CssCalculationServiceTests` reescritos para afirmar la regla legal correcta.

**Nota:** la documentación interna (`payroll-calculations.md` §2.2) ya describía el
comportamiento correcto desde antes; era el código el que estaba desalineado.

---

## 3. Verificado sin cambios — Horas extra

Texto literal del Código de Trabajo, confirmado artículo por artículo:

| Art. | Texto | Factor | En código |
|---|---|---|---|
| 33.1 | "25 por ciento de recargo […] período diurno" | 1.25 | `Diurna` ✅ |
| 33.2 | "50 por ciento […] período nocturno o […] jornada mixta iniciada en el período diurno" | 1.50 | `Nocturna`, `MixtaDiurnaNocturna` ✅ |
| 33.3 | "75 por ciento […] prolongación de la nocturna o de la jornada mixta iniciada en período nocturno" | 1.75 | `MixtaNocturnaDiurna` ✅ |
| 36.4 | "No se pueden trabajar más de tres horas extraordinarias en un día, ni más de nueve en una semana […] el excedente será remunerado con un 75 por ciento de recargo adicional" | ×1.75 | `FactorExcesoLegal` ✅ |
| 48 | "El trabajo en día domingo o en cualquier otro día de descanso semanal obligatorio se remunerará con un recargo del 50 por ciento" | 1.50 | `DomingoFeriado` ✅ |
| 48 | "El trabajo en el día que deba darse como compensación […] 50 por ciento de recargo" | 1.50 | `DiaSustituto` ✅ |
| 49 | "El trabajo en día de fiesta o duelo nacional se pagará con un recargo del 150 por ciento" | 2.50 | `FeriadoOrdinario` ✅ |
| 50 | "primero se aplicará al salario el recargo por trabajo en domingos, día de fiesta o duelo nacional, y al resultado se agregará entonces el recargo que corresponda por las horas excedentes" | composición multiplicativa | 1.875 / 2.25 / 3.125 / 3.75 ✅ |

El Art. 50 confirma que la composición es **multiplicativa y en ese orden**, que es
justo lo que hace `OvertimeClassifier.FactorBase`.

> ⚠️ Varias fuentes secundarias en la web citan el domingo como "Art. 49". El texto
> oficial lo ubica en el **Art. 48**; el Art. 49 es fiesta o duelo nacional.

---

## 4. Verificado sin cambios — ISR

Código Fiscal Art. 700: 0 % hasta 11,000 · 15 % del excedente de 11,000 hasta 50,000 ·
5,850 + 25 % sobre el excedente de 50,000. Coincide con
`DefaultPanamaPayrollConfig.GetTaxBracketsForYear`. ✅

---

## 5. PENDIENTE — Salario mínimo desactualizado

`DefaultPanamaPayrollConfig.SalarioMinimoLegal = 1.30m` (B/. por hora, "referencia
comercio").

El **Decreto Ejecutivo N.° 13**, vigente desde el 16 de enero de 2026, fija para
comercio al por menor en gran empresa **B/.3.02/hora (Región 1)** y **B/.2.48/hora
(Región 2)** — 59 tasas distintas para 74 actividades económicas. El valor del código
está desfasado a menos de la mitad.

Además hay una **inconsistencia de unidades** sin resolver:

| Ubicación | Valor | Unidad |
|---|---|---|
| `PayrollTaxConfiguration.SalarioMinimoLegal` | 700.00 | mensual |
| `DefaultPanamaPayrollConfig.SalarioMinimoLegal` | 1.30 | por hora |

Ambos alimentan el mismo campo del DTO. Hay que decidir la unidad canónica y, dado
que el decreto define 59 tasas por actividad y región, probablemente convenga que el
tenant configure la suya en vez de tener una constante global.

No se corrigió en esta pasada: requiere decisión de producto sobre el modelo de datos.

---

## 6. Discrepancia menor sin resolver — tasas de riesgo profesional

| Ubicación | Bajo | Medio | Alto |
|---|---|---|---|
| `PayrollTaxConfiguration` (doc del campo) | 0.56 % | 2.50 % | 5.39 % |
| `DefaultPanamaPayrollConfig` (constantes) | 0.56 % | 2.10 % | 5.67 % |

Las clases de riesgo salen del Reglamento de la CSS (Acuerdo N.° 2 de 1995), que no
se pudo verificar en línea en esta pasada. Conviene confirmarlo contra el reglamento
antes de tocarlo: afecta solo al costo patronal, no al descuento del empleado.
