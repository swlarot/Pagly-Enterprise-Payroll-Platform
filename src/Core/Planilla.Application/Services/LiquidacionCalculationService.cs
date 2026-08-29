using Vorluno.Planilla.Application.DTOs;
using Vorluno.Planilla.Application.Helpers;
using Vorluno.Planilla.Domain.Entities;
using Vorluno.Planilla.Domain.Enums;

namespace Vorluno.Planilla.Application.Services;

/// <summary>
/// Resultado del cálculo de liquidación laboral.
/// </summary>
public class LiquidacionCalculationResult
{
    public decimal AnosServicio { get; set; }
    public decimal SalarioDiario { get; set; }
    public decimal SalarioSemanal { get; set; }

    // Componentes
    public decimal PrimaAntiguedad { get; set; }
    public decimal Indemnizacion { get; set; }
    public decimal IndemnizacionSemanas { get; set; }
    public decimal RecargoArt219 { get; set; }
    public decimal Preaviso { get; set; }
    public decimal VacacionesProporcionales { get; set; }
    public decimal DiasVacacionesProporcionales { get; set; }
    public decimal DecimoTercerMesProporcional { get; set; }
    public decimal Cesantia { get; set; }
    public decimal SalarioPendiente { get; set; }
    public decimal DiasSalarioPendiente { get; set; }

    // Deducciones empleado
    public decimal CssEmpleado { get; set; }
    public decimal SeEmpleado { get; set; }
    public decimal Isr { get; set; }

    // Costos patronales
    public decimal CssPatronal { get; set; }
    public decimal SePatronal { get; set; }

    // Totales
    public decimal TotalBruto { get; set; }
    public decimal TotalDeducciones { get; set; }
    public decimal TotalNeto { get; set; }
}

/// <summary>
/// Servicio de cálculo de liquidaciones laborales según el Código de Trabajo de Panamá.
/// Compone las calculadoras puras de <see cref="LiquidacionCalculator"/> (port de Talento,
/// validado contra MITRADEL) y agrega salario pendiente y deducciones CSS/SE.
/// </summary>
public class LiquidacionCalculationService
{
    /// <summary>
    /// Calcula todos los componentes de una liquidación laboral.
    /// </summary>
    /// <param name="empleado">Empleado a liquidar</param>
    /// <param name="request">Datos de la solicitud de liquidación</param>
    /// <param name="ultimaFechaVacaciones">Obsoleto — los días vencidos se toman de request.DiasVacacionesPendientes.</param>
    public LiquidacionCalculationResult Calcular(
        Empleado empleado,
        CreateLiquidacionRequest request,
        DateTime? ultimaFechaVacaciones = null)
    {
        var salarioMensual = empleado.SalarioBase;
        var salarioDiario = salarioMensual / 30m;

        var anosServicio = CalcularAnosServicio(empleado.FechaContratacion, request.FechaTerminacion);
        var diasVacVencidas = request.DiasVacacionesPendientes is > 0 ? request.DiasVacacionesPendientes!.Value : 0m;

        // ====================================================================
        // Núcleo legal: calculadoras puras (Código de Trabajo Panamá).
        //
        // Bases salariales (B5): la prima usa el promedio de 5 años (Art. 226) y la
        // indemnización el promedio 6m/30d (Art. 149), calculados desde el historial
        // salarial. Si el empleado no tiene historial, cae al salario actual (fallback)
        // Tipo de contrato (B4): asume Indefinido (prima Art. 224) hasta que el dominio
        // exponga el tipo — DEFINIDO/POR_OBRA usaría cesantía (Decreto 60/1995).
        // ====================================================================
        var calc = LiquidacionCalculator.Calcular(new LiquidacionCalcInput
        {
            MonthlySalaryForPrima = SalaryHistoryAverager.AverageForPrima(empleado.HistorialSalarial, request.FechaTerminacion, salarioMensual),
            MonthlySalaryForIndemnization = SalaryHistoryAverager.AverageForIndemnization(empleado.HistorialSalarial, request.FechaTerminacion, salarioMensual),
            MonthlySalaryForDailyRate = salarioMensual,
            YearsWorked = anosServicio,
            Causa = request.TipoTerminacion.ToCausaTerminacion(),
            UnpaidVacationDays = diasVacVencidas,
            // request.IncluyePreaviso indica que el preaviso fue otorgado (no se compensa).
            PreavisoOtorgado = request.IncluyePreaviso,
            ContractDurationType = empleado.TipoContrato
        });

        // Salario pendiente (días trabajados no pagados) — propio de Pagly, fuera de las calculadoras.
        var diasPend = request.DiasSalarioPendiente is > 0 ? request.DiasSalarioPendiente!.Value : 0m;
        var montoPend = RoundingPolicy.Round(diasPend * salarioDiario);

        // ====================================================================
        // Deducciones CSS/SE: aplican SOLO sobre vacaciones y décimo proporcional.
        // Prima, indemnización, recargo, cesantía y salario pendiente NO cotizan
        // (son indemnizatorios). El décimo usa CSS reducida (Art. 96.4-96.5).
        // ====================================================================
        var baseVacaciones = calc.VacacionesNoPagadasAmount + calc.VacacionesProporcionalAmount;
        var baseDecimo = calc.DecimoProporcionalAmount;

        var cssEmpleado = RoundingPolicy.Round(
            baseVacaciones * PayrollConstants.CssTasaEmpleado +
            baseDecimo * PayrollConstants.CssTasaDecimoEmpleado);
        var seEmpleado = RoundingPolicy.Round(
            (baseVacaciones + baseDecimo) * PayrollConstants.SeTasaEmpleado);

        var cssPatronal = RoundingPolicy.Round(
            baseVacaciones * PayrollConstants.CssTasaPatronal +
            baseDecimo * PayrollConstants.CssTasaDecimoPatronal);
        var sePatronal = RoundingPolicy.Round(
            (baseVacaciones + baseDecimo) * PayrollConstants.SeTasaPatronal);

        // ISR: por norma general no se retiene sobre prestaciones de liquidación.
        var isr = 0m;

        var totalBruto = RoundingPolicy.Round(calc.TotalAmount + montoPend);
        var totalDeducciones = RoundingPolicy.Round(cssEmpleado + seEmpleado + isr);
        var totalNeto = RoundingPolicy.Round(totalBruto - totalDeducciones);

        return new LiquidacionCalculationResult
        {
            AnosServicio = anosServicio,
            SalarioDiario = RoundingPolicy.Round(salarioDiario),
            SalarioSemanal = calc.WeeklySalaryForPrima,

            PrimaAntiguedad = calc.PrimaAntiguedadAmount,
            Indemnizacion = calc.IndemnizacionDespidoAmount,
            IndemnizacionSemanas = calc.IndemnizacionWeeks,
            RecargoArt219 = calc.RecargoArt219Amount,
            Preaviso = calc.PreavisoCompensacionAmount,
            VacacionesProporcionales = RoundingPolicy.Round(baseVacaciones),
            // Antes se mostraban solo los dias vencidos que el usuario escribia a mano,
            // mientras el monto sumaba vencidas + proporcionales. Con 7 meses de servicio
            // la UI decia "0.00 dias" junto a un pago de B/. 447.59.
            DiasVacacionesProporcionales = diasVacVencidas > 0
                ? diasVacVencidas
                : calc.VacacionesProporcionalDias,
            DecimoTercerMesProporcional = calc.DecimoProporcionalAmount,
            Cesantia = calc.CesantiaAmount,
            SalarioPendiente = montoPend,
            DiasSalarioPendiente = diasPend,

            CssEmpleado = cssEmpleado,
            SeEmpleado = seEmpleado,
            Isr = isr,
            CssPatronal = cssPatronal,
            SePatronal = sePatronal,

            TotalBruto = totalBruto,
            TotalDeducciones = totalDeducciones,
            TotalNeto = totalNeto
        };
    }

    /// <summary>
    /// Calcula los años de servicio como decimal. Ej: 2 años 6 meses = 2.5
    /// </summary>
    private static decimal CalcularAnosServicio(DateTime fechaContratacion, DateTime fechaTerminacion)
    {
        var totalDias = (fechaTerminacion - fechaContratacion).TotalDays;
        if (totalDias < 0) totalDias = 0;
        return Math.Round((decimal)totalDias / 365.25m, 4);
    }
}
