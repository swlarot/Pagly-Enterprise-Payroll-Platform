// ====================================================================
// Planilla - HorasPlanillaService
// Prepara las horas de una planilla en bloque. Pensado para miles de
// empleados: tres consultas por planilla (empleados, filas existentes,
// novedades), nunca una consulta por empleado.
// ====================================================================

using Microsoft.EntityFrameworkCore;
using Vorluno.Planilla.Application.Interfaces;
using Vorluno.Planilla.Domain.Entities;
using Vorluno.Planilla.Domain.Enums;
using Vorluno.Planilla.Infrastructure.Data;

namespace Vorluno.Planilla.Infrastructure.Services;

public class HorasPlanillaService : IHorasPlanillaService
{
    private readonly ApplicationDbContext _context;

    public HorasPlanillaService(ApplicationDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<int> GenerarHorasPorDefectoAsync(PayrollHeader planilla, CancellationToken ct = default)
    {
        var tenantId = planilla.TenantId;

        var existentes = await _context.PayrollEmployeeHours
            .Where(h => h.PayrollHeaderId == planilla.Id && h.TenantId == tenantId)
            .Select(h => h.EmpleadoId)
            .ToHashSetAsync(ct);

        // Incluye también las filas agregadas en este mismo contexto y aún no guardadas.
        foreach (var e in _context.ChangeTracker.Entries<PayrollEmployeeHours>())
            if (e.State == EntityState.Added && e.Entity.PayrollHeaderId == planilla.Id)
                existentes.Add(e.Entity.EmpleadoId);

        var activos = await _context.Empleados
            .Where(e => e.TenantId == tenantId && e.EstaActivo && !e.IsDeleted)
            .Select(e => new { e.Id, e.HoursPerPeriod })
            .ToListAsync(ct);

        var nuevas = activos
            .Where(e => !existentes.Contains(e.Id))
            .Select(e => new PayrollEmployeeHours
            {
                PayrollHeaderId = planilla.Id,
                EmpleadoId = e.Id,
                TenantId = tenantId,
                RegularHours = e.HoursPerPeriod,
                CreatedAt = DateTime.UtcNow,
            })
            .ToList();

        _context.PayrollEmployeeHours.AddRange(nuevas);
        return nuevas.Count;
    }

    public async Task<ResumenNovedades> ImportarNovedadesAsync(PayrollHeader planilla, ModoNovedades modo, CancellationToken ct = default)
    {
        var tenantId = planilla.TenantId;
        var inicio = planilla.PeriodStartDate;
        var fin = planilla.PeriodEndDate;

        // Horas extra aprobadas y todavía no pagadas, de todos los empleados, en una consulta.
        var horasExtra = await _context.HorasExtra
            .AsNoTracking()
            .Where(h => h.TenantId == tenantId && h.EstaAprobada && h.PlanillaDetailId == null
                     && h.Fecha >= inicio && h.Fecha <= fin)
            .Select(h => new { h.EmpleadoId, h.TipoHoraExtra, h.CantidadHoras, h.EsExceso })
            .ToListAsync(ct);

        // Ausencias que afectan salario y no han sido procesadas, en una consulta.
        var ausencias = await _context.Ausencias
            .AsNoTracking()
            .Where(a => a.TenantId == tenantId && a.AfectaSalario && a.PlanillaDetailId == null
                     && a.FechaInicio <= fin && a.FechaFin >= inicio)
            .Select(a => new { a.EmpleadoId, a.FechaInicio, a.FechaFin })
            .ToListAsync(ct);

        if (horasExtra.Count == 0 && ausencias.Count == 0)
            return new ResumenNovedades(false, 0, 0, 0, 0, 0);

        var activos = await _context.Empleados
            .Where(e => e.TenantId == tenantId && e.EstaActivo && !e.IsDeleted)
            .Select(e => e.Id)
            .ToHashSetAsync(ct);

        var porEmpleado = new Dictionary<int, Novedad>();
        Novedad De(int empleadoId)
        {
            if (!porEmpleado.TryGetValue(empleadoId, out var n)) porEmpleado[empleadoId] = n = new Novedad();
            return n;
        }

        foreach (var he in horasExtra.Where(h => activos.Contains(h.EmpleadoId)))
        {
            var n = De(he.EmpleadoId);
            // Campos antiguos (diurna/nocturna) por compatibilidad, más el específico.
            switch (he.TipoHoraExtra)
            {
                case TipoHoraExtra.Diurna:
                case TipoHoraExtra.DomingoFeriado:
                    n.Diurna += he.CantidadHoras; break;
                case TipoHoraExtra.Nocturna:
                case TipoHoraExtra.NocturnaDomingoFeriado:
                    n.Nocturna += he.CantidadHoras; break;
                case TipoHoraExtra.FiestaNacionalDiurna:
                    n.Diurna += he.CantidadHoras; n.Festivo += he.CantidadHoras; break;
                case TipoHoraExtra.FiestaNacionalNocturna:
                    n.Nocturna += he.CantidadHoras; n.Festivo += he.CantidadHoras; break;
                case TipoHoraExtra.MixtaDiurnaNocturna:
                    n.Diurna += he.CantidadHoras; n.Mixta += he.CantidadHoras; break;
                case TipoHoraExtra.MixtaNocturnaDiurna:
                    n.Nocturna += he.CantidadHoras; n.Mixta += he.CantidadHoras; break;
                default:
                    n.Diurna += he.CantidadHoras; break;
            }
            if (he.EsExceso) n.Exceso += he.CantidadHoras;
        }

        foreach (var a in ausencias.Where(x => activos.Contains(x.EmpleadoId)))
        {
            var desde = a.FechaInicio < inicio ? inicio : a.FechaInicio;
            var hasta = a.FechaFin > fin ? fin : a.FechaFin;
            var dias = (decimal)(hasta.Date - desde.Date).TotalDays + 1;
            De(a.EmpleadoId).Ausencia += dias * 8m; // 8 horas por día
        }

        var conNovedades = porEmpleado.Where(kv => !kv.Value.Vacia).ToList();
        if (conNovedades.Count == 0)
            return new ResumenNovedades(false, 0, 0, 0, 0, 0);

        var ids = conNovedades.Select(kv => kv.Key).ToList();
        var filas = await _context.PayrollEmployeeHours
            .Where(h => h.PayrollHeaderId == planilla.Id && h.TenantId == tenantId && ids.Contains(h.EmpleadoId))
            .ToDictionaryAsync(h => h.EmpleadoId, ct);

        // Filas recién agregadas en este contexto (planilla nueva) también cuentan.
        foreach (var e in _context.ChangeTracker.Entries<PayrollEmployeeHours>())
            if (e.State == EntityState.Added && e.Entity.PayrollHeaderId == planilla.Id && !filas.ContainsKey(e.Entity.EmpleadoId))
                filas[e.Entity.EmpleadoId] = e.Entity;

        var conPrevios = conNovedades.Count(kv =>
            filas.TryGetValue(kv.Key, out var f) && (f.OvertimeDayHours > 0 || f.OvertimeNightHours > 0 || f.AbsenceHours > 0));

        if (modo == ModoNovedades.Preguntar && conPrevios > 0)
            return new ResumenNovedades(true, conPrevios, conNovedades.Count, 0, 0, 0);

        decimal totDiurna = 0, totNocturna = 0, totAusencia = 0;
        var sumar = modo == ModoNovedades.Sumar;
        foreach (var (empleadoId, n) in conNovedades)
        {
            if (!filas.TryGetValue(empleadoId, out var fila))
            {
                fila = new PayrollEmployeeHours
                {
                    PayrollHeaderId = planilla.Id,
                    EmpleadoId = empleadoId,
                    TenantId = tenantId,
                    RegularHours = 0,
                    CreatedAt = DateTime.UtcNow,
                };
                _context.PayrollEmployeeHours.Add(fila);
                filas[empleadoId] = fila;
            }
            else
            {
                fila.UpdatedAt = DateTime.UtcNow;
            }

            if (sumar)
            {
                fila.OvertimeDayHours += n.Diurna;
                fila.OvertimeNightHours += n.Nocturna;
                fila.OvertimeHolidayHours += n.Festivo;
                fila.OvertimeMixedHours += n.Mixta;
                fila.OvertimeExcessHours += n.Exceso;
                fila.AbsenceHours += n.Ausencia;
            }
            else
            {
                fila.OvertimeDayHours = n.Diurna;
                fila.OvertimeNightHours = n.Nocturna;
                fila.OvertimeHolidayHours = n.Festivo;
                fila.OvertimeMixedHours = n.Mixta;
                fila.OvertimeExcessHours = n.Exceso;
                fila.AbsenceHours = n.Ausencia;
            }

            totDiurna += n.Diurna;
            totNocturna += n.Nocturna;
            totAusencia += n.Ausencia;
        }

        return new ResumenNovedades(false, conPrevios, conNovedades.Count, totDiurna, totNocturna, totAusencia);
    }

    private sealed class Novedad
    {
        public decimal Diurna, Nocturna, Festivo, Mixta, Exceso, Ausencia;
        public bool Vacia => Diurna == 0 && Nocturna == 0 && Festivo == 0 && Mixta == 0 && Exceso == 0 && Ausencia == 0;
    }
}
