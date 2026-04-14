# Prueba en empresa (flujo contador – P1)

Ya estás dentro de la empresa. Una sola ruta y qué debe darte al final.

---

## Ruta

**Empleados** → **Planillas** (nueva → horas → calcular) → **detalle/recibo**.

1. **Empleados** (`/empleados`): Crear o editar al menos un empleado con **Salario base**, **Tipo de período** (ej. Quincenal), **Horas/semana** (ej. 48). Guardar. Anota la **Tasa/h** que muestra (ej. Salario 1040 quincenal, 104 h → 10.00 $/h).
2. **Planillas** (`/planillas`): **Nueva planilla** con el mismo **Tipo de período**, rango de fechas y nombre. Guardar (queda en Borrador).
3. En esa planilla en Borrador: abrir **Horas trabajadas** → **Auto-llenar Regulares** (o cargar horas a mano). Guardar cambios.
4. **Calcular planilla**. Cuando pase a Calculada, abrir el detalle o los recibos.

---

## Valores que debe darte al final (lo que espera un contador)

Con los datos que ingresaste, al final debes poder ver **por empleado** algo equivalente a:

| Concepto | Qué es | Ejemplo (si bruto = 1040 quincenal) |
|----------|--------|--------------------------------------|
| **Salario bruto** | Pago por horas (regulares + recargos domingo/feriado/extras menos ausencias) | 1040.00 (o el que salga de tus horas × tasa) |
| **CSS (empleado)** | % sobre bruto (según configuración) | Ej. 9.75% → 101.40 |
| **Seguro Educativo** | % sobre bruto | Ej. 1.25% → 13.00 |
| **ISR** | Retención por tabla (anualizado según tipo de período de la planilla) | Según tabla y dependientes |
| **Neto a pagar** | Bruto − CSS − SE − ISR (y otras deducciones si hay) | Bruto − todas las deducciones |

Además:

- **Tasa/h** del empleado = Salario base del período ÷ Horas del período (ej. 1040 ÷ 104 = 10.00).
- **Bruto** = suma de (horas regulares × tasa) + recargos (domingo/feriado 1.50×, extra diurna 1.25×, extra nocturna 1.50×) − descuento por ausencias.
- El **ISR** debe calcularse según el **tipo de período de la planilla** (quincenal = 24 períodos/año, etc.), no solo el del empleado.

Si al final de la ruta ves esos conceptos con montos coherentes con lo que ingresaste, el flujo para el contador está cubierto.

---

## Si "Calcular Planilla" dice que falta configuración CSS

El error *"No se encontró configuración de CSS activa para companyId=..."* significa que a tu empresa no se creó la configuración de impuestos (CSS, SE, ISR).

**Solución:** Ve a **Configuración** (menú lateral) → pestaña **Tasas CSS/SE**. Si aparece el mensaje "No hay configuración de impuestos", haz clic en **"Crear configuración por defecto (Ley 462)"**. Luego vuelve a **Planillas** y **Calcular Planilla**.
