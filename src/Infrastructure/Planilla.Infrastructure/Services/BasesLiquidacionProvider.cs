// ====================================================================
// Planilla - BasesLiquidacionProvider
//
// El devengado real que la hoja del contador usa para liquidar. Todo sale
// de IDevengadoMensualService (planillas de Pagly + meses importados o
// escritos a mano), y los dos cortes —la última vacación tomada y la
// última partida de décimo pagada— salen de sus propias tablas.
// ====================================================================

using Microsoft.EntityFrameworkCore;
using Vorluno.Planilla.Application.DTOs;
using Vorluno.Planilla.Application.Interfaces;
using Vorluno.Planilla.Domain.Enums;
using Vorluno.Planilla.Infrastructure.Data;

namespace Vorluno.Planilla.Infrastructure.Services;

public class BasesLiquidacionProvider : IBasesLiquidacionProvider
{
    private readonly ApplicationDbContext _context;
    private readonly IDevengadoMensualService _devengado;

    public BasesLiquidacionProvider(ApplicationDbContext context, IDevengadoMensualService devengado)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _devengado = devengado ?? throw new ArgumentNullException(nameof(devengado));
    }

    public async Task<BasesDevengadasLiquidacion?> ObtenerAsync(
        int empleadoId, DateTime fechaContratacion, DateTime fechaTerminacion, CancellationToken ct = default)
    {
        // 60 meses hacia atrás desde el mes de la terminación (Art. 224: 260 semanas).
        var meses60 = await _devengado.UltimosMesesAsync(empleadoId, fechaTerminacion, 60, ct);
        if (meses60.Count == 0 || meses60.All(m => m.Total <= 0m)) return null;

        // Los meses anteriores a la contratación no cuentan aunque haya datos sueltos.
        var desdeContratacion = meses60
            .Where(m => new DateTime(m.Anio, m.Mes, 1) >= new DateTime(fechaContratacion.Year, fechaContratacion.Month, 1))
            .ToList();

        var ultimos6 = desdeContratacion.TakeLast(6).ToList();
        var ultimoConDatos = desdeContratacion.LastOrDefault(m => m.Total > 0m);

        // Última vacación tomada: desde el mes siguiente arranca el período de
        // referencia de las vacaciones proporcionales; si no hay, desde la contratación.
        var ultimaVacacion = await _context.SolicitudesVacaciones
            .AsNoTracking()
            .Where(v => v.EmpleadoId == empleadoId
                     && v.FechaFin <= fechaTerminacion
                     && (v.Estado == EstadoVacaciones.Aprobada || v.Estado == EstadoVacaciones.EnCurso || v.Estado == EstadoVacaciones.Completada))
            .OrderByDescending(v => v.FechaFin)
            .Select(v => (DateTime?)v.FechaFin)
            .FirstOrDefaultAsync(ct);

        var vacacionesDesde = SiguienteMes(ultimaVacacion ?? fechaContratacion);
        var devengadoVacaciones = await SumarDesdeAsync(empleadoId, vacacionesDesde, fechaTerminacion, ct);

        // Última partida de décimo pagada al empleado.
        var ultimaPartida = await _context.DetallesDecimo
            .AsNoTracking()
            .Where(d => d.EmpleadoId == empleadoId
                     && d.PlanillaDecimo!.FechaPago <= fechaTerminacion
                     && (d.PlanillaDecimo.Estado == EstadoDecimo.Calculada || d.PlanillaDecimo.Estado == EstadoDecimo.Pagada))
            .OrderByDescending(d => d.PlanillaDecimo!.FechaPago)
            .Select(d => (DateTime?)d.PlanillaDecimo!.FechaPago)
            .FirstOrDefaultAsync(ct);

        var decimoDesde = SiguienteMes(ultimaPartida ?? fechaContratacion);
        var devengadoDecimo = await SumarDesdeAsync(empleadoId, decimoDesde, fechaTerminacion, ct);

        return new BasesDevengadasLiquidacion
        {
            Meses60 = desdeContratacion
                .Select(m => new MesLiquidacion(m.Anio, m.Mes, m.Total, m.Origen.ToString()))
                .ToList(),
            Devengado6Meses = ultimos6.Sum(m => m.Total),
            Meses6ConDatos = ultimos6.Count(m => m.Total > 0m),
            UltimoMesDevengado = ultimoConDatos?.Total ?? 0m,
            DevengadoDesdeUltimaVacacion = devengadoVacaciones,
            VacacionesDesde = vacacionesDesde,
            DevengadoDesdeUltimaPartidaDecimo = devengadoDecimo,
            DecimoDesde = decimoDesde,
        };
    }

    private async Task<decimal> SumarDesdeAsync(int empleadoId, DateTime desde, DateTime hasta, CancellationToken ct)
    {
        if (desde > hasta) return 0m;
        var meses = await _devengado.ObtenerMesesAsync(empleadoId, desde, hasta, ct);
        return meses.Sum(m => m.Total);
    }

    /// <summary>El primer día del mes siguiente a una fecha: el corte empieza ahí.</summary>
    private static DateTime SiguienteMes(DateTime fecha) => new DateTime(fecha.Year, fecha.Month, 1).AddMonths(1);
}
