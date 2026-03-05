// ====================================================================
// Planilla - OvertimeFactorService
// Creado: 2026-02-10
// Descripción: Servicio para determinar tipo de hora extra y calcular factores
// según Código de Trabajo de Panamá (Arts. 33, 48-49)
// ====================================================================

using Microsoft.EntityFrameworkCore;
using Vorluno.Planilla.Application.Interfaces;
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
    /// Determina el tipo de hora extra según fecha, horario y contexto
    /// </summary>
    public TipoHoraExtra DetermineOvertimeType(DateTime fecha, TimeSpan horaInicio, TimeSpan horaFin)
    {
        bool esDomingo = fecha.DayOfWeek == DayOfWeek.Sunday;
        bool esFeriado = _holidayService.IsNationalHoliday(fecha);
        bool esNocturna = IsNocturna(horaInicio, horaFin);
        bool esMixta = IsMixta(horaInicio, horaFin);

        // Prioridad 1: Fiesta nacional
        if (esFeriado)
        {
            if (esNocturna)
                return TipoHoraExtra.FiestaNacionalNocturna;
            return TipoHoraExtra.FiestaNacionalDiurna;
        }

        // Prioridad 2: Domingo
        if (esDomingo)
        {
            if (esNocturna)
                return TipoHoraExtra.NocturnaDomingoFeriado;
            return TipoHoraExtra.DomingoFeriado;
        }

        // Prioridad 3: Mixta
        if (esMixta)
        {
            // Determinar si inicia en horario diurno o nocturno
            bool iniciaDiurno = horaInicio >= TimeSpan.FromHours(6) && horaInicio < TimeSpan.FromHours(18);
            if (iniciaDiurno)
                return TipoHoraExtra.MixtaDiurnaNocturna;
            return TipoHoraExtra.MixtaNocturnaDiurna;
        }

        // Prioridad 4: Simple (diurna o nocturna)
        if (esNocturna)
            return TipoHoraExtra.Nocturna;

        return TipoHoraExtra.Diurna;
    }

    /// <summary>
    /// Calcula el factor multiplicador base según el tipo de hora extra
    /// </summary>
    public decimal CalculateBaseFactor(TipoHoraExtra tipo)
    {
        return tipo switch
        {
            TipoHoraExtra.Diurna => 1.25m,
            TipoHoraExtra.Nocturna => 1.50m,
            TipoHoraExtra.DomingoFeriado => 1.50m,
            TipoHoraExtra.NocturnaDomingoFeriado => 2.25m, // 1.50 × 1.50
            TipoHoraExtra.FiestaNacionalDiurna => 3.125m, // 2.50 × 1.25
            TipoHoraExtra.FiestaNacionalNocturna => 3.75m, // 2.50 × 1.50
            TipoHoraExtra.MixtaDiurnaNocturna => 1.50m,
            TipoHoraExtra.MixtaNocturnaDiurna => 1.75m,
            _ => 1.25m
        };
    }

    /// <summary>
    /// Calcula el factor multiplicador completo incluyendo exceso si aplica
    /// </summary>
    public decimal CalculateFactor(TipoHoraExtra tipo, bool esExceso)
    {
        decimal factorBase = CalculateBaseFactor(tipo);
        
        if (esExceso)
        {
            // Factor de exceso: 1.75x adicional según Art. 48
            return factorBase * 1.75m;
        }

        return factorBase;
    }

    /// <summary>
    /// Valida si las horas extra exceden los límites legales (>3h/día o >9h/semana)
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

        // Obtener horas extra del día
        var horasDelDia = await _context.HorasExtra
            .Where(h => h.EmpleadoId == empleadoId 
                && h.Fecha.Date == fecha.Date
                && h.PlanillaDetailId == null) // Solo horas no pagadas
            .SumAsync(h => h.CantidadHoras);

        // Obtener horas extra de la semana
        var horasDeLaSemana = await _context.HorasExtra
            .Where(h => h.EmpleadoId == empleadoId
                && h.Fecha >= inicioSemana
                && h.Fecha <= finSemana
                && h.PlanillaDetailId == null) // Solo horas no pagadas
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

    /// <summary>
    /// Determina si el horario es nocturno (6pm-6am)
    /// </summary>
    private bool IsNocturna(TimeSpan horaInicio, TimeSpan horaFin)
    {
        // Si cruza medianoche
        if (horaFin < horaInicio)
        {
            // Es nocturna si inicia después de las 6pm o termina antes de las 6am
            return horaInicio >= TimeSpan.FromHours(18) || horaFin <= TimeSpan.FromHours(6);
        }

        // Si no cruza medianoche, es nocturna si está completamente dentro del rango 6pm-6am
        return horaInicio >= TimeSpan.FromHours(18) || horaFin <= TimeSpan.FromHours(6);
    }

    /// <summary>
    /// Determina si el horario es mixto (cruza el límite 6pm-6am)
    /// </summary>
    private bool IsMixta(TimeSpan horaInicio, TimeSpan horaFin)
    {
        // Si cruza medianoche
        if (horaFin < horaInicio)
        {
            // Es mixta si cruza el límite 6am o 6pm
            return (horaInicio >= TimeSpan.FromHours(18) && horaFin <= TimeSpan.FromHours(6)) ||
                   (horaInicio < TimeSpan.FromHours(6) && horaFin > TimeSpan.FromHours(18));
        }

        // Si no cruza medianoche, es mixta si cruza el límite 6pm o 6am
        return (horaInicio < TimeSpan.FromHours(18) && horaFin > TimeSpan.FromHours(18)) ||
               (horaInicio < TimeSpan.FromHours(6) && horaFin > TimeSpan.FromHours(6));
    }
}
