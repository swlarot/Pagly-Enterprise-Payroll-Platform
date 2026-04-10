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
    public decimal Indemnizacion { get; set; }
    public decimal Preaviso { get; set; }
    public decimal VacacionesProporcionales { get; set; }
    public decimal DiasVacacionesProporcionales { get; set; }
    public decimal DecimoTercerMesProporcional { get; set; }
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
/// </summary>
public class LiquidacionCalculationService
{
    /// <summary>
    /// Calcula todos los componentes de una liquidación laboral.
    /// </summary>
    /// <param name="empleado">Empleado a liquidar</param>
    /// <param name="request">Datos de la solicitud de liquidación</param>
    /// <param name="ultimaFechaVacaciones">Fecha hasta la cual el empleado tiene vacaciones pagadas (null = desde contratación)</param>
    public LiquidacionCalculationResult Calcular(
        Empleado empleado,
        CreateLiquidacionRequest request,
        DateTime? ultimaFechaVacaciones = null)
    {
        var result = new LiquidacionCalculationResult();

        var salarioMensual = empleado.SalarioBase;
        var salarioDiario = salarioMensual / 30m;
        var salarioSemanal = salarioMensual * 12m / 52m;

        result.SalarioDiario = Math.Round(salarioDiario, 2);
        result.SalarioSemanal = Math.Round(salarioSemanal, 2);

        // Años de servicio (decimal)
        var anosServicio = CalcularAnosServicio(empleado.FechaContratacion, request.FechaTerminacion);
        result.AnosServicio = anosServicio;

        // 1. Indemnización (Art. 225) — SOLO despido injustificado
        result.Indemnizacion = CalcularIndemnizacion(request.TipoTerminacion, salarioSemanal, anosServicio);

        // 2. Preaviso (Art. 212)
        result.Preaviso = request.IncluyePreaviso ? CalcularPreaviso(salarioDiario) : 0m;

        // 3. Vacaciones proporcionales (Art. 54)
        var baseVacaciones = ultimaFechaVacaciones ?? empleado.FechaContratacion;
        var (diasVac, montoVac) = CalcularVacacionesProporcionales(
            baseVacaciones,
            request.FechaTerminacion,
            salarioDiario,
            request.DiasVacacionesPendientes);
        result.DiasVacacionesProporcionales = diasVac;
        result.VacacionesProporcionales = montoVac;

        // 4. Décimo Tercer Mes Proporcional
        result.DecimoTercerMesProporcional = CalcularDecimoTercerMesProporcional(
            salarioMensual, request.FechaTerminacion);

        // 5. Salario pendiente
        var (diasPend, montoPend) = CalcularSalarioPendiente(
            salarioDiario, request.DiasSalarioPendiente);
        result.DiasSalarioPendiente = diasPend;
        result.SalarioPendiente = montoPend;

        // ====================================================================
        // Deducciones: CSS y SE SOLO aplican sobre vacaciones y décimo proporcional
        // Indemnización, preaviso y salario pendiente están exentos.
        // ====================================================================

        var baseSujetaCss = result.VacacionesProporcionales;
        var baseSujetaDecimoCss = result.DecimoTercerMesProporcional;

        // CSS empleado: tasa regular sobre vacaciones + tasa décimo sobre décimo proporcional
        var cssVacaciones = baseSujetaCss * PayrollConstants.CssTasaEmpleado;
        var cssDecimo = baseSujetaDecimoCss * PayrollConstants.CssTasaDecimoEmpleado;
        result.CssEmpleado = Math.Round(cssVacaciones + cssDecimo, 2);

        // SE empleado: tasa regular sobre vacaciones + décimo proporcional
        var seVacaciones = baseSujetaCss * PayrollConstants.SeTasaEmpleado;
        var seDecimo = baseSujetaDecimoCss * PayrollConstants.SeTasaEmpleado;
        result.SeEmpleado = Math.Round(seVacaciones + seDecimo, 2);

        // ISR: generalmente no aplica en liquidación, se deja en 0
        result.Isr = 0m;

        // Costos patronales
        var cssPatronalVacaciones = baseSujetaCss * PayrollConstants.CssTasaPatronal;
        var cssPatronalDecimo = baseSujetaDecimoCss * PayrollConstants.CssTasaDecimoPatronal;
        result.CssPatronal = Math.Round(cssPatronalVacaciones + cssPatronalDecimo, 2);

        var sePatronalVacaciones = baseSujetaCss * PayrollConstants.SeTasaPatronal;
        var sePatronalDecimo = baseSujetaDecimoCss * PayrollConstants.SeTasaPatronal;
        result.SePatronal = Math.Round(sePatronalVacaciones + sePatronalDecimo, 2);

        // Totales
        result.TotalBruto = Math.Round(
            result.Indemnizacion +
            result.Preaviso +
            result.VacacionesProporcionales +
            result.DecimoTercerMesProporcional +
            result.SalarioPendiente, 2);

        result.TotalDeducciones = Math.Round(
            result.CssEmpleado +
            result.SeEmpleado +
            result.Isr, 2);

        result.TotalNeto = Math.Round(result.TotalBruto - result.TotalDeducciones, 2);

        return result;
    }

    /// <summary>
    /// Calcula los años de servicio como decimal.
    /// Ej: 2 años 6 meses = 2.5
    /// </summary>
    private static decimal CalcularAnosServicio(DateTime fechaContratacion, DateTime fechaTerminacion)
    {
        var totalDias = (fechaTerminacion - fechaContratacion).TotalDays;
        if (totalDias < 0) totalDias = 0;
        return Math.Round((decimal)totalDias / 365.25m, 4);
    }

    /// <summary>
    /// Indemnización (Art. 225): 3.4 semanas de salario por año de servicio.
    /// Solo aplica para despido injustificado.
    /// </summary>
    private static decimal CalcularIndemnizacion(TipoTerminacion tipo, decimal salarioSemanal, decimal anosServicio)
    {
        if (!tipo.IncluyeIndemnizacion())
            return 0m;

        // 3.4 semanas por año de servicio
        return Math.Round(salarioSemanal * 3.4m * anosServicio, 2);
    }

    /// <summary>
    /// Preaviso (Art. 212): 30 días de salario.
    /// </summary>
    private static decimal CalcularPreaviso(decimal salarioDiario)
    {
        return Math.Round(salarioDiario * 30m, 2);
    }

    /// <summary>
    /// Vacaciones proporcionales (Art. 54): 1 día por cada 11.5 días trabajados.
    /// Si se proporcionan días pendientes manualmente, se usan esos.
    /// </summary>
    private static (decimal dias, decimal monto) CalcularVacacionesProporcionales(
        DateTime desdeUltimaVacacion,
        DateTime fechaTerminacion,
        decimal salarioDiario,
        decimal? diasManual)
    {
        decimal dias;
        if (diasManual.HasValue && diasManual.Value > 0)
        {
            dias = diasManual.Value;
        }
        else
        {
            var diasTrabajados = (decimal)(fechaTerminacion - desdeUltimaVacacion).TotalDays;
            if (diasTrabajados < 0) diasTrabajados = 0;
            dias = Math.Round(diasTrabajados / 11.5m, 2);
        }

        var monto = Math.Round(dias * salarioDiario, 2);
        return (dias, monto);
    }

    /// <summary>
    /// Décimo Tercer Mes Proporcional: 1/12 del total devengado en el tercio en curso.
    /// Tercios: Ene-Abr, May-Ago, Sep-Dic.
    /// Calcula los meses proporcionales dentro del tercio actual.
    /// </summary>
    private static decimal CalcularDecimoTercerMesProporcional(decimal salarioMensual, DateTime fechaTerminacion)
    {
        // Determinar inicio del tercio actual
        var mes = fechaTerminacion.Month;
        int mesInicioTercio;
        if (mes >= 1 && mes <= 4) mesInicioTercio = 1;       // Ene-Abr
        else if (mes >= 5 && mes <= 8) mesInicioTercio = 5;   // May-Ago
        else mesInicioTercio = 9;                              // Sep-Dic

        var inicioTercio = new DateTime(fechaTerminacion.Year, mesInicioTercio, 1);

        // Meses trabajados en el tercio (proporcional)
        var diasEnTercio = (decimal)(fechaTerminacion - inicioTercio).TotalDays;
        if (diasEnTercio < 0) diasEnTercio = 0;
        var mesesEnTercio = diasEnTercio / 30m;

        // Total devengado en el tercio (aproximado)
        var totalDevengado = salarioMensual * mesesEnTercio;

        // Décimo = 1/12 del total devengado
        return Math.Round(totalDevengado / 12m, 2);
    }

    /// <summary>
    /// Salario pendiente: días trabajados en el período final no pagado.
    /// </summary>
    private static (decimal dias, decimal monto) CalcularSalarioPendiente(
        decimal salarioDiario,
        decimal? diasManual)
    {
        var dias = diasManual ?? 0m;
        if (dias < 0) dias = 0;
        var monto = Math.Round(dias * salarioDiario, 2);
        return (dias, monto);
    }
}
