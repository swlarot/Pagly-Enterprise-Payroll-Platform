// ====================================================================
// Planilla - OvertimeFactorService
// Creado: 2026-02-10
// Actualizado: 2026-06-18 — Regla de las 3 horas nocturnas (Art. 30) y
//   catálogo completo de 12 tipos. La clasificación por horario y los factores
//   viven en OvertimeClassifier (Application, puro/testeable); este servicio
//   añade el contexto de feriado/domingo y la validación de límites con datos.
// Descripción: Servicio para determinar tipo de hora extra y calcular factores
// según Código de Trabajo de Panamá (Arts. 30, 33, 48-50)
// ====================================================================

using Microsoft.EntityFrameworkCore;
using Vorluno.Planilla.Application.Interfaces;
using Vorluno.Planilla.Application.Services;
using Vorluno.Planilla.Domain.Enums;
using Vorluno.Planilla.Infrastructure.Data;

namespace Vorluno.Planilla.Infrastructure.Services;

/// <summary>
/// Servicio para determinar el tipo de hora extra según fecha y horario,
/// calcular factores multiplicadores y validar límites legales.
/// </summary>
public class OvertimeFactorService : IOvertimeFactorService
{
    private readonly ApplicationDbContext _context;
    private readonly PanamaHolidayService _holidayService;

    public OvertimeFactorService(ApplicationDbContext context, PanamaHolidayService holidayService)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _holidayService = holidayService ?? throw new ArgumentNullException(nameof(holidayService));
    }

    /// <summary>
    /// Determina el tipo de hora extra según fecha, horario y contexto (feriado/domingo).
    /// </summary>
    public TipoHoraExtra DetermineOvertimeType(DateTime fecha, TimeSpan horaInicio, TimeSpan horaFin)
    {
        bool esDomingo = fecha.DayOfWeek == DayOfWeek.Sunday;
        bool esFeriado = _holidayService.IsNationalHoliday(fecha);
        bool esNocturna = OvertimeClassifier.EsNocturnaPorHorario(horaInicio, horaFin);

        // Prioridad 1: feriado / duelo nacional (Arts. 49-50)
        if (esFeriado)
            return esNocturna ? TipoHoraExtra.FiestaNacionalNocturna : TipoHoraExtra.FiestaNacionalDiurna;

        // Prioridad 2: domingo (Arts. 48, 50)
        if (esDomingo)
            return esNocturna ? TipoHoraExtra.NocturnaDomingoFeriado : TipoHoraExtra.DomingoFeriado;

        // Prioridad 3: día regular — clasificación por horario con la regla de 3h (Art. 30).
        return OvertimeClassifier.ClasificarJornadaRegular(horaInicio, horaFin);
    }

    /// <summary>Factor multiplicador base según el tipo de hora extra.</summary>
    public decimal CalculateBaseFactor(TipoHoraExtra tipo) => OvertimeClassifier.FactorBase(tipo);

    /// <summary>Factor multiplicador completo, incluyendo recargo por exceso (Art. 36.4).</summary>
    public decimal CalculateFactor(TipoHoraExtra tipo, bool esExceso)
    {
        decimal factorBase = OvertimeClassifier.FactorBase(tipo);
        return esExceso ? factorBase * 1.75m : factorBase;
    }

    /// <summary>
    /// Valida si las horas extra exceden los límites legales (>3h/día o >9h/semana).
    /// </summary>
    public async Task<(bool esExceso, string mensaje)> ValidateOvertimeLimits(
        int empleadoId,
        DateTime fecha,
        decimal horasNuevas)
    {
        // Calcular inicio y fin de semana (lunes a domingo)
        int diasHastaLunes = ((int)DayOfWeek.Monday - (int)fecha.DayOfWeek + 7) % 7;
        DateTime inicioSemana = fecha.AddDays(-diasHastaLunes).Date;
        DateTime finSemana = inicioSemana.AddDays(6).Date.AddDays(1).AddTicks(-1);

        var horasDelDia = await _context.HorasExtra
            .Where(h => h.EmpleadoId == empleadoId
                && h.Fecha.Date == fecha.Date
                && h.PlanillaDetailId == null)
            .SumAsync(h => h.CantidadHoras);

        var horasDeLaSemana = await _context.HorasExtra
            .Where(h => h.EmpleadoId == empleadoId
                && h.Fecha >= inicioSemana
                && h.Fecha <= finSemana
                && h.PlanillaDetailId == null)
            .SumAsync(h => h.CantidadHoras);

        decimal totalDelDia = horasDelDia + horasNuevas;
        decimal totalDeLaSemana = horasDeLaSemana + horasNuevas;

        bool excedeDia = totalDelDia > 3m;
        bool excedeSemana = totalDeLaSemana > 9m;

        if (excedeDia && excedeSemana)
        {
            return (true, $"Excede límites legales: {totalDelDia:F2}h/día (máx 3h) y {totalDeLaSemana:F2}h/semana (máx 9h). Se aplicará recargo por exceso.");
        }
        else if (excedeDia)
        {
            return (true, $"Excede límite diario: {totalDelDia:F2}h/día (máx 3h). Se aplicará recargo por exceso.");
        }
        else if (excedeSemana)
        {
            return (true, $"Excede límite semanal: {totalDeLaSemana:F2}h/semana (máx 9h). Se aplicará recargo por exceso.");
        }

        return (false, string.Empty);
    }
}
