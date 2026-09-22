// ====================================================================
// Planilla - DecimoCalculationService
//
// El décimo sale del devengado mes a mes del cuatrimestre, y el devengado
// lo da IDevengadoMensualService: planillas aprobadas de Pagly primero, y
// si un mes no tiene planilla, lo importado al migrar o escrito a mano.
// Así dejan de perderse los meses previos a Pagly y las planillas cuyo
// período cruza el borde del cuatrimestre (una planilla cuenta en el mes
// de su período trabajado).
//
// Los meses parciales (el primero y el último del cuatrimestre, que suele
// empezar el 16 y terminar el 15) se prorratean por los días que caen
// dentro del período cuando el mes NO viene de planillas; los meses de
// planilla ya traen exactamente lo que se pagó en ellos.
//
// Previsualizar no guarda nada: es lo que la pantalla enseña antes de
// crear la partida. Al calcular, los meses escritos a mano se guardan como
// devengado manual del empleado para que la liquidación y la ficha de
// renta los vean también.
// ====================================================================

using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Vorluno.Planilla.Application.DTOs;
using Vorluno.Planilla.Application.Helpers;
using Vorluno.Planilla.Application.Interfaces;
using Vorluno.Planilla.Domain.Entities;
using Vorluno.Planilla.Domain.Enums;
using Vorluno.Planilla.Infrastructure.Data;

namespace Vorluno.Planilla.Infrastructure.Services;

public class DecimoCalculationService : IDecimoCalculationService
{
    private readonly ApplicationDbContext _context;
    private readonly IPayrollConfigProvider _configProvider;
    private readonly IDevengadoMensualService _devengado;

    public DecimoCalculationService(
        ApplicationDbContext context,
        IPayrollConfigProvider configProvider,
        IDevengadoMensualService devengado)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _configProvider = configProvider ?? throw new ArgumentNullException(nameof(configProvider));
        _devengado = devengado ?? throw new ArgumentNullException(nameof(devengado));
    }

    public async Task<DecimoPreview> PrevisualizarAsync(
        DateTime periodoDesde, DateTime periodoHasta, DateTime fechaPago, int tenantId,
        IReadOnlyList<AjusteMesDecimo>? ajustes = null, CancellationToken ct = default)
    {
        var empleados = await _context.Empleados
            .AsNoTracking()
            .Where(e => e.TenantId == tenantId && e.EstaActivo && !e.IsDeleted)
            .OrderBy(e => e.Apellido).ThenBy(e => e.Nombre)
            .ToListAsync(ct);

        var taxConfig = await _configProvider.GetTaxConfigAsync(tenantId, fechaPago);
        var taxBrackets = await _configProvider.GetTaxBracketsAsync(tenantId, fechaPago.Year);
        var porAjuste = (ajustes ?? Array.Empty<AjusteMesDecimo>())
            .ToDictionary(a => (a.EmpleadoId, a.Anio, a.Mes), a => a.Monto);

        var lista = new List<EmpleadoDecimoPreview>();
        foreach (var empleado in empleados)
            lista.Add(await CalcularEmpleadoAsync(empleado, periodoDesde, periodoHasta, tenantId, taxConfig, taxBrackets, porAjuste, ct));

        return new DecimoPreview(lista);
    }

    public async Task<DecimoCalculationSummary> CalcularAsync(
        int planillaDecimoId, int tenantId,
        IReadOnlyList<AjusteMesDecimo>? ajustes = null, CancellationToken ct = default)
    {
        var planilla = await _context.PlanillasDecimo
            .Include(p => p.Detalles)
            .FirstOrDefaultAsync(p => p.Id == planillaDecimoId && p.TenantId == tenantId, ct)
            ?? throw new InvalidOperationException("Planilla de décimo no encontrada");

        if (planilla.Estado == EstadoDecimo.Pagada)
            throw new InvalidOperationException("No se puede recalcular una partida ya pagada: reábrela primero.");

        var preview = await PrevisualizarAsync(
            planilla.PeriodoDesde, planilla.PeriodoHasta, planilla.FechaPago, tenantId, ajustes, ct);

        // Los meses escritos a mano se guardan como devengado manual del empleado:
        // desde ahí los verán la liquidación, la ficha de renta y el próximo décimo.
        foreach (var a in ajustes ?? Array.Empty<AjusteMesDecimo>())
        {
            if (a.Monto < 0) throw new InvalidOperationException("Un mes escrito a mano no puede ser negativo.");
            if (await _devengado.MesTienePlanillaAsync(a.EmpleadoId, a.Anio, a.Mes, ct))
                throw new InvalidOperationException(
                    $"El mes {a.Mes:D2}/{a.Anio} ya tiene planilla en Pagly: su devengado se toma de ahí y no se escribe a mano.");
            await _devengado.GuardarMesAsync(a.EmpleadoId, a.Anio, a.Mes,
                salario: a.Monto, vacaciones: 0m, extras: 0m, comision: 0m,
                origen: OrigenDevengado.Manual, nota: $"Escrito al crear el décimo {planilla.Numero}", ct: ct);
        }

        if (planilla.Detalles.Any())
        {
            _context.DetallesDecimo.RemoveRange(planilla.Detalles);
            planilla.Detalles.Clear();
        }

        var procesados = 0;
        foreach (var e in preview.Empleados)
        {
            // Un empleado sin nada devengado en el cuatrimestre no entra en la partida.
            if (e.TotalDevengado <= 0m) continue;

            _context.DetallesDecimo.Add(new DetalleDecimo
            {
                TenantId = tenantId,
                PlanillaDecimoId = planilla.Id,
                EmpleadoId = e.EmpleadoId,
                DesgloseMensualJson = JsonSerializer.Serialize(
                    e.Meses.Select(m => new DesgloseMensualItem(m.Anio, m.Mes, m.Monto)).ToList()),
                TotalDevengado = e.TotalDevengado,
                MontoDecimo = e.MontoDecimo,
                CssEmpleado = e.CssEmpleado,
                CssPatrono = e.CssPatrono,
                SeEmpleado = e.SeEmpleado,
                SePatrono = e.SePatrono,
                ISR = e.Isr,
                TotalDeducciones = e.TotalDeducciones,
                NetoPago = e.NetoPago,
                CreatedAt = DateTime.UtcNow
            });
            procesados++;
        }

        var conDatos = preview.Empleados.Where(e => e.TotalDevengado > 0m).ToList();
        planilla.TotalDevengado = conDatos.Sum(e => e.TotalDevengado);
        planilla.TotalDecimo = conDatos.Sum(e => e.MontoDecimo);
        planilla.TotalCssEmpleado = conDatos.Sum(e => e.CssEmpleado);
        planilla.TotalCssPatrono = conDatos.Sum(e => e.CssPatrono);
        planilla.TotalSeEmpleado = conDatos.Sum(e => e.SeEmpleado);
        planilla.TotalSePatrono = conDatos.Sum(e => e.SePatrono);
        planilla.TotalISR = conDatos.Sum(e => e.Isr);
        planilla.TotalNetoPago = conDatos.Sum(e => e.NetoPago);
        planilla.Estado = EstadoDecimo.Calculada;
        planilla.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(ct);
        return new DecimoCalculationSummary(procesados, planilla.TotalDecimo);
    }

    // ────────────────────────────────────────────────────────────────

    private async Task<EmpleadoDecimoPreview> CalcularEmpleadoAsync(
        Empleado empleado, DateTime desde, DateTime hasta, int tenantId,
        PayrollTaxConfigDto? taxConfig, List<TaxBracketDto> taxBrackets,
        Dictionary<(int, int, int), decimal> porAjuste, CancellationToken ct)
    {
        var meses = await _devengado.ObtenerMesesAsync(empleado.Id, desde, hasta, ct);
        var numerosPlanilla = await NumerosDePlanillaPorMesAsync(empleado.Id, tenantId, desde, hasta, ct);

        var filas = new List<MesDecimo>();
        foreach (var m in meses)
        {
            var fraccion = FraccionDelMesDentroDelPeriodo(m.Anio, m.Mes, desde, hasta);
            var clave = (empleado.Id, m.Anio, m.Mes);

            if (porAjuste.TryGetValue(clave, out var manual))
            {
                // Lo escrito a mano manda, salvo que el mes venga de una planilla de Pagly.
                if (m.Origen != OrigenDevengado.Planilla)
                {
                    filas.Add(new MesDecimo(m.Anio, m.Mes, RoundingPolicy.Round(manual), OrigenDevengado.Manual, fraccion, Array.Empty<string>()));
                    continue;
                }
            }

            var numeros = numerosPlanilla.TryGetValue((m.Anio, m.Mes), out var ns) ? ns : new List<string>();
            var monto = m.Origen switch
            {
                // Las planillas ya traen lo que se pagó en ese mes dentro del período.
                OrigenDevengado.Planilla => m.Total,
                OrigenDevengado.SinDatos => 0m,
                // Un mes importado/manual es un mes completo: si el período solo
                // cubre parte de él, se prorratea por días.
                _ => RoundingPolicy.Round(m.Total * fraccion),
            };
            filas.Add(new MesDecimo(m.Anio, m.Mes, monto, m.Origen, fraccion, numeros));
        }

        var totalDev = RoundingPolicy.Round(filas.Sum(f => f.Monto));
        var montoDecimo = RoundingPolicy.Round(totalDev / 12m);

        // CSS reducida del décimo: 7.25 % empleado / 10.75 % patronal
        // (Ley 51/2005 Art. 96.4-96.5). El Seguro Educativo no se reduce.
        var cssEmp = RoundingPolicy.Round(montoDecimo * PayrollConstants.CssTasaDecimoEmpleado);
        var cssPat = RoundingPolicy.Round(montoDecimo * PayrollConstants.CssTasaDecimoPatronal);

        var seActivo = empleado.IsSubjectToEducationalInsurance;
        var seEmp = seActivo ? RoundingPolicy.Round(montoDecimo * PayrollConstants.SeTasaEmpleado) : 0m;
        var sePat = seActivo ? RoundingPolicy.Round(montoDecimo * PayrollConstants.SeTasaPatronal) : 0m;

        var isr = 0m;
        if (empleado.IsSubjectToIncomeTax && taxBrackets.Count > 0 && montoDecimo > 0m)
        {
            // ISR del décimo: (salario mensual + décimo) × 13 → tramos → ÷ 13.
            // El salario mensual sale del último mes con devengado del período.
            var ultimoConDatos = filas.LastOrDefault(f => f.Monto > 0m);
            var salarioMensual = ultimoConDatos is null
                ? 0m
                : (ultimoConDatos.Fraccion > 0m ? RoundingPolicy.Round(ultimoConDatos.Monto / ultimoConDatos.Fraccion) : ultimoConDatos.Monto);

            var annualBase = (salarioMensual + montoDecimo) * 13m;
            var depDeduccion = 0m;
            if (taxConfig != null)
            {
                var validDeps = Math.Min(empleado.Dependents, taxConfig.MaxDependents);
                depDeduccion = validDeps * taxConfig.DependentDeductionAmount;
            }
            // El Seguro Educativo no se deduce de la base del ISR (Ley 8 de 2010, Art. 24).
            var netGravable = Math.Max(0m, annualBase - depDeduccion);
            isr = RoundingPolicy.Round(CalcularIsrAnual(netGravable, taxBrackets) / 13m);
        }

        var totalDed = cssEmp + seEmp + isr;
        return new EmpleadoDecimoPreview(
            empleado.Id,
            $"{empleado.Nombre} {empleado.Apellido}".Trim(),
            empleado.NumeroIdentificacion,
            filas,
            totalDev, montoDecimo,
            cssEmp, cssPat, seEmp, sePat, isr,
            totalDed, montoDecimo - totalDed);
    }

    /// <summary>Los números de planilla que aportan a cada mes, para poder decirlo en pantalla.</summary>
    private async Task<Dictionary<(int, int), List<string>>> NumerosDePlanillaPorMesAsync(
        int empleadoId, int tenantId, DateTime desde, DateTime hasta, CancellationToken ct)
    {
        var inicio = new DateTime(desde.Year, desde.Month, 1);
        var finExclusivo = new DateTime(hasta.Year, hasta.Month, 1).AddMonths(1);

        var filas = await _context.PayrollDetails
            .AsNoTracking()
            .Where(d => d.TenantId == tenantId && d.EmpleadoId == empleadoId
                     && d.PayrollHeader!.PeriodStartDate >= inicio
                     && d.PayrollHeader.PeriodStartDate < finExclusivo
                     && (d.PayrollHeader.Status == PayrollStatus.Approved || d.PayrollHeader.Status == PayrollStatus.Paid))
            .Select(d => new { d.PayrollHeader!.PeriodStartDate.Year, d.PayrollHeader.PeriodStartDate.Month, d.PayrollHeader.PayrollNumber })
            .ToListAsync(ct);

        return filas
            .GroupBy(f => (f.Year, f.Month))
            .ToDictionary(g => g.Key, g => g.Select(x => x.PayrollNumber).Distinct().OrderBy(x => x).ToList());
    }

    /// <summary>
    /// Qué parte de un mes cae dentro del período (1 = completo). El cuatrimestre
    /// del décimo suele ir del 16 al 15, así que el primero y el último mes son
    /// parciales.
    /// </summary>
    public static decimal FraccionDelMesDentroDelPeriodo(int anio, int mes, DateTime desde, DateTime hasta)
    {
        var primero = new DateTime(anio, mes, 1);
        var ultimo = primero.AddMonths(1).AddDays(-1);
        var d = desde.Date > primero ? desde.Date : primero;
        var h = hasta.Date < ultimo ? hasta.Date : ultimo;
        if (h < d) return 0m;
        var dias = (decimal)(h - d).TotalDays + 1m;
        return Math.Round(dias / DateTime.DaysInMonth(anio, mes), 4);
    }

    private static decimal CalcularIsrAnual(decimal netGravable, List<TaxBracketDto> brackets)
    {
        var ordered = brackets.OrderBy(b => b.MinIncome).ToList();

        TaxBracketDto? bracket = null;
        foreach (var b in ordered)
        {
            if (netGravable > b.MinIncome)
                bracket = b;
            else
                break;
        }

        if (bracket == null) return 0m;

        var excess = netGravable - bracket.MinIncome;
        var bracketTax = RoundingPolicy.CalculatePercentage(excess, bracket.Rate);
        return RoundingPolicy.Round(bracket.FixedAmount + bracketTax);
    }
}
