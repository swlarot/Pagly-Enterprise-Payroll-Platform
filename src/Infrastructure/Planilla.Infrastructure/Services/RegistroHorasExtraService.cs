// ====================================================================
// Arma una hora extra con sus cuentas (horas, exceso legal, factor y
// monto) igual para todos: la que registra una persona y la que propone
// el reloj de asistencia. No guarda: quien llama decide cuándo.
// ====================================================================

using Vorluno.Planilla.Application.Interfaces;
using Vorluno.Planilla.Domain.Entities;
using Vorluno.Planilla.Domain.Enums;

namespace Vorluno.Planilla.Infrastructure.Services;

public class RegistroHorasExtraService
{
    private readonly IOvertimeFactorService _factores;

    public RegistroHorasExtraService(IOvertimeFactorService factores) => _factores = factores;

    public async Task<HoraExtra> ConstruirAsync(
        Empleado empleado, int tenantId, DateTime fecha, TipoHoraExtra tipo,
        TimeSpan inicio, TimeSpan fin, string motivo, string? origen = null)
    {
        var cantidadHoras = CalcularHoras(inicio, fin);

        // Límites legales (Código de Trabajo Art. 48: 3 h al día, 9 a la semana).
        var (esExceso, mensajeValidacion) = await _factores.ValidateOvertimeLimits(empleado.Id, fecha, cantidadHoras);

        var factorBase = await _factores.CalculateBaseFactorAsync(tipo);
        var factorTotal = await _factores.CalculateFactorAsync(tipo, esExceso);
        decimal? factorExceso = esExceso ? await _factores.GetFactorExcesoAsync() : null;

        return new HoraExtra
        {
            TenantId = tenantId,
            EmpleadoId = empleado.Id,
            Fecha = fecha.Date,
            TipoHoraExtra = tipo,
            HoraInicio = inicio,
            HoraFin = fin,
            CantidadHoras = cantidadHoras,
            FactorMultiplicador = factorTotal, // Factor total (base × exceso si aplica)
            EsExceso = esExceso,
            FactorExceso = factorExceso,
            MontoCalculado = CalcularMonto(empleado, cantidadHoras, factorBase, factorExceso),
            Motivo = motivo,
            EstaAprobada = false,
            Origen = origen,
            Observaciones = string.IsNullOrEmpty(mensajeValidacion) ? null : mensajeValidacion,
            CreatedAt = DateTime.UtcNow,
        };
    }

    public static decimal CalcularHoras(TimeSpan inicio, TimeSpan fin)
    {
        var diferencia = fin - inicio;
        if (diferencia.TotalHours < 0)
            diferencia = diferencia.Add(TimeSpan.FromHours(24));
        return (decimal)diferencia.TotalHours;
    }

    /// <summary>
    /// Monto con el HourlyRate del empleado; si es 0, con su salario mensual
    /// (SalarioMensual / (HoursPerWeek × 4.333)). Considera el factor de exceso.
    /// </summary>
    public static decimal CalcularMonto(Empleado empleado, decimal cantidadHoras, decimal factorMultiplicador, decimal? factorExceso = null)
    {
        var hourlyRate = empleado.HourlyRate > 0
            ? empleado.HourlyRate
            : Empleado.ComputeHourlyRateFromMonthly(empleado.SalarioBase, empleado.HoursPerWeek);

        var factorTotal = factorMultiplicador;
        if (factorExceso.HasValue && factorExceso.Value > 0)
            factorTotal *= factorExceso.Value;

        return Math.Round(hourlyRate * cantidadHoras * factorTotal, 2);
    }
}
