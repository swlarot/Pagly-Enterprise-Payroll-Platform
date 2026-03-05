# Feature DEV-39: Desglose de horas por tipo en reporte de planilla regular

**Fecha:** 2026-03-05
**Prioridad:** Normal
**Archivos modificados:** 4

---

## Descripción

El reporte de planilla regular mostraba las horas agrupadas en 4 columnas (Regulares, Domingo, Feriado, Extra). Se agrega un desglose expandible por tipo de hora con columnas: **Tipo/Concepto | Horas | Tarifa x Hora | Valor**, visible en la UI, el Excel y el PDF.

---

## Tipos de hora incluidos en el desglose

| Concepto | Campo `PayrollEmployeeHours` (Horas) | Campo (Pago) |
|----------|-------------------------------------|--------------|
| Horas Regulares | `RegularHours` | `RegularPay` |
| Horas Domingo | `SundayHours` | `SundayPay` |
| Horas Feriado | `HolidayHours` | `HolidayPay` |
| H. Extra Diurnas | `OvertimeDayHours` | `OvertimeDayPay` |
| H. Extra Nocturnas | `OvertimeNightHours` | `OvertimeNightPay` |
| H. Extra Feriado | `OvertimeHolidayHours` | `OvertimeHolidayPay` |
| H. Extra Mixtas | `OvertimeMixedHours` | `OvertimeMixedPay` |
| H. Extra Excedentes | `OvertimeExcessHours` | `OvertimeExcessPay` |

**Regla de filtrado:** solo se incluyen filas donde `Horas > 0` y `Pay > 0`. Las ausencias e incapacidades (negativas o informativas) se omiten.

**Tarifa por hora:** `Math.Round(Pay / Horas, 4)` — calculada dinámicamente, no almacenada.

---

## Cambios implementados

### 1. DTO: `ReportePlanillaRegularDto.cs`

```
src/Core/Planilla.Application/DTOs/Reportes/ReportePlanillaRegularDto.cs
```

Se agrega el nuevo record y el campo en `EmpleadoPlanillaRegularItem`:

```csharp
public record LineaDesgloseHoras(
    string TipoConcepto,
    decimal Horas,
    decimal TarifaPorHora,
    decimal Valor
);

public record EmpleadoPlanillaRegularItem(
    ...
    List<LineaDesgloseHoras> DesgloseHoras  // nuevo campo al final
);
```

### 2. Servicio de reportes: `ReportesService.cs`

```
src/Infrastructure/Planilla.Infrastructure/Services/ReportesService.cs
```

Se agrega el método privado `BuildDesgloseHoras(PayrollEmployeeHours?)` que itera los 8 tipos de hora y construye la lista filtrando filas vacías. Se invoca desde `GenerarReportePlanillaRegular` por cada empleado.

Los datos de `PayrollEmployeeHours` ya se cargaban previamente para el reporte (LEFT JOIN existente) — no se agrega ninguna query nueva.

### 3. Exportación: `ExportacionService.cs`

```
src/Infrastructure/Planilla.Infrastructure/Services/ExportacionService.cs
```

**Excel (`ExportarExcelPlanillaRegular`):** Después de cada fila de empleado, si `DesgloseHoras.Count > 0`, se insertan:
- Una fila de encabezado (fondo cian claro, fuente 8pt negrita): Tipo/Concepto | Horas | Tarifa x Hora | Valor
- Una fila por cada `LineaDesgloseHoras` (fuente 8pt cursiva, formato numérico `#,##0.00`)

**PDF (`ExportarPdfPlanillaRegular`):** Después de las 12 celdas del empleado, si hay desglose, se agrega una celda `ColumnSpan(12)` con una mini-tabla interior (encabezado en azul claro, filas de 7pt).

### 4. Página de reportes: `ReportesPage.jsx`

```
src/UI/Planilla.Web/ClientApp/src/pages/ReportesPage.jsx
```

**Estado nuevo:**
```js
const [expandedRows, setExpandedRows] = useState(new Set());
const toggleRow = (idx) => { ... };
```
`expandedRows` se resetea a `new Set()` cada vez que se abre un modal.

**Tabla del modal:** Se añade una columna extra al inicio con el botón `▸/▾`. Al expandir, se inserta un `<tr>` con `colSpan="13"` que contiene la mini-tabla de desglose (fondo `bg-navy-900/60`, encabezado `bg-navy-700`, valores en verde esmeralda).

---

## Comportamiento esperado

### API (`GET /api/reportes/planilla-regular/{id}`)

```json
{
  "empleados": [
    {
      "cedula": "8-123-456",
      "nombreCompleto": "Juan García",
      "horasRegulares": 96.0,
      ...
      "desgloseHoras": [
        { "tipoConcepto": "Horas Regulares", "horas": 96.0, "tarifaPorHora": 5.2083, "valor": 500.00 },
        { "tipoConcepto": "H. Extra Diurnas", "horas": 4.0, "tarifaPorHora": 6.5104, "valor": 26.04 }
      ]
    }
  ]
}
```

### UI

- Empleados sin horas registradas (planilla mensual sin `PayrollEmployeeHours`): sin botón `▸`, `desgloseHoras = []`.
- Empleados con horas: botón `▸` visible; al hacer clic muestra la mini-tabla.
- Solo aparecen tipos de hora con valor > 0.

---

## Notas

- **Tardanzas:** no existen en el modelo actual (`PayrollEmployeeHours` no tiene campo de tardanzas). Se omiten hasta que se implemente.
- No se requiere migración de base de datos.
- La serialización JSON de `List<LineaDesgloseHoras>` es automática vía System.Text.Json con camelCase.
