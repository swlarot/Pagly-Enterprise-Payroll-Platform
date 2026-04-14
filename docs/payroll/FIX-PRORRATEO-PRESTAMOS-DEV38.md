# Fix DEV-38: Prorrateo de cuota de préstamo por frecuencia de pago

**Fecha:** 2026-03-05
**Prioridad:** Urgente
**Impacto:** Todos los empleados con préstamos activos en planillas no mensuales (quincenal, bisemanal, semanal)
**Archivos modificados:** 2

---

## Problema

Las deducciones de préstamos se aplicaban usando `CuotaMensual` directamente como monto del período, sin importar la frecuencia de pago de la planilla. En una planilla quincenal (24 períodos/año), esto generaba el doble del descuento correcto.

### Ejemplo numérico

Empleado con préstamo `CuotaMensual = B/.200`, planilla **quincenal** (24 períodos/año):

| Concepto | Bug | Fix |
|---------|-----|-----|
| Descuento por quincena | B/.200.00 | B/.100.00 |
| Descuento anual total | B/.4,800.00 | B/.2,400.00 |
| Cuota mensual equivalente | B/.400.00 (DOBLE) | B/.200.00 (correcto) |

Misma situación por frecuencia:

| Frecuencia | Períodos/año | CuotaMensual | Bug descuenta | Fix descuenta |
|------------|-------------|--------------|---------------|---------------|
| Mensual | 12 | B/.200 | B/.200 (correcto) | B/.200 (sin cambio) |
| Quincenal | 24 | B/.200 | B/.200 (DOBLE) | B/.100 |
| Bisemanal | 26 | B/.200 | B/.200 (~8.3% de más) | B/.92.31 |
| Semanal | 52 | B/.200 | B/.200 (CUÁDRUPLE) | B/.46.15 |

---

## Causa raíz

El código construía la `DeduccionPendiente` para cada préstamo activo usando directamente `prestamo.CuotaMensual` como `MontoFijo`, sin convertirlo al período de la planilla.

### Código anterior (buggy)

```csharp
// PayrollHeadersController.cs ~453
deduccionesPendientes.Add(new DeduccionPendiente
{
    ...
    MontoFijo = prestamo.CuotaMensual,  // ← Bug: monto mensual en período no-mensual
    ...
});
```

El mismo patrón existía en `PayrollProcessingService.cs` línea ~252.

---

## Fix aplicado

### Fórmula de prorrateo

```
montoCuotaPeriodo = CuotaMensual × 12 / PeríodosPorAño
montoCuotaPeriodo = Min(montoCuotaPeriodo, MontoPendiente)  // cap para última cuota
```

La infraestructura `PayrollConstants.GetPeriodsPerYear(PayPeriodType)` ya existía y fue reutilizada.

### Archivos modificados

**1. `src/UI/Planilla.Web/Controllers/PayrollHeadersController.cs`**

Flujo activo de cálculo de planilla. Se agrega el `using` de `PayrollConstants` y se introduce el prorrateo antes del `foreach` de préstamos:

```csharp
var periodsPerYear = PayrollConstants.GetPeriodsPerYear(payrollHeader.PayPeriodType);

foreach (var prestamo in prestamosActivos)
{
    var montoCuotaPeriodo = Math.Round(prestamo.CuotaMensual * 12m / periodsPerYear, 2);
    montoCuotaPeriodo = Math.Min(montoCuotaPeriodo, prestamo.MontoPendiente);

    deduccionesPendientes.Add(new DeduccionPendiente
    {
        ...
        MontoFijo = montoCuotaPeriodo,
        ...
    });
}
```

**2. `src/Infrastructure/Planilla.Infrastructure/Services/PayrollProcessingService.cs`**

Método `GetDeduccionesAdicionalesConPrelacionAsync`. El parámetro `payPeriodType` ya estaba disponible en la firma del método:

```csharp
var periodsPerYear = PayrollConstants.GetPeriodsPerYear(payPeriodType);

foreach (var prestamo in prestamos)
{
    var montoCuotaPeriodo = Math.Round(prestamo.CuotaMensual * 12m / periodsPerYear, 2);
    montoCuotaPeriodo = Math.Min(montoCuotaPeriodo, prestamo.MontoPendiente);

    deduccionesPendientes.Add(new DeduccionPendiente
    {
        ...
        MontoFijo = montoCuotaPeriodo,
        ...
    });
}
```

---

## Notas importantes

- **`ProcessPrestamosAsync` (línea ~324) no se modificó.** Ese método registra `MontoPagado = prestamo.CuotaMensual` al momento de liquidar el préstamo, lo que es parte de DEV-25 (implementar endpoint `/pay`). Se corregirá cuando se implemente ese endpoint.
- La última cuota está protegida por el `Math.Min(..., prestamo.MontoPendiente)` para evitar descontar más de lo que queda pendiente.
- No se requiere migración de base de datos — solo lógica de cálculo.

---

## Relación con otros tickets

| Ticket | Relación |
|--------|----------|
| DEV-25 | Implementa endpoint `/pay`; corregirá `MontoPagado` en `ProcessPrestamosAsync` |
| DEV-35 | Consolida lógica duplicada controller/service; el fix está en ambos lugares hasta que DEV-35 se resuelva |
