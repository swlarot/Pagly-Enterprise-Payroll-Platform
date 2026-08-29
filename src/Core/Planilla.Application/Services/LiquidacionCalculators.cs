// ====================================================================
// Planilla - LiquidacionCalculators
// Creado: 2026-06-18 — Descomposición del cálculo de liquidación en
//   calculadoras puras, espejando el motor de Talento (rrhh-urbis-api,
//   src/domain/payroll/*.ts) ya auditado contra la ley primaria.
//
// Cada pieza cita su artículo del Código de Trabajo de Panamá. El redondeo
// usa RoundingPolicy (AwayFromZero) para paridad numérica con Talento.
// ====================================================================

using Vorluno.Planilla.Application.Helpers;
using Vorluno.Planilla.Domain.Enums;

namespace Vorluno.Planilla.Application.Services;

/// <summary>Parámetros de cálculo de una liquidación. Los salarios base llegan separados
/// porque cada partida tiene su propia regla legal de cómputo.</summary>
public record LiquidacionCalcInput
{
    /// <summary>Salario mensual base para prima de antigüedad (Art. 224 + 226: promedio últimos 5 años).</summary>
    public required decimal MonthlySalaryForPrima { get; init; }
    /// <summary>Salario mensual base para indemnización (Art. 225 + 149: promedio 6 meses/30 días, lo más favorable).</summary>
    public required decimal MonthlySalaryForIndemnization { get; init; }
    /// <summary>Salario mensual corriente para vacaciones/décimo (Art. 144).</summary>
    public required decimal MonthlySalaryForDailyRate { get; init; }
    /// <summary>Años trabajados (con decimales — ej. 7.5).</summary>
    public required decimal YearsWorked { get; init; }
    /// <summary>Causa de terminación — dispara la indemnización Art. 225 cuando aplica.</summary>
    public required CausaTerminacion Causa { get; init; }
    /// <summary>Días de vacaciones vencidas no pagadas.</summary>
    public decimal UnpaidVacationDays { get; init; }
    /// <summary>Décimos prorrateados calculados externamente (0 por defecto).</summary>
    public decimal ProratedDecimos { get; init; }
    /// <summary>Otras indemnizaciones pactadas o propinas (digitadas por el usuario).</summary>
    public decimal OtrasIndemnizaciones { get; init; }
    /// <summary>Recargo Art. 219 (0..1): solo cuando un tribunal ordena reintegro y el empleador paga.</summary>
    public decimal RecargoArt219Percentage { get; init; }
    /// <summary>Trabajador(a) doméstico(a) — régimen Decreto 39/2014 (cambia el preaviso).</summary>
    public bool IsDomesticWorker { get; init; }
    /// <summary>¿Se otorgó preaviso?</summary>
    public bool PreavisoOtorgado { get; init; }
    /// <summary>Días efectivamente otorgados de preaviso (si PreavisoOtorgado).</summary>
    public decimal? PreavisoDiasOtorgados { get; init; }
    /// <summary>Tipo de contrato — Indefinido usa prima Art. 224; Definido/PorObra usa cesantía.</summary>
    public TipoContratoDuracion ContractDurationType { get; init; } = TipoContratoDuracion.Indefinido;
}

/// <summary>Resultado del cálculo de preaviso.</summary>
public record PreavisoCalcResult(
    decimal DiasRequeridos,
    decimal DiasOtorgados,
    decimal DiasDebidos,
    decimal CompensacionAmount,
    string Source);

/// <summary>Resultado detallado de la liquidación (espejo de Talento LiquidacionResult).</summary>
public record LiquidacionCalcResult
{
    public decimal WeeklySalaryForPrima { get; init; }
    public decimal WeeklySalaryForIndemnization { get; init; }
    public decimal DailySalary { get; init; }
    public decimal PrimaAntiguedadAmount { get; init; }
    public decimal IndemnizacionWeeks { get; init; }
    public decimal IndemnizacionDespidoAmount { get; init; }
    public decimal RecargoArt219Percentage { get; init; }
    public decimal RecargoArt219Amount { get; init; }
    public decimal VacacionesNoPagadasAmount { get; init; }
    public decimal DecimosProrrateadosAmount { get; init; }
    public decimal OtrasIndemnizaciones { get; init; }
    public decimal VacacionesProporcionalAmount { get; init; }

    /// <summary>
    /// Dias de vacacion que representa <see cref="VacacionesProporcionalAmount"/>,
    /// a razon de 1 dia por cada 11 trabajados (Art. 54.1). Se expone junto al monto
    /// para que la UI no tenga que reconstruirlo y ambos no puedan divergir.
    /// </summary>
    public decimal VacacionesProporcionalDias { get; init; }
    public decimal DecimoProporcionalAmount { get; init; }
    public decimal CesantiaAmount { get; init; }
    public decimal PreavisoDiasRequeridos { get; init; }
    public decimal PreavisoDiasOtorgados { get; init; }
    public decimal PreavisoDiasDebidos { get; init; }
    public decimal PreavisoCompensacionAmount { get; init; }
    public string PreavisoSource { get; init; } = string.Empty;
    public decimal TotalAmount { get; init; }
}

/// <summary>
/// Calculadoras puras de liquidación laboral (Código de Trabajo de Panamá).
/// Port del motor de Talento, validado contra MITRADEL y la ley primaria.
/// </summary>
public static class LiquidacionCalculator
{
    // 52 / 12 ≈ 4.3333 semanas por mes
    private const decimal WeeksPerMonth = 52m / 12m;
    // Prima de antigüedad: 1 semana de salario por año (Art. 224, sin escalones)
    private const decimal PrimaAntiguedadWeeksPerYear = 1m;
    // Indemnización Art. 225 (post-Ley 44/1995)
    private const decimal IndemWeeksFirst10Years = 3.4m;
    private const decimal IndemWeeksAfter10Years = 1m;
    private const decimal IndemThresholdYears = 10m;
    // Cesantía Fondo (Decreto 60/1995): 6% del salario mensual por mes trabajado
    private const decimal CesantiaRate = 0.06m;
    private const decimal DiasVacacionUnit = 11m; // Art. 54.1: 1 día por cada 11 días

    // Causas que devengan indemnización Art. 225 (además de la prima Art. 224).
    private static readonly HashSet<CausaTerminacion> CausasConIndemnizacionArt225 = new()
    {
        CausaTerminacion.RenunciaJustaCausa,   // Art. 223
        CausaTerminacion.DespidoInjustificado, // Art. 218
        CausaTerminacion.CausaEconomicaC,      // Art. 213.C + 215
        CausaTerminacion.FuerzaMayorB,         // Art. 213.B.7
        CausaTerminacion.MuerteEmpleador,      // Art. 210.5 — equiparable a B.7
        CausaTerminacion.MutuoAcuerdo,         // Art. 210.1 — indemnización pactada
        CausaTerminacion.ProlongacionSuspension // Art. 210.6 — interpretación pro-trabajador
    };

    // Causas en las que el empleador NO debe preaviso al trabajador.
    private static readonly HashSet<CausaTerminacion> CausasSinPreaviso = new()
    {
        CausaTerminacion.DespidoJustificadoA,      // Art. 213.A
        CausaTerminacion.MuerteTrabajador,         // Art. 210.4
        CausaTerminacion.ExpiracionTerminoPactado, // Art. 210.2
        CausaTerminacion.ConclusionObra,           // Art. 210.3
        CausaTerminacion.RenunciaJustaCausa,       // Art. 223
        CausaTerminacion.RenunciaSimple,           // el trabajador debe el preaviso, no a la inversa
        CausaTerminacion.Jubilacion                // terminación planificada
    };

    // Causas con indemnización Art. 225: el preaviso se ABSORBE en la indemnización
    // (alineado con MITRADEL — evita doble compensación por el mismo período de aviso).
    private static readonly HashSet<CausaTerminacion> CausasPreavisoAbsorbido = new()
    {
        CausaTerminacion.DespidoInjustificado,
        CausaTerminacion.CausaEconomicaC,
        CausaTerminacion.FuerzaMayorB,
        CausaTerminacion.MuerteEmpleador,
        CausaTerminacion.MutuoAcuerdo,
        CausaTerminacion.ProlongacionSuspension
    };

    /// <summary>¿La causa de terminación devenga indemnización por despido (Art. 225)?</summary>
    public static bool PagaIndemnizacionArt225(CausaTerminacion causa)
        => CausasConIndemnizacionArt225.Contains(causa);

    /// <summary>
    /// Prima de antigüedad (Art. 224): una semana de salario por año laborado, sin escalones.
    /// La parte proporcional de un año incompleto se respeta vía decimales en yearsWorked.
    /// </summary>
    public static decimal PrimaAntiguedadWeeks(decimal yearsWorked)
        => yearsWorked <= 0 ? 0 : yearsWorked * PrimaAntiguedadWeeksPerYear;

    /// <summary>
    /// Indemnización por despido injustificado (Art. 225, post-Ley 44/1995):
    /// 3.4 semanas/año los primeros 10 años + 1 semana/año adicional, sin tope.
    /// </summary>
    public static decimal IndemnizacionDespidoWeeks(decimal yearsWorked)
    {
        if (yearsWorked <= 0) return 0;
        var firstPhase = Math.Min(yearsWorked, IndemThresholdYears) * IndemWeeksFirst10Years;
        var secondPhase = Math.Max(0, yearsWorked - IndemThresholdYears) * IndemWeeksAfter10Years;
        return firstPhase + secondPhase;
    }

    /// <summary>
    /// Cesantía Fondo (Decreto Ejecutivo 60/1995 + Art. 229): 6% del salario mensual por
    /// cada mes trabajado. Sustituye la prima Art. 224 en contratos DEFINIDO/POR_OBRA.
    /// </summary>
    public static decimal CalcularCesantia(decimal monthlySalary, decimal yearsWorked, TipoContratoDuracion contrato)
    {
        if (contrato == TipoContratoDuracion.Indefinido) return 0; // usa prima Art. 224
        var monthsWorked = yearsWorked * 12m;
        return RoundingPolicy.Round(monthlySalary * monthsWorked * CesantiaRate);
    }

    /// <summary>
    /// Vacaciones proporcionales del ciclo en curso (Art. 54.1): 1 día por cada 11 días.
    /// Si hay meses vencidos: salario × meses / 11. Si &lt; 1 año: salario × días / 330.
    /// Régimen doméstico no genera proporcional adicional.
    /// </summary>
    public static decimal CalcularVacacionProporcional(
        decimal monthlySalary, decimal monthsOwed, decimal daysWorked, decimal yearsWorked, bool isDomestic)
    {
        if (isDomestic) return 0;
        if (monthsOwed > 0)
            return RoundingPolicy.Round(monthlySalary * monthsOwed / DiasVacacionUnit);
        if (daysWorked > 0 && yearsWorked < 1)
            return RoundingPolicy.Round(monthlySalary * daysWorked / (DiasVacacionUnit * 30m));
        return 0;
    }

    /// <summary>
    /// Dias de vacacion equivalentes al monto proporcional (Art. 54.1: 1 dia por cada 11
    /// trabajados). Refleja exactamente las mismas ramas que CalcularVacacionProporcional,
    /// para que el monto mostrado y los dias mostrados nunca se contradigan.
    /// </summary>
    public static decimal CalcularVacacionProporcionalDias(
        decimal monthsOwed, decimal daysWorked, decimal yearsWorked, bool isDomestic)
    {
        if (isDomestic) return 0;
        if (monthsOwed > 0) return RoundingPolicy.Round(monthsOwed * 30m);
        if (daysWorked > 0 && yearsWorked < 1)
            return RoundingPolicy.Round(daysWorked / DiasVacacionUnit);
        return 0;
    }

    /// <summary>
    /// XIII Mes proporcional al cierre (Art. 142 + Ley 51/2005 Art. 96.4-96.5):
    /// (salario / 24) + (salario × mesesVencidos / 11). Fórmula MITRADEL validada.
    /// </summary>
    public static decimal CalcularDecimoProporcional(decimal monthlySalary, decimal monthsOwed)
    {
        var baseDecimo = monthlySalary / 24m;
        var fromOwed = monthsOwed * monthlySalary / 11m;
        return RoundingPolicy.Round(baseDecimo + fromOwed);
    }

    /// <summary>
    /// Preaviso (Art. 211 régimen general / Art. 233 doméstico). Cuando hay indemnización
    /// Art. 225, el preaviso se absorbe (no se compensa por separado).
    /// </summary>
    public static PreavisoCalcResult CalcularPreaviso(
        decimal yearsWorked, CausaTerminacion causa, bool isDomestic,
        bool preavisoOtorgado, decimal? preavisoDiasOtorgados, decimal monthlySalary)
    {
        if (CausasSinPreaviso.Contains(causa))
            return new PreavisoCalcResult(0, 0, 0, 0,
                $"Sin preaviso debido — causa {causa} no lo devenga");

        // Régimen doméstico (Decreto 39/2014): sin compensación monetaria automática.
        if (isDomestic)
            return new PreavisoCalcResult(0, 0, 0, 0,
                "Régimen doméstico (Art. 233) — sin compensación monetaria automática");

        // Absorbido en la indemnización Art. 225 (alineado MITRADEL).
        if (CausasPreavisoAbsorbido.Contains(causa))
            return new PreavisoCalcResult(0, 0, 0, 0,
                "Preaviso absorbido en indemnización Art. 225 (alineado MITRADEL)");

        // Tabla régimen general (Art. 211).
        decimal diasRequeridos = yearsWorked >= 2m ? 30m : yearsWorked >= 0.5m ? 14m : 7m;

        var diasOtorgadosBase = preavisoOtorgado ? preavisoDiasOtorgados ?? diasRequeridos : 0m;
        var diasOtorgados = Math.Max(0m, Math.Min(diasOtorgadosBase, diasRequeridos));
        var diasDebidos = Math.Max(0m, diasRequeridos - diasOtorgados);

        var dailySalary = monthlySalary / 30m;
        var compensacion = RoundingPolicy.Round(diasDebidos * dailySalary);

        var source = preavisoOtorgado
            ? $"Art. 211 — preaviso otorgado {diasOtorgados}/{diasRequeridos} días"
            : $"Art. 211 — preaviso NO otorgado, se compensan {diasRequeridos} días";

        return new PreavisoCalcResult(diasRequeridos, diasOtorgados, diasDebidos, compensacion, source);
    }

    /// <summary>
    /// Calcula la liquidación completa componiendo todas las partidas según su regla legal.
    /// </summary>
    public static LiquidacionCalcResult Calcular(LiquidacionCalcInput input)
    {
        var weeklyPrima = input.MonthlySalaryForPrima / WeeksPerMonth;
        var weeklyIndem = input.MonthlySalaryForIndemnization / WeeksPerMonth;
        var dailySalary = input.MonthlySalaryForDailyRate / 30m;

        var isIndefinido = input.ContractDurationType == TipoContratoDuracion.Indefinido;

        // 1. Prima de antigüedad (Art. 224) — solo INDEFINIDO con ≥ 1 año (MITRADEL no paga < 1 año).
        var eligibleForPrima = isIndefinido && input.YearsWorked >= 1m;
        var primaWeeks = eligibleForPrima ? PrimaAntiguedadWeeks(input.YearsWorked) : 0m;
        var primaAmount = RoundingPolicy.Round(primaWeeks * weeklyPrima);

        // 2. Indemnización por despido (Art. 225) — solo si la causa lo dispara.
        var indemWeeks = PagaIndemnizacionArt225(input.Causa)
            ? IndemnizacionDespidoWeeks(input.YearsWorked)
            : 0m;
        var indemAmount = RoundingPolicy.Round(indemWeeks * weeklyIndem);

        // 2b. Recargo Art. 219 — solo sobre indemnización Art. 225 existente.
        var recargoPct = Math.Max(0m, Math.Min(1m, input.RecargoArt219Percentage));
        var recargoAmount = indemAmount > 0 ? RoundingPolicy.Round(indemAmount * recargoPct) : 0m;

        // 3. Vacaciones no pagadas (días vencidos × salario diario).
        var vacacionesNoPagadas = RoundingPolicy.Round(input.UnpaidVacationDays * dailySalary);

        // 4. Décimos prorrateados (externo) y 5. otras.
        var decimosProrrateados = RoundingPolicy.Round(input.ProratedDecimos);
        var otras = RoundingPolicy.Round(input.OtrasIndemnizaciones);

        // 5b/5c. Vacaciones y décimo proporcional del ciclo en curso (Art. 54.1 / 142).
        var monthsOwed = input.UnpaidVacationDays / 30m;
        var daysWorked = input.YearsWorked * 365.25m;
        var vacacionProp = CalcularVacacionProporcional(
            input.MonthlySalaryForDailyRate, monthsOwed, daysWorked, input.YearsWorked, input.IsDomesticWorker);
        var vacacionPropDias = CalcularVacacionProporcionalDias(
            monthsOwed, daysWorked, input.YearsWorked, input.IsDomesticWorker);
        var decimoProp = CalcularDecimoProporcional(input.MonthlySalaryForDailyRate, monthsOwed);

        // 5d. Cesantía (Decreto 60/1995) — solo DEFINIDO/POR_OBRA.
        var cesantia = CalcularCesantia(input.MonthlySalaryForDailyRate, input.YearsWorked, input.ContractDurationType);

        // 6. Preaviso (Art. 211 / 233).
        var preaviso = CalcularPreaviso(
            input.YearsWorked, input.Causa, input.IsDomesticWorker,
            input.PreavisoOtorgado, input.PreavisoDiasOtorgados, input.MonthlySalaryForDailyRate);

        var total = RoundingPolicy.Round(
            primaAmount + indemAmount + recargoAmount + vacacionesNoPagadas +
            vacacionProp + decimosProrrateados + decimoProp + cesantia + otras +
            preaviso.CompensacionAmount);

        return new LiquidacionCalcResult
        {
            WeeklySalaryForPrima = RoundingPolicy.Round(weeklyPrima),
            WeeklySalaryForIndemnization = RoundingPolicy.Round(weeklyIndem),
            DailySalary = RoundingPolicy.Round(dailySalary),
            PrimaAntiguedadAmount = primaAmount,
            IndemnizacionWeeks = RoundingPolicy.Round(indemWeeks),
            IndemnizacionDespidoAmount = indemAmount,
            RecargoArt219Percentage = recargoPct,
            RecargoArt219Amount = recargoAmount,
            VacacionesNoPagadasAmount = vacacionesNoPagadas,
            DecimosProrrateadosAmount = decimosProrrateados,
            OtrasIndemnizaciones = otras,
            VacacionesProporcionalAmount = vacacionProp,
            VacacionesProporcionalDias = vacacionPropDias,
            DecimoProporcionalAmount = decimoProp,
            CesantiaAmount = cesantia,
            PreavisoDiasRequeridos = preaviso.DiasRequeridos,
            PreavisoDiasOtorgados = preaviso.DiasOtorgados,
            PreavisoDiasDebidos = preaviso.DiasDebidos,
            PreavisoCompensacionAmount = preaviso.CompensacionAmount,
            PreavisoSource = preaviso.Source,
            TotalAmount = total
        };
    }
}
