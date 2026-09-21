// ====================================================================
// Planilla - DevengadoMensualService
// Combina las planillas de Pagly con los meses importados o manuales.
// Las planillas mandan: si un mes tiene planilla aprobada, lo importado
// para ese mes se ignora (y no se deja guardar).
// ====================================================================

using Microsoft.EntityFrameworkCore;
using Vorluno.Planilla.Application.Interfaces;
using Vorluno.Planilla.Domain.Entities;
using Vorluno.Planilla.Domain.Enums;
using Vorluno.Planilla.Infrastructure.Data;

namespace Vorluno.Planilla.Infrastructure.Services;

public class DevengadoMensualService : IDevengadoMensualService
{
    private readonly ApplicationDbContext _context;
    private readonly ITenantContext _tenantContext;

    public DevengadoMensualService(ApplicationDbContext context, ITenantContext tenantContext)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
    }

    public async Task<IReadOnlyList<MesDevengado>> ObtenerMesesAsync(
        int empleadoId, DateTime desde, DateTime hasta, CancellationToken ct = default)
    {
        var inicio = new DateTime(desde.Year, desde.Month, 1);
        var fin = new DateTime(hasta.Year, hasta.Month, 1);
        if (fin < inicio) return Array.Empty<MesDevengado>();

        // 1) Planillas: el mes es el del período trabajado, nunca el de pago.
        var finExclusivo = fin.AddMonths(1);
        var planillas = await _context.PayrollDetails
            .AsNoTracking()
            .Where(d => d.EmpleadoId == empleadoId
                     && d.PayrollHeader!.PeriodStartDate >= inicio
                     && d.PayrollHeader.PeriodStartDate < finExclusivo
                     && (d.PayrollHeader.Status == PayrollStatus.Approved
                         || d.PayrollHeader.Status == PayrollStatus.Paid))
            .Select(d => new
            {
                d.PayrollHeader!.PeriodStartDate.Year,
                d.PayrollHeader.PeriodStartDate.Month,
                d.GrossPay,
                d.MontoVacaciones,
                d.OvertimePay,
                d.MontoHorasExtraExceso,
                d.Commissions,
                d.GastoRepresentacion
            })
            .ToListAsync(ct);

        var porPlanilla = planillas
            .GroupBy(p => (p.Year, p.Month))
            .ToDictionary(g => g.Key, g =>
            {
                // Misma separación que la ficha de renta: lo que no es vacaciones,
                // extras ni comisión es salario, y la suma es siempre el bruto real.
                var extras = g.Sum(p => p.OvertimePay + p.MontoHorasExtraExceso);
                var vac = g.Sum(p => p.MontoVacaciones);
                var com = g.Sum(p => p.Commissions);
                var bruto = g.Sum(p => p.GrossPay - Math.Min(p.GastoRepresentacion, p.GrossPay));
                return (Salario: bruto - vac - extras - com, Vacaciones: vac, Extras: extras, Comision: com);
            });

        // 2) Importados / manuales.
        var importados = await _context.DevengadosMensuales
            .AsNoTracking()
            .Where(d => d.EmpleadoId == empleadoId
                     && (d.Anio > inicio.Year || (d.Anio == inicio.Year && d.Mes >= inicio.Month))
                     && (d.Anio < fin.Year || (d.Anio == fin.Year && d.Mes <= fin.Month)))
            .ToListAsync(ct);

        var porImportado = importados.ToDictionary(d => (d.Anio, d.Mes));

        // 3) Un registro por mes, con la fuente que mande.
        var resultado = new List<MesDevengado>();
        for (var cursor = inicio; cursor <= fin; cursor = cursor.AddMonths(1))
        {
            var clave = (cursor.Year, cursor.Month);

            if (porPlanilla.TryGetValue(clave, out var p))
            {
                resultado.Add(new MesDevengado(cursor.Year, cursor.Month,
                    Redondear(p.Salario), Redondear(p.Vacaciones), Redondear(p.Extras), Redondear(p.Comision),
                    OrigenDevengado.Planilla));
            }
            else if (porImportado.TryGetValue(clave, out var d))
            {
                resultado.Add(new MesDevengado(cursor.Year, cursor.Month,
                    d.Salario, d.Vacaciones, d.Extras, d.Comision, d.Origen));
            }
            else
            {
                resultado.Add(new MesDevengado(cursor.Year, cursor.Month, 0m, 0m, 0m, 0m, OrigenDevengado.SinDatos));
            }
        }

        return resultado;
    }

    public Task<IReadOnlyList<MesDevengado>> UltimosMesesAsync(
        int empleadoId, DateTime hasta, int cantidad, CancellationToken ct = default)
    {
        if (cantidad <= 0) return Task.FromResult<IReadOnlyList<MesDevengado>>(Array.Empty<MesDevengado>());
        var finMes = new DateTime(hasta.Year, hasta.Month, 1);
        var desde = finMes.AddMonths(-(cantidad - 1));
        return ObtenerMesesAsync(empleadoId, desde, finMes, ct);
    }

    public async Task GuardarMesAsync(
        int empleadoId, int anio, int mes,
        decimal salario, decimal vacaciones, decimal extras, decimal comision,
        OrigenDevengado origen, string? nota = null, CancellationToken ct = default)
    {
        if (mes is < 1 or > 12) throw new ArgumentOutOfRangeException(nameof(mes));
        if (origen is OrigenDevengado.Planilla or OrigenDevengado.SinDatos)
            throw new ArgumentException("Solo se guardan meses importados o manuales.", nameof(origen));
        if (salario < 0 || vacaciones < 0 || extras < 0 || comision < 0)
            throw new ArgumentException("Los montos no pueden ser negativos.");

        if (await MesTienePlanillaAsync(empleadoId, anio, mes, ct))
            throw new InvalidOperationException(
                $"El mes {mes:D2}/{anio} ya tiene planilla en Pagly; su devengado se toma de ahí y no se puede sobreescribir.");

        var tenantId = _tenantContext.TenantId;
        var existente = await _context.DevengadosMensuales
            .FirstOrDefaultAsync(d => d.EmpleadoId == empleadoId && d.Anio == anio && d.Mes == mes, ct);

        if (existente is null)
        {
            _context.DevengadosMensuales.Add(new DevengadoMensual
            {
                TenantId = tenantId,
                EmpleadoId = empleadoId,
                Anio = anio,
                Mes = mes,
                Salario = salario,
                Vacaciones = vacaciones,
                Extras = extras,
                Comision = comision,
                Origen = origen,
                Nota = nota
            });
        }
        else
        {
            existente.Salario = salario;
            existente.Vacaciones = vacaciones;
            existente.Extras = extras;
            existente.Comision = comision;
            existente.Origen = origen;
            existente.Nota = nota;
            existente.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync(ct);
    }

    public Task<bool> MesTienePlanillaAsync(int empleadoId, int anio, int mes, CancellationToken ct = default)
    {
        var inicio = new DateTime(anio, mes, 1);
        var fin = inicio.AddMonths(1);
        return _context.PayrollDetails
            .AnyAsync(d => d.EmpleadoId == empleadoId
                        && d.PayrollHeader!.PeriodStartDate >= inicio
                        && d.PayrollHeader.PeriodStartDate < fin
                        && (d.PayrollHeader.Status == PayrollStatus.Approved
                            || d.PayrollHeader.Status == PayrollStatus.Paid), ct);
    }

    private static decimal Redondear(decimal v) => Math.Round(v, 2, MidpointRounding.AwayFromZero);
}
