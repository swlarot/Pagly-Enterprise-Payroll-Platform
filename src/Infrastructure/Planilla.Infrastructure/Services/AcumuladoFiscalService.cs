// ====================================================================
// Planilla - AcumuladoFiscalService
// Arma el acumulado fiscal del año de un empleado para el motor de ISR.
//
// Decisión de diseño: lo acumulado se DERIVA de las planillas guardadas en
// lugar de llevarse en un contador que se va sumando corrida a corrida. Un
// contador mutable obliga a revertir con exactitud cada recálculo y cada
// anulación, y basta un camino que no revierta para que el empleado quede con
// el ISR del año descuadrado. Derivándolo, el acumulado siempre refleja lo que
// de verdad hay guardado.
//
// Los saldos iniciales de migración sí se guardan: no hay planillas de las que
// derivarlos porque se generaron en el sistema anterior.
// ====================================================================

using Microsoft.EntityFrameworkCore;
using Vorluno.Planilla.Application.DTOs;
using Vorluno.Planilla.Application.Interfaces;
using Vorluno.Planilla.Application.Services;
using Vorluno.Planilla.Domain.Enums;
using Vorluno.Planilla.Infrastructure.Data;

namespace Vorluno.Planilla.Infrastructure.Services;

/// <inheritdoc cref="IAcumuladoFiscalService"/>
public class AcumuladoFiscalService : IAcumuladoFiscalService
{
    private readonly ApplicationDbContext _context;

    public AcumuladoFiscalService(ApplicationDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<AcumuladoIsr> ObtenerAcumuladoAsync(
        int empleadoId,
        int anio,
        int? excluirPayrollHeaderId = null,
        int? excluirPlanillaDecimoId = null,
        CancellationToken cancellationToken = default)
    {
        // Saldos que trae de otro sistema. Puede no existir el registro: un
        // empleado que nació en esta plataforma arranca todo en cero.
        var saldos = await _context.AcumuladosFiscalesEmpleados
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.EmpleadoId == empleadoId && a.Anio == anio, cancellationToken);

        // Planillas regulares del año. La base gravable es el bruto menos el
        // Seguro Social: es la única deducción que resta, criterio confirmado
        // por el contador de la empresa.
        var regulares = await ConsultaRegulares(empleadoId, anio, excluirPayrollHeaderId)
            .Select(d => new
            {
                d.GrossPay,
                d.CssEmployee,
                d.IncomeTax,
                d.GastoRepresentacion,
                d.IsrGastoRepresentacion
            })
            .ToListAsync(cancellationToken);

        // Base BRUTA, sin restar el Seguro Social: criterio del contador. El gasto de
        // representación viaja dentro del bruto pero tributa aparte, así que es lo
        // único que sale de la base del salario.
        var ingresoGravable = regulares.Sum(d => d.GrossPay - Math.Min(d.GastoRepresentacion, d.GrossPay));

        // IncomeTax trae las dos retenciones sumadas; para el acumulado del salario
        // se descuenta la que corresponde al gasto de representación.
        var isrRegular = regulares.Sum(d => d.IncomeTax - d.IsrGastoRepresentacion);

        var gastoRepresentacion = regulares.Sum(d => d.GastoRepresentacion);
        var isrGastoRepresentacion = regulares.Sum(d => d.IsrGastoRepresentacion);

        // Partidas de décimo del año. Van aparte porque el motor las trata como
        // un ingreso propio dentro del reparto de períodos equivalentes.
        var decimos = await ConsultaDecimos(empleadoId, anio, excluirPlanillaDecimoId)
            .Select(d => new { d.MontoDecimo, d.CssEmpleado, d.ISR })
            .ToListAsync(cancellationToken);

        // También bruto: el décimo entra al ACUMULADO del libro tal cual se paga.
        var decimoGravable = decimos.Sum(d => d.MontoDecimo);
        var isrDecimo = decimos.Sum(d => d.ISR);
        var partidasDecimo = decimos.Count(d => d.MontoDecimo > 0m);

        // Meses del año importados o escritos a mano que NO tienen planilla en
        // Pagly. Entran al acumulado igual que una planilla: son ingreso del año.
        var importados = await MesesImportadosSinPlanillaAsync(empleadoId, anio, cancellationToken);
        var ingresoImportado = importados.Sum(m => m.Total);

        return new AcumuladoIsr
        {
            IngresoGravableInicial = (saldos?.IngresoGravableInicial ?? 0m) + ingresoImportado,
            DecimoInicial = saldos?.DecimoInicial ?? 0m,
            IsrRetenidoInicial = saldos?.IsrRetenidoInicial ?? 0m,
            IngresoGravableProcesado = ingresoGravable,
            DecimoProcesado = decimoGravable,
            PartidasDecimoInicial = saldos?.PartidasDecimoInicial ?? 0,
            PartidasDecimoProcesadas = partidasDecimo,
            IsrRegularProcesado = isrRegular,
            IsrDecimoProcesado = isrDecimo,
            GastoRepresentacionInicial = saldos?.GastoRepresentacionInicial ?? 0m,
            IsrGastoRepresentacionInicial = saldos?.IsrGastoRepresentacionInicial ?? 0m,
            GastoRepresentacionProcesado = gastoRepresentacion,
            IsrGastoRepresentacionProcesado = isrGastoRepresentacion
        };
    }

    public async Task<int> ObtenerNumeroPeriodoAsync(
        int empleadoId,
        int anio,
        int? excluirPayrollHeaderId = null,
        CancellationToken cancellationToken = default)
    {
        var corridasPrevias = await ConsultaRegulares(empleadoId, anio, excluirPayrollHeaderId)
            .CountAsync(cancellationToken);

        // Los meses importados sin planilla también son períodos corridos del año:
        // un mes quincenal son dos quincenas. Sin esto, ocho meses importados se
        // proyectarían como si fueran una sola quincena y la renta se dispararía.
        var frecuencia = await _context.Empleados
            .Where(e => e.Id == empleadoId)
            .Select(e => e.PayPeriodType)
            .FirstOrDefaultAsync(cancellationToken);
        var importados = await MesesImportadosSinPlanillaAsync(empleadoId, anio, cancellationToken);
        var periodosImportados = importados.Count * PeriodosPorMes(frecuencia);

        // La corrida que se está calculando todavía no está guardada, por eso el +1.
        return corridasPrevias + periodosImportados + 1;
    }

    /// <summary>Cuántos períodos de pago tiene un mes según la frecuencia (2 en quincenal, 1 en mensual…).</summary>
    private static int PeriodosPorMes(PayPeriodType frecuencia) => frecuencia switch
    {
        PayPeriodType.Semanal => 4,
        PayPeriodType.Bisemanal => 2,
        PayPeriodType.Mensual => 1,
        _ => 2
    };

    private sealed record MesImportado(int Anio, int Mes, decimal Salario, decimal Vacaciones, decimal Extras, decimal Comision)
    {
        public decimal Total => Salario + Vacaciones + Extras + Comision;
    }

    /// <summary>
    /// Meses del año cargados en DevengadoMensual (importados o manuales) que no
    /// tienen ninguna planilla aprobada o pagada. Un mes con planilla se deriva de
    /// ella y lo importado se ignora, igual que en DevengadoMensualService.
    /// </summary>
    private async Task<List<MesImportado>> MesesImportadosSinPlanillaAsync(int empleadoId, int anio, CancellationToken ct)
    {
        var cargados = await _context.DevengadosMensuales
            .AsNoTracking()
            .Where(d => d.EmpleadoId == empleadoId && d.Anio == anio)
            .Select(d => new MesImportado(d.Anio, d.Mes, d.Salario, d.Vacaciones, d.Extras, d.Comision))
            .ToListAsync(ct);
        if (cargados.Count == 0) return cargados;

        var inicio = new DateTime(anio, 1, 1);
        var fin = inicio.AddYears(1);
        var mesesConPlanilla = await _context.PayrollDetails
            .AsNoTracking()
            .Where(d => d.EmpleadoId == empleadoId
                     && d.PayrollHeader!.PeriodStartDate >= inicio
                     && d.PayrollHeader.PeriodStartDate < fin
                     && (d.PayrollHeader.Status == PayrollStatus.Approved || d.PayrollHeader.Status == PayrollStatus.Paid))
            .Select(d => d.PayrollHeader!.PeriodStartDate.Month)
            .Distinct()
            .ToListAsync(ct);

        return cargados.Where(m => !mesesConPlanilla.Contains(m.Mes)).OrderBy(m => m.Mes).ToList();
    }


    public async Task<FichaIsrAnualDto?> ObtenerFichaAnualAsync(
        int empleadoId,
        int anio,
        CancellationToken cancellationToken = default)
    {
        var empleado = await _context.Empleados
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == empleadoId, cancellationToken);

        if (empleado is null) return null;

        var saldos = await _context.AcumuladosFiscalesEmpleados
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.EmpleadoId == empleadoId && a.Anio == anio, cancellationToken);

        var frecuencia = empleado.PayPeriodType;
        var periodosCalendario = MotorIsrPanama.ObtenerPeriodosAnuales(frecuencia);
        var periodosDePago = MotorIsrPanama.ObtenerPeriodosEquivalentesAnuales(frecuencia);

        var ficha = new FichaIsrAnualDto
        {
            EmpleadoId = empleadoId,
            Empleado = $"{empleado.Nombre} {empleado.Apellido}".Trim(),
            Cedula = empleado.NumeroIdentificacion,
            Anio = anio,
            SalarioBase = empleado.SalarioBase,
            ConyugeDependiente = empleado.Dependents > 0 ? "SI" : "NO",
            PeriodosDePago = Math.Round(periodosDePago, 2),
            NombrePeriodo = frecuencia switch
            {
                PayPeriodType.Semanal => "Semanas",
                PayPeriodType.Bisemanal => "Bisemanas",
                PayPeriodType.Mensual => "Meses",
                _ => "Quincenas"
            }
        };

        // Las filas fijas de la hoja: 24 en quincenal, 12 en mensual, etc. Existen
        // aunque no haya planilla, igual que en el Excel, donde las quincenas sin
        // datos siguen ahí con ceros y el ACUMULADO se arrastra.
        var filas = Enumerable.Range(1, periodosCalendario)
            .Select(n => new FilaFichaIsrDto { Quincena = n })
            .ToList();

        // Planillas regulares del año. Las columnas se separan del bruto guardado:
        // lo que no es vacaciones, extras, comisión ni gasto de representación es
        // SALARIOS, de modo que la suma de la fila es SIEMPRE el bruto real.
        var regulares = await ConsultaRegulares(empleadoId, anio, null)
            .Select(d => new
            {
                d.PayrollHeader!.PeriodEndDate,
                d.GrossPay,
                d.Commissions,
                d.MontoVacaciones,
                d.OvertimePay,
                d.MontoHorasExtraExceso,
                d.GastoRepresentacion
            })
            .ToListAsync(cancellationToken);

        foreach (var d in regulares)
        {
            var fila = filas[IndiceDeFila(frecuencia, d.PeriodEndDate) - 1];
            var extras = d.OvertimePay + d.MontoHorasExtraExceso;
            var gasto = Math.Min(d.GastoRepresentacion, d.GrossPay);

            fila.Vacaciones += d.MontoVacaciones;
            fila.Extras += extras;
            fila.Comision += d.Commissions;
            fila.Salarios += d.GrossPay - gasto - d.MontoVacaciones - extras - d.Commissions;
            fila.TieneDatos = true;
        }

        // Meses importados o manuales sin planilla: se reparten en partes iguales
        // entre las filas de su mes (un mes quincenal, dos quincenas), como si
        // fueran planillas. Así la columna PERIODOS avanza y la proyección es real.
        var mesesConPlanilla = regulares.Select(d => d.PeriodEndDate.Month).ToHashSet();
        var importadosDelAnio = await MesesImportadosSinPlanillaAsync(empleadoId, anio, cancellationToken);
        foreach (var m in importadosDelAnio.Where(m => !mesesConPlanilla.Contains(m.Mes)))
        {
            var filasDelMes = filas.Where(f => MesDeLaFila(frecuencia, f.Quincena) == Meses[m.Mes - 1]).ToList();
            if (filasDelMes.Count == 0) continue;
            var partes = filasDelMes.Count;
            decimal Reparto(decimal total, int i) =>
                i < partes - 1 ? Redondear(total / partes) : total - Redondear(total / partes) * (partes - 1);
            for (var i = 0; i < partes; i++)
            {
                var fila = filasDelMes[i];
                fila.Salarios += Reparto(m.Salario, i);
                fila.Vacaciones += Reparto(m.Vacaciones, i);
                fila.Extras += Reparto(m.Extras, i);
                fila.Comision += Reparto(m.Comision, i);
                fila.TieneDatos = true;
                fila.EsImportado = true;
            }
        }

        // Partidas de décimo: van en la fila de la primera quincena del mes en que
        // se pagan (abril -> q7, agosto -> q15, diciembre -> q23), como en la hoja.
        var decimos = await ConsultaDecimos(empleadoId, anio, null)
            .Select(d => new { d.PlanillaDecimo!.FechaPago, d.MontoDecimo })
            .ToListAsync(cancellationToken);

        foreach (var d in decimos.Where(d => d.MontoDecimo > 0m))
        {
            var fila = filas[IndiceDeFilaDecimo(frecuencia, d.FechaPago) - 1];
            fila.XiiiMes += d.MontoDecimo;
            fila.TieneDatos = true;
        }

        // Saldos de migración: entran como si fueran la fila 0 de la hoja.
        var acumulado = (saldos?.IngresoGravableInicial ?? 0m) + (saldos?.DecimoInicial ?? 0m);
        var partidas = saldos?.PartidasDecimoInicial ?? 0;

        // Y ahora las fórmulas de la hoja, fila por fila.
        foreach (var fila in filas)
        {
            if (fila.XiiiMes > 0m) partidas++;

            fila.Periodos = MotorIsrPanama.CalcularPeriodoEquivalente(frecuencia, fila.Quincena, partidas);

            acumulado += fila.Salarios + fila.Vacaciones + fila.Extras + fila.Comision + fila.XiiiMes;
            fila.Acumulado = Redondear(acumulado);

            // =I/C*26, con el acumulado sin redondear, como hace Excel.
            var ingresoGravable = acumulado / fila.Periodos * periodosDePago;
            fila.IngresoGravable = Redondear(ingresoGravable);

            var rentaAnual = MotorIsrPanama.CalcularIsrAnual(ingresoGravable);
            fila.RentaAnual = rentaAnual;

            var rentaPorPeriodo = rentaAnual / periodosDePago;
            fila.RentaPorPeriodo = Redondear(rentaPorPeriodo);

            fila.ImpuestoCausado = Redondear(rentaPorPeriodo * fila.Periodos);
            fila.ImpuestoAPagar = fila.ImpuestoCausado;
            fila.RentaAcumulada = fila.ImpuestoAPagar;
        }

        // Los meses con décimo se resaltan completos (sus dos quincenas), como en la hoja.
        var mesesConDecimo = filas
            .Where(f => f.XiiiMes > 0m)
            .Select(f => MesDeLaFila(frecuencia, f.Quincena))
            .ToHashSet();

        // MESES solo se escribe en la primera fila de cada mes, como en la hoja.
        string? mesAnterior = null;
        foreach (var fila in filas)
        {
            var mes = MesDeLaFila(frecuencia, fila.Quincena);
            fila.EsMesDecimo = mesesConDecimo.Contains(mes);
            fila.Mes = mes == mesAnterior ? string.Empty : mes;
            mesAnterior = mes;
        }

        ficha.Filas = filas;
        ficha.TotalSalarios = Redondear(filas.Sum(f => f.Salarios));
        ficha.TotalVacaciones = Redondear(filas.Sum(f => f.Vacaciones));
        ficha.TotalExtras = Redondear(filas.Sum(f => f.Extras));
        ficha.TotalComision = Redondear(filas.Sum(f => f.Comision));
        ficha.TotalXiiiMes = Redondear(filas.Sum(f => f.XiiiMes));

        return ficha;
    }

    private static readonly string[] Meses =
    {
        "Enero", "Febrero", "Marzo", "Abril", "Mayo", "Junio",
        "Julio", "Agosto", "Septiembre", "Octubre", "Noviembre", "Diciembre"
    };

    /// <summary>Mes al que pertenece la fila n según la frecuencia.</summary>
    private static string MesDeLaFila(PayPeriodType frecuencia, int n) => frecuencia switch
    {
        PayPeriodType.Quincenal => Meses[(n - 1) / 2],
        PayPeriodType.Mensual => Meses[n - 1],
        // Semanal y bisemanal no caen limpias en meses; se aproxima por proporción.
        _ => Meses[Math.Min(11, (int)((n - 1) * 12m / MotorIsrPanama.ObtenerPeriodosAnuales(frecuencia)))]
    };

    /// <summary>
    /// Fila de la hoja a la que va una planilla, por la fecha de fin del período.
    /// Quincenal: mes y mitad (día 15 o antes es la primera quincena). Mensual: el mes.
    /// </summary>
    private static int IndiceDeFila(PayPeriodType frecuencia, DateTime finPeriodo)
    {
        var total = MotorIsrPanama.ObtenerPeriodosAnuales(frecuencia);
        return frecuencia switch
        {
            PayPeriodType.Quincenal => (finPeriodo.Month - 1) * 2 + (finPeriodo.Day <= 15 ? 1 : 2),
            PayPeriodType.Mensual => finPeriodo.Month,
            // Semanal / bisemanal: por posición en el año.
            _ => Math.Clamp((int)Math.Ceiling(finPeriodo.DayOfYear / (365m / total)), 1, total)
        };
    }

    /// <summary>Fila donde va una partida de décimo: la primera del mes en que se paga.</summary>
    private static int IndiceDeFilaDecimo(PayPeriodType frecuencia, DateTime fechaPago) => frecuencia switch
    {
        PayPeriodType.Quincenal => (fechaPago.Month - 1) * 2 + 1,
        PayPeriodType.Mensual => fechaPago.Month,
        _ => IndiceDeFila(frecuencia, fechaPago)
    };

    private static decimal Redondear(decimal v) => Math.Round(v, 2, MidpointRounding.AwayFromZero);

    /// <summary>
    /// Detalles de planilla regular del empleado en el año, sin las anuladas y
    /// sin la que se esté recalculando.
    /// </summary>
    private IQueryable<Domain.Entities.PayrollDetail> ConsultaRegulares(
        int empleadoId, int anio, int? excluirPayrollHeaderId)
    {
        var query = _context.PayrollDetails
            .AsNoTracking()
            .Where(d => d.EmpleadoId == empleadoId
                     && d.PayrollHeader!.PayDate.Year == anio
                     && d.PayrollHeader.Status != PayrollStatus.Cancelled);

        if (excluirPayrollHeaderId.HasValue)
            query = query.Where(d => d.PayrollHeaderId != excluirPayrollHeaderId.Value);

        return query;
    }

    /// <summary>Partidas de décimo del empleado en el año, sin la que se esté recalculando.</summary>
    private IQueryable<Domain.Entities.DetalleDecimo> ConsultaDecimos(
        int empleadoId, int anio, int? excluirPlanillaDecimoId)
    {
        var query = _context.DetallesDecimo
            .AsNoTracking()
            .Where(d => d.EmpleadoId == empleadoId
                     && d.PlanillaDecimo!.FechaPago.Year == anio);

        if (excluirPlanillaDecimoId.HasValue)
            query = query.Where(d => d.PlanillaDecimoId != excluirPlanillaDecimoId.Value);

        return query;
    }
}
