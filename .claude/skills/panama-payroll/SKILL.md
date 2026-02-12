---
name: panama-payroll
description: >
  Panama payroll compliance calculations and rules. Use when implementing
  CSS (Caja de Seguro Social), ISR (income tax), Seguro Educativo,
  décimo tercer mes, vacaciones, overtime, or any payroll calculation for Panama.
  Covers Ley 462, Código de Trabajo Arts. 31-49, and all 8 overtime types.
---
# Compliance de Planilla Panameña

## Deducciones del Empleado
- **CSS (Seguro Social)**: 9.75% del salario bruto (tope B/.1,500 mensual)
- **Seguro Educativo**: 1.25% del salario bruto (SIN tope)
- **ISR (Impuesto sobre la Renta)**: escala progresiva ANUAL:
  - B/.0 — B/.11,000: 0% (exento)
  - B/.11,000.01 — B/.50,000: 15% sobre el excedente de B/.11,000
  - B/.50,000.01+: B/.5,850 + 25% sobre el excedente de B/.50,000

## Aportes del Empleador
- **CSS Patronal**: 12.25% del salario bruto (Ley 462 escalonado)
- **Seguro Educativo Patronal**: 1.50% del salario bruto (SIN tope)
- **Riesgos Profesionales**: varía por actividad (0.56% — 5.67%)

## Topes CSS (según años cotizados)
- Estándar: B/.1,500 mensual
- Intermedio: B/.2,000 (25+ años cotizados, promedio >= B/.2,000)
- Alto: B/.2,500 (30+ años cotizados, promedio >= B/.2,500)

## PayPeriodType y Anualización ISR
El ISR se anualiza según el tipo de período de la **PLANILLA** (no del empleado):
- Semanal: 52 períodos/año
- Bisemanal: 26 períodos/año
- Quincenal: 24 períodos/año
- Mensual: 12 períodos/año

## Horas Extra — Código de Trabajo de Panamá

### 8 Tipos de Hora Extra (TipoHoraExtra enum)

| Tipo | Factor | Descripción | Artículo |
|------|--------|-------------|----------|
| Diurna | 1.25x | 6am–6pm días normales | Art. 33 |
| Nocturna | 1.50x | 6pm–6am | Art. 33 |
| DomingoFeriado | 1.50x | Domingo/feriado diurna | |
| NocturnaDomingoFeriado | 2.25x | 1.50 × 1.50 | |
| FiestaNacionalDiurna | 3.125x | 2.50 × 1.25 | Art. 49 |
| FiestaNacionalNocturna | 3.75x | 2.50 × 1.50 | Art. 49 |
| MixtaDiurnaNocturna | 1.50x | Jornada mixta D→N | Art. 33 |
| MixtaNocturnaDiurna | 1.75x | Jornada mixta N→D | Art. 33 |

### Exceso de Horas (Art. 48)
- Límite diario: 3 horas extra máximo
- Límite semanal: 9 horas extra máximo
- Factor adicional por exceso: 1.75x (se multiplica al factor base)

### Festivos Nacionales de Panamá
**Fijos:** 1 ene, 9 ene, 1 may, 3 nov, 4 nov, 10 nov, 28 nov, 8 dic, 25 dic, 31 dic
**Móviles:** Lunes y Martes de Carnaval, Jueves Santo, Viernes Santo

## Décimo Tercer Mes
- Tres partidas anuales: 15 abril, 15 agosto, 15 diciembre
- Cálculo: salario de los 4 meses anteriores / 3
- Período 1: dic–mar → pago 15 abril
- Período 2: abr–jul → pago 15 agosto
- Período 3: ago–nov → pago 15 diciembre

## Vacaciones
- 30 días por cada 11 meses de trabajo continuo
- Proporcional: (días trabajados / 11 meses) × 30 días

## Prima de Antigüedad
- 1 semana de salario por cada año trabajado (al terminar relación laboral)

## Jornada Laboral (Art. 31)
- Diurna: máximo 8 horas/día, 48 horas/semana
- Nocturna: máximo 7 horas/día, 42 horas/semana
- Mixta: máximo 7.5 horas/día, 45 horas/semana

## Reglas de Código
1. SIEMPRE usar `decimal` (nunca float/double) para montos monetarios
2. SIEMPRE redondear a 2 decimales con `MidpointRounding.ToEven`
3. ISR se anualiza usando PayPeriodType de la PLANILLA (no del empleado)
4. CSS tiene tope, SE NO tiene tope
5. HourlyRate = SalarioBase / HoursPerPeriod (4 decimales)
