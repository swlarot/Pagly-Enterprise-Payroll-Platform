// ====================================================================
// Planilla - AsistenciaCalculationService
// Creado: 2025-12-28
// Descripción: Servicio para cálculo de conceptos de asistencia
// (horas extra, ausencias, vacaciones) integrado con planilla
// ====================================================================

using Microsoft.EntityFrameworkCore;
using Vorluno.Planilla.Application.Interfaces;
using Vorluno.Planilla.Domain.Entities;
using Vorluno.Planilla.Domain.Enums;
using Vorluno.Planilla.Infrastructure.Data;

namespace Vorluno.Planilla.Infrastructure.Services;

/// <summary>
/// Servicio para calcular montos relacionados con asistencia del empleado:
/// horas extra, ausencias y vacaciones.
/// </summary>
public class AsistenciaCalculationService : IAsistenciaCalculationService
{
    private readonly ApplicationDbContext _context;

    public AsistenciaCalculationService(ApplicationDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <summary>
    /// Calcula el salario por hora basado en el salario mensual y horas semanales.
    /// Fórmula: salarioMensual / (horasSemanales * 4.3333)
    /// Usa la constante oficial MITRADEL: 52 semanas / 12 meses = 4.3333 semanas por mes
    /// </summary>
    public decimal CalcularSalarioHora(decimal salarioMensual, int horasSemanales = 48)
    {
        if (salarioMensual <= 0) throw new ArgumentException("Salario mensual debe ser mayor a cero", nameof(salarioMensual));
        if (horasSemanales <= 0) throw new ArgumentException("Horas semanales debe ser mayor a cero", nameof(horasSemanales));

        // Usar constante oficial MITRADEL: 52/12 = 4.3333...
        const decimal weeksPerMonth = 52m / 12m;
        return Math.Round(salarioMensual / (horasSemanales * weeksPerMonth), 2);
    }

    /// <summary>
    /// Calcula el salario diario basado en el salario mensual.
    /// Fórmula: salarioMensual / 30
    /// </summary>
    public decimal CalcularSalarioDiario(decimal salarioMensual)
    {
        if (salarioMensual <= 0) throw new ArgumentException("Salario mensual debe ser mayor a cero", nameof(salarioMensual));

        return Math.Round(salarioMensual / 30m, 2);
    }

    /// <summary>
    /// Obtiene las horas extra aprobadas y no pagadas del período especificado.
    /// </summary>
    public async Task<List<HoraExtra>> GetHorasExtraAprobadas(int empleadoId, DateTime periodoInicio, DateTime periodoFin)
    {
        return await _context.HorasExtra
            .Where(h => h.EmpleadoId == empleadoId
                && h.EstaAprobada
                && h.PlanillaDetailId == null
                && h.Fecha >= periodoInicio
                && h.Fecha <= periodoFin)
            .ToListAsync();
    }

    /// <summary>
    /// Calcula el monto total de horas extra y retorna el desglose por tipo.
    /// </summary>
    /// <returns>Tupla con (montoTotal, horasDiurnas, horasNocturnas, horasDomingoFeriado)</returns>
    public async Task<(decimal montoTotal, decimal horasDiurnas, decimal horasNocturnas, decimal horasDomingoFeriado)>
        CalcularMontoHorasExtra(int empleadoId, decimal salarioHora, DateTime periodoInicio, DateTime periodoFin)
    {
        var horasExtra = await GetHorasExtraAprobadas(empleadoId, periodoInicio, periodoFin);

        decimal montoTotal = 0;
        decimal horasDiurnas = 0, horasNocturnas = 0, horasDomingoFeriado = 0;

        foreach (var hora in horasExtra)
        {
            // Determinar factor multiplicador base según tipo
            decimal factorBase = hora.TipoHoraExtra switch
            {
                TipoHoraExtra.Diurna => 1.25m,
                TipoHoraExtra.Nocturna => 1.50m,
                TipoHoraExtra.DomingoFeriado => 1.50m,
                TipoHoraExtra.NocturnaDomingoFeriado => 2.25m, // 1.50 × 1.50 según Código de Trabajo Art. 50
                TipoHoraExtra.FiestaNacionalDiurna => 3.125m, // 2.50 × 1.25 según Código de Trabajo Art. 49
                TipoHoraExtra.FiestaNacionalNocturna => 3.75m, // 2.50 × 1.50 según Código de Trabajo Art. 49
                TipoHoraExtra.MixtaDiurnaNocturna => 1.50m,
                TipoHoraExtra.MixtaNocturnaDiurna => 1.75m,
                _ => 1.25m
            };

            // Aplicar factor de exceso si aplica
            decimal factorTotal = factorBase;
            if (hora.EsExceso && hora.FactorExceso.HasValue)
            {
                factorTotal *= hora.FactorExceso.Value; // Multiplicar por 1.75x adicional
            }

            // Calcular monto para esta hora extra
            var monto = salarioHora * hora.CantidadHoras * factorTotal;
            montoTotal += monto;

            // Actualizar monto calculado en la entidad si no está calculado o si cambió
            if (!hora.MontoCalculado.HasValue || hora.MontoCalculado.Value != monto)
            {
                hora.MontoCalculado = monto;
            }

            // Acumular horas por tipo para desglose
            if (hora.TipoHoraExtra == TipoHoraExtra.Diurna || hora.TipoHoraExtra == TipoHoraExtra.MixtaDiurnaNocturna)
                horasDiurnas += hora.CantidadHoras;
            else if (hora.TipoHoraExtra == TipoHoraExtra.Nocturna || hora.TipoHoraExtra == TipoHoraExtra.MixtaNocturnaDiurna)
                horasNocturnas += hora.CantidadHoras;
            else
                horasDomingoFeriado += hora.CantidadHoras; // Incluye DomingoFeriado, NocturnaDomingoFeriado, FiestaNacionalDiurna, FiestaNacionalNocturna
        }

        return (Math.Round(montoTotal, 2), horasDiurnas, horasNocturnas, horasDomingoFeriado);
    }

    /// <summary>
    /// Obtiene las ausencias del período que afectan el salario y no han sido procesadas.
    /// </summary>
    public async Task<List<Ausencia>> GetAusenciasDelPeriodo(int empleadoId, DateTime periodoInicio, DateTime periodoFin)
    {
        return await _context.Ausencias
            .Where(a => a.EmpleadoId == empleadoId
                && a.AfectaSalario
                && a.PlanillaDetailId == null
                && a.FechaInicio <= periodoFin
                && a.FechaFin >= periodoInicio)
            .ToListAsync();
    }

    /// <summary>
    /// Calcula el descuento por ausencias del período.
    /// </summary>
    /// <returns>Tupla con (descuento, diasAusencia)</returns>
    public async Task<(decimal descuento, decimal diasAusencia)>
        CalcularDescuentoAusencias(int empleadoId, decimal salarioDiario, DateTime periodoInicio, DateTime periodoFin)
    {
        var ausencias = await GetAusenciasDelPeriodo(empleadoId, periodoInicio, periodoFin);

        decimal totalDias = 0;

        foreach (var ausencia in ausencias)
        {
            // Solo contar días dentro del período de la planilla
            var inicio = ausencia.FechaInicio < periodoInicio ? periodoInicio : ausencia.FechaInicio;
            var fin = ausencia.FechaFin > periodoFin ? periodoFin : ausencia.FechaFin;
            var dias = (decimal)(fin - inicio).TotalDays + 1;

            totalDias += dias;

            // Actualizar monto descontado en la entidad
            ausencia.MontoDescontado = dias * salarioDiario;
        }

        return (Math.Round(totalDias * salarioDiario, 2), totalDias);
    }

    /// <summary>
    /// Obtiene las solicitudes de vacaciones aprobadas del período que no han sido pagadas.
    /// </summary>
    public async Task<List<SolicitudVacaciones>> GetVacacionesDelPeriodo(int empleadoId, DateTime periodoInicio, DateTime periodoFin)
    {
        return await _context.SolicitudesVacaciones
            .Where(v => v.EmpleadoId == empleadoId
                && v.Estado == EstadoVacaciones.Aprobada
                && v.PlanillaDetailId == null
                && v.FechaInicio <= periodoFin
                && v.FechaFin >= periodoInicio)
            .ToListAsync();
    }

    /// <summary>
    /// Calcula el pago por vacaciones del período.
    /// En Panamá, las vacaciones se pagan normalmente (no son adicionales al salario durante el período).
    /// </summary>
    public async Task<(decimal montoVacaciones, decimal diasVacaciones)>
        CalcularVacaciones(int empleadoId, decimal salarioDiario, DateTime periodoInicio, DateTime periodoFin)
    {
        var vacaciones = await GetVacacionesDelPeriodo(empleadoId, periodoInicio, periodoFin);

        decimal totalDias = 0;

        foreach (var vac in vacaciones)
        {
            // Solo contar días dentro del período
            var inicio = vac.FechaInicio < periodoInicio ? periodoInicio : vac.FechaInicio;
            var fin = vac.FechaFin > periodoFin ? periodoFin : vac.FechaFin;
            var dias = (decimal)(fin - inicio).TotalDays + 1;

            totalDias += dias;
        }

        // Las vacaciones normalmente ya están incluidas en el salario base
        // Este método es para tracking, el monto es informativo
        return (Math.Round(totalDias * salarioDiario, 2), totalDias);
    }

    /// <summary>
    /// Marca las horas extra como pagadas vinculándolas al detalle de planilla.
    /// </summary>
    public async Task MarcarHorasExtraPagadas(List<HoraExtra> horasExtra, int planillaDetailId)
    {
        foreach (var hora in horasExtra)
        {
            hora.PlanillaDetailId = planillaDetailId;
        }
        // El context tracking ya tiene las entidades, solo se necesita SaveChanges en el caller
    }

    /// <summary>
    /// Marca las ausencias como procesadas vinculándolas al detalle de planilla.
    /// </summary>
    public async Task MarcarAusenciasProcesadas(List<Ausencia> ausencias, int planillaDetailId)
    {
        foreach (var ausencia in ausencias)
        {
            ausencia.PlanillaDetailId = planillaDetailId;
        }
    }

    /// <summary>
    /// Marca las vacaciones como pagadas/completadas vinculándolas al detalle de planilla.
    /// </summary>
    public async Task MarcarVacacionesPagadas(List<SolicitudVacaciones> vacaciones, int planillaDetailId)
    {
        foreach (var vac in vacaciones)
        {
            vac.PlanillaDetailId = planillaDetailId;
            vac.Estado = EstadoVacaciones.Completada;
        }
    }
}
