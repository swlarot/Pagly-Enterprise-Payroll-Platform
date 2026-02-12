# Documentación: Mejoras Completas de Horas Extra y Dashboards

**Fecha:** Febrero 2026  
**Alcance:** Horas extra según Código de Trabajo de Panamá (Arts. 33, 48-49), tipos complejos, reportes, gráficos y validaciones en frontend.

---

## 1. Resumen ejecutivo

Se implementó un sistema integral de horas extra que incluye:

- **Tipos de hora extra** según ley panameña: diurna, nocturna, domingo/feriado, nocturna dom/fer, fiesta nacional diurna/nocturna, mixtas (D-N y N-D) y exceso de horas.
- **Cálculo de factores** correctos (1.25x, 1.50x, 2.25x, 3.125x, 3.75x, 1.50x, 1.75x y factor exceso 1.75x).
- **Días festivos nacionales** de Panamá (fijos y móviles: Carnaval, Semana Santa).
- **Extensión de PayrollEmployeeHours** con campos para festivos, mixtas y exceso.
- **Gráficos** (barras, líneas, torta) y **reportes detallados** de horas extra.
- **Validación en tiempo real** y **sugerencias automáticas** de tipo en el formulario de horas extra.

---

## 2. Backend

### 2.1 Entidades y enums

#### `src/Core/Planilla.Domain/Enums/TipoHoraExtra.cs`

Enum ampliado con todos los tipos legales:

| Valor | Nombre                      | Factor base | Notas                          |
|-------|-----------------------------|------------|--------------------------------|
| 1     | Diurna                      | 1.25x      | 6am–6pm días normales          |
| 2     | Nocturna                    | 1.50x      | 6pm–6am                        |
| 3     | DomingoFeriado              | 1.50x      | Dom/feriado diurna             |
| 4     | NocturnaDomingoFeriado      | 2.25x      | 1.50 × 1.50                    |
| 5     | FiestaNacionalDiurna        | 3.125x     | 2.50 × 1.25 (Art. 49)          |
| 6     | FiestaNacionalNocturna      | 3.75x      | 2.50 × 1.50 (Art. 49)          |
| 7     | MixtaDiurnaNocturna         | 1.50x      | Art. 33                        |
| 8     | MixtaNocturnaDiurna         | 1.75x      | Art. 33                        |

#### `src/Core/Planilla.Domain/Entities/HoraExtra.cs`

Campos añadidos:

- `EsExceso` (bool): indica si supera 3h/día o 9h/semana (Art. 48).
- `FactorExceso` (decimal?): factor adicional 1.75x cuando `EsExceso = true`.
- `Observaciones` (string?): mensajes de validación (ej. exceso).

#### `src/Core/Planilla.Domain/Entities/PayrollEmployeeHours.cs`

Campos nuevos:

- **Horas:** `OvertimeHolidayHours`, `OvertimeMixedHours`, `OvertimeExcessHours` (decimal 8,2).
- **Pagos:** `OvertimeHolidayPay`, `OvertimeMixedPay`, `OvertimeExcessPay` (decimal 18,2).

`TotalHoursPay` se calcula incluyendo estos tres pagos.

---

### 2.2 Servicios nuevos

#### `src/Infrastructure/Planilla.Infrastructure/Services/PanamaHolidayService.cs`

- **Festivos fijos:** 1 ene, 9 ene, 1 may, 3/4/10/28 nov, 8/25/31 dic.
- **Festivos móviles:** Carnaval (lunes y martes), Jueves Santo y Viernes Santo.
- Métodos: `IsNationalHoliday(DateTime)`, `GetHolidayName(DateTime)`, `GetHolidaysForYear(int)`.
- Pascua calculada con algoritmo Meeus/Jones/Butcher.

#### `src/Infrastructure/Planilla.Infrastructure/Services/OvertimeFactorService.cs`

- `DetermineOvertimeType(fecha, horaInicio, horaFin)`: devuelve el `TipoHoraExtra` según fecha/hora (usa `PanamaHolidayService`).
- `CalculateBaseFactor(TipoHoraExtra)`: factor base por tipo.
- `CalculateFactor(TipoHoraExtra, esExceso)`: factor total (incluye 1.75x si exceso).
- `ValidateOvertimeLimits(empleadoId, fecha, horasNuevas)`: devuelve `(bool esExceso, string mensaje)` según límites 3h/día y 9h/semana.

Registro en `Program.cs` (o equivalente): ambos servicios como **Scoped**.

---

### 2.3 DTOs

#### `src/Core/Planilla.Application/DTOs/HoraExtraDto.cs`

Incluye: `EsExceso`, `FactorExceso`, `Observaciones`.

#### `src/Core/Planilla.Application/DTOs/HorasExtraEstadisticasDto.cs`

Estadísticas agregadas: totales por tipo (horas y montos), promedios por semana/mes, días festivos trabajados, horas con exceso, `ComparacionPeriodoDto` (período anterior).

#### `src/Core/Planilla.Application/DTOs/Reportes/ReporteHorasExtraDto.cs`

- `ReporteHorasExtraDto`: empresa, RUC, período, fecha generación, lista de empleados, totales.
- `EmpleadoHorasExtraDto`: por empleado: horas y montos por tipo (diurna, nocturna, dom/fer, festivos, mixtas, exceso).
- `TotalesHorasExtraDto`: totales del reporte.

---

### 2.4 Controladores

#### `HorasExtraController.cs`

**Endpoints existentes (ajustes):**

- `GetTipos()`: devuelve todos los tipos con nombre y factor.
- `Create` / `CreateBatch` / `Update` / `Aprobar`: usan `OvertimeFactorService` para validar límites, calcular factores y rellenar `EsExceso`, `FactorExceso`, `Observaciones`. `CalcularMonto()` acepta `factorExceso` opcional.
- `MapToDto`: mapea los nuevos campos.

**Endpoints nuevos:**

| Método | Ruta | Descripción |
|--------|------|-------------|
| GET | `/api/horasextra/estadisticas` | Estadísticas agregadas. Query: `empleadoId`, `fechaInicio`, `fechaFin`. |
| GET | `/api/horasextra/validar-limites` | Valida límites legales. Query: `empleadoId`, `fecha`, `horasNuevas`. Devuelve horas del día/semana, disponibles, porcentajes, `esExceso`, `mensaje`. |
| GET | `/api/horasextra/es-festivo` | Indica si la fecha es festivo nacional. Query: `fecha`. Devuelve `esFestivo`, `nombreFestivo`, `fechaStr`. |
| GET | `/api/horasextra/sugerir-tipo` | Sugiere tipo según fecha y horario. Query: `fecha`, `horaInicio`, `horaFin`. Devuelve `tipoSugerido`, `nombreTipo`, `factor`, `esFestivo`, `esDomingo`. |

#### `PayrollHeadersController.cs`

- **CalculatePayroll:** si hay horas extra aprobadas, usa `AsistenciaCalculationService.CalcularMontoHorasExtra` y asigna a `PayrollDetail` (OvertimePay, HorasExtraDiurnas/Nocturnas/DomingoFeriado, MontoHorasExtra). Si no hay aprobadas, usa `PayrollEmployeeHours` incluyendo los nuevos campos (OvertimeHolidayPay, OvertimeMixedPay, OvertimeExcessPay) y los suma en `TotalHoursPay`.
- **ImportNovedades:** reparte horas extra por tipo: FiestaNacional* → `OvertimeHolidayHours`, Mixta* → `OvertimeMixedHours`, y si `EsExceso` → `OvertimeExcessHours`; además mantiene `OvertimeDayHours`/`OvertimeNightHours` para compatibilidad.
- **UpsertEmployeeHours:** request y persistencia incluyen `OvertimeHolidayHours`, `OvertimeMixedHours`, `OvertimeExcessHours`.

#### `ReportesController.cs`

- GET `/api/reportes/horas-extra/{planillaId}`: JSON del reporte de horas extra.
- GET `/api/reportes/horas-extra/{planillaId}/excel`: placeholder (retorna JSON).
- GET `/api/reportes/horas-extra/{planillaId}/pdf`: placeholder (retorna JSON).

---

### 2.5 Servicios de reportes y cálculo

#### `ReportesService.cs`

- **GenerarReporteHorasExtra(planillaId):** obtiene planilla y horas extra aprobadas del período; agrupa por empleado; calcula horas y montos por tipo; devuelve `ReporteHorasExtraDto` con empleados (solo con horas > 0) y totales.

#### `AsistenciaCalculationService.cs`

- **CalcularMontoHorasExtra:** el `switch` por `TipoHoraExtra` incluye todos los tipos y factores; si `EsExceso` y `FactorExceso` tienen valor, se aplica el factor de exceso al total. Acumula `horasDiurnas`, `horasNocturnas`, `horasDomingoFeriado` para el desglose.

---

### 2.6 Cálculo de la planilla y flujo de horas extra

Esta sección describe **cómo se calcula la planilla ahora** con todos los cambios: origen del salario bruto, fórmulas de horas y prioridad entre módulo de horas extra aprobadas y horas manuales.

#### Flujo general de `CalculatePayroll` (por empleado)

1. Se cargan los registros de **PayrollEmployeeHours** de la planilla (si existen).
2. Para cada empleado activo:
   - **GrossPay (salario bruto):**
     - Si existe registro en **PayrollEmployeeHours**, se calculan todos los pagos por horas (regulares, domingo, feriado, extras diurnas/nocturnas, **festivos, mixtas, exceso**) y se suma el **TotalHoursPay**; ese valor es el **grossPay** que se envía al orquestador (CSS, SE, ISR).
     - Si no existe registro de horas, **grossPay = SalarioBase** del empleado.
   - El orquestador devuelve deducciones y neto a partir de ese **grossPay**.
   - Luego se determina el **monto de horas extra** que se guarda en **PayrollDetail** (OvertimePay, desglose, MontoHorasExtra).
   - Se crea el **PayrollDetail** con GrossPay, OvertimePay, desglose de horas, deducciones y neto.

#### Fórmulas cuando se usan PayrollEmployeeHours (sin horas extra aprobadas)

Cuando el empleado tiene registro en **PayrollEmployeeHours**, los pagos se calculan así:

| Concepto | Fórmula |
|----------|--------|
| RegularPay | `RegularHours × HourlyRate` |
| SundayPay | `SundayHours × HourlyRate × 1.50` |
| HolidayPay | `HolidayHours × HourlyRate × 1.50` |
| OvertimeDayPay | `OvertimeDayHours × HourlyRate × 1.25` |
| OvertimeNightPay | `OvertimeNightHours × HourlyRate × 1.50` |
| **OvertimeHolidayPay** | `OvertimeHolidayHours × HourlyRate × 3.4375` (promedio 3.125 y 3.75) |
| **OvertimeMixedPay** | `OvertimeMixedHours × HourlyRate × 1.625` (promedio 1.50 y 1.75) |
| **OvertimeExcessPay** | `OvertimeExcessHours × HourlyRate × 2.40625` (factor promedio con exceso) |
| AbsenceDeduction | `AbsenceHours × HourlyRate` |
| **TotalHoursPay** | Suma de todos los pagos anteriores menos AbsenceDeduction |

**HourlyRate** se obtiene de: `Employee.HourlyRate` si &gt; 0; si no, `SalarioBase / HoursPerPeriod` o `SalarioBase` según corresponda.

#### Prioridad: horas extra aprobadas vs horas manuales (PayrollDetail)

El **OvertimePay** y el desglose que se guardan en **PayrollDetail** dependen de si el empleado tiene **horas extra aprobadas** en el período:

- **Si hay horas extra aprobadas** (módulo Horas Extra, aprobadas, en el rango de fechas de la planilla):
  - Se llama a **AsistenciaCalculationService.CalcularMontoHorasExtra**.
  - Se usa el **salario por hora** del empleado (HourlyRate o SalarioBase/208, etc.).
  - **OvertimePay** = monto total devuelto por `CalcularMontoHorasExtra`.
  - **HorasExtraDiurnas**, **HorasExtraNocturnas**, **HorasExtraDomingoFeriado** = desglose devuelto por el servicio.
  - **MontoHorasExtra** = mismo valor que OvertimePay.
  - El **GrossPay** que va al orquestador sigue siendo el que sale de **PayrollEmployeeHours.TotalHoursPay** (si existe registro) o SalarioBase; es decir, si hay registro de horas, el bruto ya incluye todos los conceptos de la tabla de horas (incluidas festivos, mixtas, exceso).

- **Si no hay horas extra aprobadas** pero sí registro en **PayrollEmployeeHours** (fallback):
  - **OvertimePay** = `OvertimeDayPay + OvertimeNightPay` (solo diurna + nocturna del registro).
  - **HorasExtraDiurnas** = OvertimeDayHours, **HorasExtraNocturnas** = OvertimeNightHours.
  - **HorasExtraDomingoFeriado** = 0 en este fallback.
  - **MontoHorasExtra** = OvertimePay.
  - Nota: el **GrossPay** enviado al orquestador sí incluye **TotalHoursPay** (con OvertimeHolidayPay, OvertimeMixedPay, OvertimeExcessPay); solo el desglose en PayrollDetail queda resumido en día/nocturna en este caso.

#### Cálculo detallado en AsistenciaCalculationService.CalcularMontoHorasExtra

- Entrada: **empleadoId**, **salarioHora**, **periodoInicio**, **periodoFin**.
- Se obtienen las **horas extra aprobadas** del período (GetHorasExtraAprobadas).
- Por cada **HoraExtra**:
  - **Factor base** según `TipoHoraExtra`: 1.25 (Diurna), 1.50 (Nocturna, DomingoFeriado, MixtaDiurnaNocturna), 2.25 (NocturnaDomingoFeriado), 3.125 (FiestaNacionalDiurna), 3.75 (FiestaNacionalNocturna), 1.75 (MixtaNocturnaDiurna).
  - **Factor total** = factor base × **FactorExceso** (si `EsExceso` y `FactorExceso` tienen valor; típicamente 1.75).
  - **Monto** = salarioHora × CantidadHoras × factorTotal.
  - Se acumula monto total y desglose: **horasDiurnas** (Diurna, MixtaDiurnaNocturna), **horasNocturnas** (Nocturna, MixtaNocturnaDiurna), **horasDomingoFeriado** (resto: DomingoFeriado, NocturnaDomingoFeriado, FiestaNacionalDiurna, FiestaNacionalNocturna).
- Retorna: **(montoTotal, horasDiurnas, horasNocturnas, horasDomingoFeriado)**.

#### ImportNovedades: cómo se llenan los campos de PayrollEmployeeHours

Al importar novedades desde el módulo de horas extra y ausencias:

- Por cada empleado se consultan **horas extra aprobadas** y **ausencias** del período de la planilla.
- **Horas extra** se clasifican así:
  - **OvertimeDayHours:** Diurna, DomingoFeriado; además FiestaNacionalDiurna y MixtaDiurnaNocturna (compatibilidad).
  - **OvertimeNightHours:** Nocturna, NocturnaDomingoFeriado; además FiestaNacionalNocturna y MixtaNocturnaDiurna (compatibilidad).
  - **OvertimeHolidayHours:** FiestaNacionalDiurna + FiestaNacionalNocturna.
  - **OvertimeMixedHours:** MixtaDiurnaNocturna + MixtaNocturnaDiurna.
  - **OvertimeExcessHours:** suma de CantidadHoras donde **EsExceso == true**.
- **AbsenceHours:** a partir de ausencias (días × 8 horas).
- Si ya existía registro en PayrollEmployeeHours, se actualiza (overwrite o sum según parámetros); si no, se crea uno nuevo con estos valores.

Con esto, al **calcular la planilla** después de importar, si no se usan horas extra aprobadas para ese empleado, el **TotalHoursPay** y por tanto el **GrossPay** ya incluyen festivos, mixtas y exceso mediante las fórmulas de la tabla anterior.

#### Resumen: qué incluye “todo lo demás”

- **Cálculo de la planilla:** GrossPay por empleado viene de TotalHoursPay (con todos los tipos de horas) o SalarioBase; el orquestador calcula CSS, SE, ISR y neto con ese bruto.
- **PayrollDetail:** OvertimePay y desglose se llenan desde horas extra aprobadas (CalcularMontoHorasExtra) cuando hay; si no, desde OvertimeDayPay + OvertimeNightPay del registro de horas (el bruto sí puede incluir festivos/mixtas/exceso vía TotalHoursPay).
- **ImportNovedades:** reparte las horas extra aprobadas en los campos de PayrollEmployeeHours (incluidos OvertimeHolidayHours, OvertimeMixedHours, OvertimeExcessHours) para que, al calcular, las fórmulas de la tabla apliquen los factores correctos.
- **Reportes y gráficos:** usan los mismos datos (PayrollDetail, HorasExtra, estadísticas) para mostrar totales y desgloses coherentes con este cálculo.

---

### 2.7 Migraciones de base de datos

1. **AddOvertimeExcessFields** (tabla `HorasExtra`): columnas `EsExceso`, `FactorExceso`, `Observaciones` (si aplica).
2. **AddOvertimeComplexFieldsToPayrollEmployeeHours** (tabla `PayrollEmployeeHours`): columnas `OvertimeHolidayHours`, `OvertimeMixedHours`, `OvertimeExcessHours`, `OvertimeHolidayPay`, `OvertimeMixedPay`, `OvertimeExcessPay` (tipos decimal según entidad).

**Aplicar migraciones:**

```bash
dotnet ef database update --project src/Infrastructure/Planilla.Infrastructure --startup-project src/UI/Planilla.Web
```

*Nota:* La conexión usa la cadena de `appsettings.json` (o `appsettings.Development.json`). Si la autenticación falla, configurar la contraseña correcta de PostgreSQL en un archivo de configuración local no versionado.

---

## 3. Frontend

### 3.1 Página Horas Extra (`HorasExtraPage.jsx`)

**Nombres “Por Tipo”:**

- Función `getTipoDisplayName(tipoValor)` con nombres cortos únicos: Diurna, Nocturna, Dom/Fer, Noct. Dom/Fer, Fiesta Diurna, Fiesta Nocturna, Mixta D-N, Mixta N-D.
- La card “Por Tipo” usa `tipo.displayName` en lugar de `tipo.nombre.split(' ')[0]`.

**Gráficos:**

- Datos derivados de `porTipo` y `horasExtra`: `chartDataByType` (tipo, horas, monto) y `pieChartData` (name, value, color).
- Se muestran `OvertimeByTypeBarChart` y `OvertimeCostDistributionPieChart` cuando hay datos.

**Validación y sugerencias:**

- Estado: `limites`, `festivoInfo`, `sugerenciaTipo`, `validando`.
- Efectos: al cambiar empleado/fecha/horas se llama a `validarLimites`; al cambiar fecha, `verificarFestivo`; al cambiar fecha/hora inicio/fin, `sugerirTipo`.
- `calcularHoras(inicio, fin)` para las horas del bloque.
- En el formulario:
  - Debajo de Fecha: si `festivoInfo.esFestivo`, se muestra banner con nombre del festivo.
  - En Tipo: si la sugerencia difiere del tipo elegido, se muestra mensaje y botón “Usar tipo sugerido”.
  - Bloque “Límites legales”: componente `OvertimeLimitsChart` (barras día/semana), mensaje de exceso si `limites.esExceso`, alertas amarillas si porcentaje día o semana ≥ 70 % y < 90 %.
- Al enviar: si `limites.esExceso`, se muestra `window.confirm` con el mensaje de advertencia antes de enviar; el botón de submit se estiliza en rojo y con indicador de advertencia.
- `resetForm` limpia también `limites`, `festivoInfo` y `sugerenciaTipo`.

---

### 3.2 Componentes de gráficos

**Ubicación:** `src/UI/Planilla.Web/ClientApp/src/components/charts/`

| Archivo | Descripción |
|---------|-------------|
| `OvertimeByTypeBarChart.jsx` | Barras por tipo (horas). Props: `data[]` con `tipo`, `horas`, `monto`; `title`. |
| `OvertimeTrendLineChart.jsx` | Líneas de tendencia (horas y monto por mes). Props: `data[]` con `mes`, `horas`, `monto`; `title`. |
| `OvertimeCostDistributionPieChart.jsx` | Torta de distribución de costos. Props: `data[]` con `name`, `value`, `color`; `title`. |
| `OvertimeLimitsChart.jsx` | Barras de progreso para límite diario (3h) y semanal (9h). Props: `horasDelDia`, `horasDeLaSemana`, `limiteDiario`, `limiteSemanal`. Colores: verde &lt; 70 %, amarillo 70–90 %, rojo &gt; 90 %. |

Librería: **recharts** (por ejemplo `^2.12.0` en `package.json`).

---

### 3.3 Planillas (`PlanillasPage.jsx`)

- **Tabla de horas:** columnas Extra Festivos, Extra Mixtas, Extra Exceso; inputs editables para `overtimeHolidayHours`, `overtimeMixedHours`, `overtimeExcessHours`.
- **normalizeHoursRow** y **saveEmployeeHours** incluyen los tres nuevos campos.
- **handleHoursChange** actualiza estado y debounce para guardar.

---

### 3.4 Reportes (`ReportesPage.jsx`)

- Nueva card “Horas Extra” (tipo `horas-extra`) que llama a `GET /api/reportes/horas-extra/{planillaId}` y abre el modal.
- En el modal, para `modalType === 'horas-extra'`: tabla con columnas por empleado (Cédula, Nombre, Hrs Diurna, Nocturna, Dom/Fer, Festivos, Mixtas, Exceso, Total Hrs, Monto Total) y fila de totales.
- Títulos del modal: se añade `'horas-extra': 'Reporte de Horas Extra'`.
- Los botones “Descargar Excel” y “Descargar PDF” para este reporte usan los endpoints actuales (por ahora pueden devolver JSON/placeholder).

---

## 4. Resumen de archivos tocados o creados

### Backend (C#)

- **Domain:** `TipoHoraExtra.cs`, `HoraExtra.cs`, `PayrollEmployeeHours.cs`
- **Application:** `HoraExtraDto.cs`, `HorasExtraEstadisticasDto.cs`, `Reportes/ReporteHorasExtraDto.cs`
- **Infrastructure:** `PanamaHolidayService.cs`, `OvertimeFactorService.cs`, `ReportesService.cs` (método GenerarReporteHorasExtra), `AsistenciaCalculationService.cs`, migraciones `AddOvertimeExcessFields`, `AddOvertimeComplexFieldsToPayrollEmployeeHours`
- **Web:** `HorasExtraController.cs`, `PayrollHeadersController.cs`, `ReportesController.cs`, `Program.cs` (registro de servicios)

### Frontend (React/JSX)

- **Páginas:** `HorasExtraPage.jsx`, `PlanillasPage.jsx`, `ReportesPage.jsx`
- **Componentes:** `charts/OvertimeByTypeBarChart.jsx`, `OvertimeTrendLineChart.jsx`, `OvertimeCostDistributionPieChart.jsx`, `OvertimeLimitsChart.jsx`
- **Dependencia:** `recharts` en `ClientApp/package.json`

---

## 5. Cómo probar

1. **Build:**  
   - Backend: `dotnet build src/UI/Planilla.Web/Vorluno.Planilla.Web.csproj`  
   - Frontend: `cd src/UI/Planilla.Web/ClientApp && npm run build`

2. **Migraciones:**  
   Configurar conexión a PostgreSQL y ejecutar:  
   `dotnet ef database update --project src/Infrastructure/Planilla.Infrastructure --startup-project src/UI/Planilla.Web`

3. **Flujo sugerido:**  
   - Crear/editar horas extra y comprobar sugerencia de tipo y aviso de festivo.  
   - Comprobar límites diario/semanal y mensaje de exceso.  
   - Aprobar horas y calcular planilla; revisar importar novedades y que los nuevos campos se llenen.  
   - En Reportes, elegir planilla y “Horas Extra”; revisar tabla y totales.  
   - En Horas Extra, ver gráficos de barras y torta cuando existan datos.

---

## 6. Pendientes / mejoras futuras

- Implementar exportación real a Excel y PDF para el reporte de horas extra (en `ExportacionService` y enlaces en Reportes).
- Aplicar la migración `AddOvertimeComplexFieldsToPayrollEmployeeHours` cuando la base de datos esté accesible (misma orden que en la sección 2.6).
- Opcional: usar `OvertimeTrendLineChart` en un dashboard o en Horas Extra con datos por mes desde estadísticas o planillas.

---

*Documento generado a partir de la implementación realizada en el proyecto Planilla (Pagly).*
