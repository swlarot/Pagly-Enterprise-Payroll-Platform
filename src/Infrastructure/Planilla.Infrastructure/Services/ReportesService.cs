// ====================================================================
// Planilla - ReportesService (Rediseño C-012)
// Actualizado: 2026-02-20
// 5 reportes: PlanillaRegular, Mensual, Acreedores, SIP, Comprobantes
// ====================================================================

using Microsoft.EntityFrameworkCore;
using Vorluno.Planilla.Application.DTOs.Reportes;
using Vorluno.Planilla.Application.Helpers;
using Vorluno.Planilla.Application.Interfaces;
using Vorluno.Planilla.Domain.Entities;
using Vorluno.Planilla.Domain.Enums;
using Vorluno.Planilla.Infrastructure.Data;

namespace Vorluno.Planilla.Infrastructure.Services;

public class ReportesService
{
    private readonly ApplicationDbContext _context;
    private readonly ITenantContext _tenantContext;

    public ReportesService(ApplicationDbContext context, ITenantContext tenantContext)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
    }

    private async Task<(string Nombre, string Ruc)> GetTenantInfo()
    {
        var tenant = await _tenantContext.GetCurrentTenantAsync();
        var nombre = tenant?.Name ?? "Sin nombre";
        var ruc = tenant != null && !string.IsNullOrEmpty(tenant.RUC) && !string.IsNullOrEmpty(tenant.DV)
            ? $"{tenant.RUC}-{tenant.DV}" : "Sin RUC";
        return (nombre, ruc);
    }

    private string GetEstadoTexto(PayrollStatus status) => status switch
    {
        PayrollStatus.Draft => "Borrador",
        PayrollStatus.Calculated => "Calculada",
        PayrollStatus.Approved => "Aprobada",
        PayrollStatus.Paid => "Pagada",
        _ => "Desconocido"
    };

    private static string? ResolveNombreAcreedor(DeduccionAplicada da)
    {
        return !string.IsNullOrWhiteSpace(da.NombreAcreedor)
            ? da.NombreAcreedor
            : da.DeduccionFija?.Acreedor?.Nombre ?? da.DeduccionFija?.NombreAcreedor;
    }

    /// <summary>Reporte 1: Planilla Regular — borrador operativo por período</summary>
    public async Task<ReportePlanillaRegularDto> GenerarReportePlanillaRegular(int planillaId)
    {
        var tenantId = _tenantContext.TenantId;

        // SEGURIDAD CRÍTICA: Filtrar por TenantId para aislar datos entre tenants
        var planilla = await _context.PayrollHeaders
            .Where(p => p.Id == planillaId && p.TenantId == tenantId)
            .Include(p => p.Details)
                .ThenInclude(d => d.Empleado)
            .Include(p => p.Details)
                .ThenInclude(d => d.DeduccionesAplicadas)
                    .ThenInclude(da => da.DeduccionFija)
                        .ThenInclude(df => df!.Acreedor)
            .FirstOrDefaultAsync();

        if (planilla == null)
            throw new InvalidOperationException($"Planilla {planillaId} no encontrada o no autorizada");

        // LEFT JOIN con PayrollEmployeeHours: empleados mensuales pueden no tenerlo
        var horasPorEmpleado = await _context.PayrollEmployeeHours
            .Where(h => h.PayrollHeaderId == planillaId && h.TenantId == tenantId)
            .ToListAsync();

        var (nombre, ruc) = await GetTenantInfo();

        var empleados = planilla.Details
            .Where(d => d.Empleado != null)
            .Select(d =>
            {
                var horas = horasPorEmpleado.FirstOrDefault(h => h.EmpleadoId == d.EmpleadoId);
                var horasExtra = horas != null
                    ? horas.OvertimeDayHours + horas.OvertimeNightHours + horas.OvertimeHolidayHours
                      + horas.OvertimeMixedHours + horas.OvertimeExcessHours
                    : 0m;
                var horasExtraExceso = d.HorasExtraExceso;
                var montoExceso = d.MontoHorasExtraExceso;

                var totalAcreedores = d.DeduccionesAplicadas.Sum(da => da.MontoAplicado);
                var razonLimitacion = d.TuvoLimitacionSalarioMinimo
                    ? d.DeduccionesAplicadas.FirstOrDefault(da => da.MontoLimitado > 0)?.RazonLimitacion
                    : null;

                var desglose = BuildDesgloseHoras(horas);

                return new EmpleadoPlanillaRegularItem(
                    d.Empleado!.NumeroIdentificacion,
                    $"{d.Empleado.Nombre} {d.Empleado.Apellido}",
                    horas?.RegularHours ?? 0,
                    horas?.SundayHours ?? 0,
                    horas?.HolidayHours ?? 0,
                    horasExtra,
                    horasExtraExceso,
                    montoExceso,
                    d.GrossPay,
                    d.CssEmployee,
                    d.EducationalInsuranceEmployee,
                    d.IncomeTax,
                    totalAcreedores,
                    d.PensionAlimenticia,
                    d.Embargos,
                    d.DeduccionesVoluntarias,
                    d.TotalDeductions,
                    d.NetPay,
                    d.TuvoLimitacionSalarioMinimo,
                    razonLimitacion,
                    desglose
                );
            })
            .OrderBy(e => e.NombreCompleto)
            .ToList();

        var totales = new TotalesPlanillaRegular(
            empleados.Count,
            empleados.Sum(e => e.SalarioBruto),
            empleados.Sum(e => e.CssEmpleado),
            empleados.Sum(e => e.SeEmpleado),
            empleados.Sum(e => e.Isr),
            empleados.Sum(e => e.TotalAcreedores),
            empleados.Sum(e => e.PensionAlimenticia),
            empleados.Sum(e => e.Embargos),
            empleados.Sum(e => e.DeduccionesVoluntarias),
            empleados.Sum(e => e.TotalDeducciones),
            empleados.Sum(e => e.SalarioNeto),
            empleados.Sum(e => e.HorasExtraExceso),
            empleados.Sum(e => e.MontoHorasExtraExceso)
        );

        return new ReportePlanillaRegularDto(
            nombre, ruc,
            planilla.PayrollNumber,
            $"{planilla.PeriodStartDate:dd/MM/yyyy} - {planilla.PeriodEndDate:dd/MM/yyyy}",
            planilla.PayDate,
            GetEstadoTexto(planilla.Status),
            empleados, totales,
            EsSinDeducciones: planilla.TipoPlanilla == TipoPlanilla.SinDeducciones
        );
    }

    /// <summary>Reporte 2: Mensual — consolida todas las planillas del mes</summary>
    public async Task<ReporteMensualDto> GenerarReporteMensual(int mes, int anio)
    {
        var tenantId = _tenantContext.TenantId;

        // SEGURIDAD CRÍTICA: Filtrar por TenantId para aislar datos entre tenants
        // DEV-112: Proyección directa en BD — evita cargar entidades completas en memoria
        var (nombre, ruc) = await GetTenantInfo();

        var periodosIncluidos = await _context.PayrollHeaders
            .Where(p => p.TenantId == tenantId
                && p.PeriodStartDate.Month == mes
                && p.PeriodStartDate.Year == anio
                && p.Status >= PayrollStatus.Calculated)
            .OrderBy(p => p.PeriodStartDate)
            .Select(p => p.PayrollNumber)
            .ToListAsync();

        // Proyección plana directa sobre PayrollDetails — sin Include de entidades completas
        var rows = await _context.PayrollHeaders
            .Where(p => p.TenantId == tenantId
                && p.PeriodStartDate.Month == mes
                && p.PeriodStartDate.Year == anio
                && p.Status >= PayrollStatus.Calculated)
            .SelectMany(p => p.Details
                .Where(d => d.Empleado != null)
                .Select(d => new
                {
                    d.EmpleadoId,
                    Cedula = d.Empleado!.NumeroIdentificacion,
                    Nombre = d.Empleado.Nombre + " " + d.Empleado.Apellido,
                    d.GrossPay,
                    d.CssEmployee,
                    d.EducationalInsuranceEmployee,
                    d.IncomeTax,
                    TotalAcreedores = d.DeduccionesAplicadas.Sum(da => da.MontoAplicado),
                    d.PensionAlimenticia,
                    d.Embargos,
                    d.DeduccionesVoluntarias,
                    d.TotalDeductions,
                    d.NetPay
                }))
            .ToListAsync();

        var empleados = rows
            .GroupBy(d => d.EmpleadoId)
            .Select(g =>
            {
                var primer = g.First();
                return new EmpleadoMensualItem(
                    primer.Cedula,
                    primer.Nombre,
                    g.Sum(d => d.GrossPay),
                    g.Sum(d => d.CssEmployee),
                    g.Sum(d => d.EducationalInsuranceEmployee),
                    g.Sum(d => d.IncomeTax),
                    g.Sum(d => d.TotalAcreedores),
                    g.Sum(d => d.PensionAlimenticia),
                    g.Sum(d => d.Embargos),
                    g.Sum(d => d.DeduccionesVoluntarias),
                    g.Sum(d => d.TotalDeductions),
                    g.Sum(d => d.NetPay)
                );
            })
            .OrderBy(e => e.NombreCompleto)
            .ToList();

        var nombresDesMeses = new[] { "", "Enero", "Febrero", "Marzo", "Abril", "Mayo", "Junio",
            "Julio", "Agosto", "Septiembre", "Octubre", "Noviembre", "Diciembre" };

        var totales = new TotalesMensual(
            empleados.Select(e => e.Cedula).Distinct().Count(),
            empleados.Sum(e => e.TotalBruto),
            empleados.Sum(e => e.TotalCss),
            empleados.Sum(e => e.TotalSe),
            empleados.Sum(e => e.TotalIsr),
            empleados.Sum(e => e.TotalAcreedores),
            empleados.Sum(e => e.TotalPensionAlimenticia),
            empleados.Sum(e => e.TotalEmbargos),
            empleados.Sum(e => e.TotalDeduccionesVoluntarias),
            empleados.Sum(e => e.TotalDeducciones),
            empleados.Sum(e => e.TotalNeto)
        );

        return new ReporteMensualDto(
            nombre, ruc,
            mes, anio,
            mes >= 1 && mes <= 12 ? nombresDesMeses[mes] : mes.ToString(),
            periodosIncluidos,
            empleados, totales
        );
    }

    /// <summary>Reporte 3: Acreedores — para que RRHH entregue a contabilidad</summary>
    public async Task<ReporteAcreedoresDto> GenerarReporteAcreedores(int planillaId)
    {
        var tenantId = _tenantContext.TenantId;

        // SEGURIDAD CRÍTICA: Filtrar por TenantId para aislar datos entre tenants
        var planilla = await _context.PayrollHeaders
            .Where(p => p.Id == planillaId && p.TenantId == tenantId)
            .Include(p => p.Details)
                .ThenInclude(d => d.Empleado)
            .Include(p => p.Details)
                .ThenInclude(d => d.DeduccionesAplicadas)
                    .ThenInclude(da => da.DeduccionFija)
                        .ThenInclude(df => df!.Acreedor)
            .FirstOrDefaultAsync();

        if (planilla == null)
            throw new InvalidOperationException($"Planilla {planillaId} no encontrada o no autorizada");

        var (nombre, ruc) = await GetTenantInfo();

        // Recopilar todas las deducciones de acreedores (excluir CSS/SE/ISR que no tienen NombreAcreedor)
        var todasDeducciones = planilla.Details
            .Where(d => d.Empleado != null)
            .SelectMany(d => d.DeduccionesAplicadas
                .Select(da => new
                {
                    EmpleadoNombre = $"{d.Empleado!.Nombre} {d.Empleado.Apellido}",
                    EmpleadoCedula = d.Empleado.NumeroIdentificacion,
                    NombreAcreedor = ResolveNombreAcreedor(da),
                    Acreedor = da.DeduccionFija?.Acreedor,
                    DeduccionFija = da.DeduccionFija,
                    da.TipoDeduccion,
                    da.Descripcion,
                    da.MontoSolicitado,
                    da.MontoAplicado,
                    da.MontoLimitado,
                    da.RazonLimitacion
                })
                .Where(x => !string.IsNullOrWhiteSpace(x.NombreAcreedor)))
            .ToList();

        var gruposAcreedor = todasDeducciones
            .GroupBy(x => x.NombreAcreedor!)
            .Select(g =>
            {
                var primeraConAcreedor = g.FirstOrDefault(x => x.Acreedor != null);
                var acreedor = primeraConAcreedor?.Acreedor;
                var primeraConDf = g.FirstOrDefault(x => x.DeduccionFija != null);
                var df = primeraConDf?.DeduccionFija;

                // Priorizar datos del catálogo Acreedor; fallback a campos embebidos en DeduccionFija
                var identificacion = acreedor?.Identificacion ?? df?.IdentificacionAcreedor;
                var banco = acreedor?.Banco ?? df?.BancoAcreedor;
                var numeroCuenta = acreedor?.NumeroCuenta ?? df?.CuentaBancariaAcreedor;
                var tipoAcreedor = acreedor?.TipoAcreedor.ToString();

                var detalle = g.Select(x => new EmpleadoAcreedorDetalle(
                    x.EmpleadoNombre,
                    x.EmpleadoCedula,
                    x.TipoDeduccion.ToString(),
                    x.Descripcion,
                    x.MontoSolicitado,
                    x.MontoAplicado,
                    x.MontoLimitado > 0,
                    x.RazonLimitacion
                )).ToList();

                return new AcreedorPagoItem(
                    g.Key,
                    tipoAcreedor,
                    identificacion,
                    banco,
                    numeroCuenta,
                    g.Sum(x => x.MontoAplicado),
                    g.Select(x => x.EmpleadoCedula).Distinct().Count(),
                    detalle
                );
            })
            .OrderByDescending(a => a.TotalATransferir)
            .ToList();

        return new ReporteAcreedoresDto(
            nombre, ruc,
            $"{planilla.PeriodStartDate:dd/MM/yyyy} - {planilla.PeriodEndDate:dd/MM/yyyy}",
            DateTimeHelper.NowPanama(),
            gruposAcreedor,
            gruposAcreedor.Sum(a => a.TotalATransferir),
            gruposAcreedor.Count
        );
    }

    /// <summary>Reporte 4: SIP — para la plataforma CSS de Panamá</summary>
    public async Task<ReporteSipDto> GenerarReporteSip(int planillaId)
    {
        var tenantId = _tenantContext.TenantId;

        // SEGURIDAD CRÍTICA: Filtrar por TenantId para aislar datos entre tenants
        var planilla = await _context.PayrollHeaders
            .Where(p => p.Id == planillaId && p.TenantId == tenantId)
            .Include(p => p.Details)
                .ThenInclude(d => d.Empleado)
            .FirstOrDefaultAsync();

        if (planilla == null)
            throw new InvalidOperationException($"Planilla {planillaId} no encontrada o no autorizada");

        var (nombre, ruc) = await GetTenantInfo();

        var empleados = planilla.Details
            .Where(d => d.Empleado != null)
            .Select(d =>
            {
                // Recalcular base CSS desde el monto employee (reversa del 9.75%)
                var baseCss = d.CssEmployee > 0 ? Math.Round(d.CssEmployee / PayrollConstants.CssTasaEmpleado, 2) : d.GrossPay;
                var totalSip = d.CssEmployee + d.CssEmployer
                    + d.EducationalInsuranceEmployee + d.EducationalInsuranceEmployer
                    + d.RiskContribution;

                return new EmpleadoSipItem(
                    d.Empleado!.NumeroIdentificacion,
                    $"{d.Empleado.Nombre} {d.Empleado.Apellido}",
                    d.GrossPay,
                    baseCss,
                    d.CssEmployee,
                    d.CssEmployer,
                    d.EducationalInsuranceEmployee,
                    d.EducationalInsuranceEmployer,
                    d.RiskContribution,
                    totalSip
                );
            })
            .OrderBy(e => e.NombreCompleto)
            .ToList();

        var totales = new TotalesSip(
            empleados.Sum(e => e.SalarioBruto),
            empleados.Sum(e => e.BaseCss),
            empleados.Sum(e => e.CssEmpleado),
            empleados.Sum(e => e.CssPatronal),
            empleados.Sum(e => e.SeEmpleado),
            empleados.Sum(e => e.SePatronal),
            empleados.Sum(e => e.RiesgoProfesional),
            empleados.Sum(e => e.TotalSip)
        );

        return new ReporteSipDto(
            nombre, ruc,
            planilla.PayrollNumber,
            $"{planilla.PeriodStartDate:dd/MM/yyyy} - {planilla.PeriodEndDate:dd/MM/yyyy}",
            DateTimeHelper.NowPanama(),
            empleados, totales
        );
    }

    /// <summary>Reporte 5: Comprobantes de Pago — recibos individuales por empleado</summary>
    public async Task<ReporteComprobantesDto> GenerarReporteComprobantes(int planillaId)
    {
        var tenantId = _tenantContext.TenantId;

        // SEGURIDAD CRÍTICA: Filtrar por TenantId para aislar datos entre tenants
        var planilla = await _context.PayrollHeaders
            .Where(p => p.Id == planillaId && p.TenantId == tenantId)
            .Include(p => p.Details)
                .ThenInclude(d => d.Empleado)
                    .ThenInclude(e => e!.Posicion)
            .Include(p => p.Details)
                .ThenInclude(d => d.Empleado)
                    .ThenInclude(e => e!.Departamento)
            .Include(p => p.Details)
                .ThenInclude(d => d.DeduccionesAplicadas)
                    .ThenInclude(da => da.DeduccionFija)
                        .ThenInclude(df => df!.Acreedor)
            .FirstOrDefaultAsync();

        if (planilla == null)
            throw new InvalidOperationException($"Planilla {planillaId} no encontrada o no autorizada");

        // LEFT JOIN con horas: empleados mensuales pueden no tener registro de horas
        var horasPorEmpleado = await _context.PayrollEmployeeHours
            .Where(h => h.PayrollHeaderId == planillaId && h.TenantId == tenantId)
            .ToListAsync();

        var (nombre, ruc) = await GetTenantInfo();

        var comprobantes = planilla.Details
            .Where(d => d.Empleado != null)
            .Select(d =>
            {
                var horas = horasPorEmpleado.FirstOrDefault(h => h.EmpleadoId == d.EmpleadoId);

                var lineasAcreedores = d.DeduccionesAplicadas
                    .Select(da => new
                    {
                        Deduccion = da,
                        NombreAcreedor = ResolveNombreAcreedor(da)
                    })
                    .Where(x => !string.IsNullOrWhiteSpace(x.NombreAcreedor))
                    .Select(x => x.Deduccion)
                    .OrderBy(da => da.OrdenAplicacion)
                    .Select(da => new LineaDeduccionComprobante(
                        ResolveNombreAcreedor(da) ?? "Sin nombre",
                        da.TipoDeduccion.ToString(),
                        da.Descripcion,
                        da.MontoAplicado
                    ))
                    .ToList();

                var totalAcreedores = lineasAcreedores.Sum(l => l.Monto);

                // Agregar líneas de deducción para pensión alimenticia, embargos y voluntarias
                var lineasDeduccionesExtra = new List<LineaDeduccionComprobante>();
                if (d.PensionAlimenticia > 0)
                    lineasDeduccionesExtra.Add(new LineaDeduccionComprobante("Pensión Alimenticia", "PensionAlimenticia", "Pensión alimenticia", d.PensionAlimenticia));
                if (d.Embargos > 0)
                    lineasDeduccionesExtra.Add(new LineaDeduccionComprobante("Embargos", "Embargo", "Embargos judiciales", d.Embargos));
                if (d.DeduccionesVoluntarias > 0)
                    lineasDeduccionesExtra.Add(new LineaDeduccionComprobante("Deducciones Voluntarias", "Voluntaria", "Deducciones voluntarias", d.DeduccionesVoluntarias));

                var todasLineasAcreedores = lineasAcreedores.Concat(lineasDeduccionesExtra).ToList();
                var totalTodasAcreedores = todasLineasAcreedores.Sum(l => l.Monto);

                // Reserva décimo = 1/12 del salario bruto (referencial, no deducción)
                var reservaDecimo = Math.Round(d.GrossPay / 12m, 2);

                return new ComprobanteEmpleado(
                    d.Empleado!.NumeroIdentificacion,
                    $"{d.Empleado.Nombre} {d.Empleado.Apellido}",
                    d.Empleado.Posicion?.Nombre,
                    d.Empleado.Departamento?.Nombre,
                    horas?.RegularPay ?? d.BaseSalary, // DEV-89: pago real del período; fallback a salario base si no hay horas
                    horas?.SundayPay ?? 0,
                    horas?.HolidayPay ?? 0,
                    d.MontoHorasExtra,
                    d.GrossPay,
                    d.CssEmployee,
                    d.EducationalInsuranceEmployee,
                    d.IncomeTax,
                    d.PensionAlimenticia,
                    d.Embargos,
                    d.DeduccionesVoluntarias,
                    todasLineasAcreedores,
                    totalTodasAcreedores,
                    d.TotalDeductions,
                    d.NetPay,
                    reservaDecimo,
                    d.TuvoLimitacionSalarioMinimo
                );
            })
            .OrderBy(c => c.NombreCompleto)
            .ToList();

        return new ReporteComprobantesDto(
            nombre, ruc,
            planilla.PayrollNumber,
            $"{planilla.PeriodStartDate:dd/MM/yyyy} - {planilla.PeriodEndDate:dd/MM/yyyy}",
            planilla.PayDate,
            comprobantes,
            EsSinDeducciones: planilla.TipoPlanilla == TipoPlanilla.SinDeducciones
        );
    }

    private static List<LineaDesgloseHoras> BuildDesgloseHoras(Domain.Entities.PayrollEmployeeHours? horas)
    {
        if (horas == null) return [];

        var lineas = new List<LineaDesgloseHoras>();

        void Agregar(string concepto, decimal h, decimal pay)
        {
            if (h <= 0 || pay <= 0) return;
            var tarifa = Math.Round(pay / h, 4);
            lineas.Add(new LineaDesgloseHoras(concepto, h, tarifa, pay));
        }

        Agregar("Horas Regulares", horas.RegularHours, horas.RegularPay);
        Agregar("Recargo Domingo", horas.SundayHours, horas.SundayPay);
        Agregar("Recargo Feriado", horas.HolidayHours, horas.HolidayPay);
        Agregar("H. Extra Diurnas", horas.OvertimeDayHours, horas.OvertimeDayPay);
        Agregar("H. Extra Nocturnas", horas.OvertimeNightHours, horas.OvertimeNightPay);
        Agregar("H. Extra Feriado", horas.OvertimeHolidayHours, horas.OvertimeHolidayPay);
        Agregar("H. Extra Mixtas", horas.OvertimeMixedHours, horas.OvertimeMixedPay);
        Agregar("H. Extra Excedentes", horas.OvertimeExcessHours, horas.OvertimeExcessPay);

        return lineas;
    }

    // ====================================================================
    // REPORTE 6: Desglose Décimo Tercer Mes (DEV-171)
    // ====================================================================

    public async Task<ReporteDesgloseDecimoDto> GenerarReporteDesgloseDecimo(int planillaDecimoId)
    {
        var tenantId = _tenantContext.TenantId;
        var (nombre, ruc) = await GetTenantInfo();

        var planilla = await _context.PlanillasDecimo
            .Include(p => p.Detalles)
                .ThenInclude(d => d.Empleado)
            .FirstOrDefaultAsync(p => p.Id == planillaDecimoId && p.TenantId == tenantId)
            ?? throw new InvalidOperationException($"Planilla de décimo {planillaDecimoId} no encontrada");

        // Query batch: un solo viaje a BD para todos los PayrollDetails del período
        var empleadoIds = planilla.Detalles.Select(d => d.EmpleadoId).ToList();

        var allDetails = await _context.PayrollDetails
            .Include(d => d.PayrollHeader)
            .Where(d => d.TenantId == tenantId
                && empleadoIds.Contains(d.EmpleadoId)
                && d.PayrollHeader.PeriodStartDate >= planilla.PeriodoDesde
                && d.PayrollHeader.PeriodEndDate <= planilla.PeriodoHasta
                && (d.PayrollHeader.Status == PayrollStatus.Approved
                    || d.PayrollHeader.Status == PayrollStatus.Paid))
            .OrderBy(d => d.EmpleadoId)
                .ThenBy(d => d.PayrollHeader.PeriodStartDate)
            .ToListAsync();

        var detailsPorEmpleado = allDetails
            .GroupBy(d => d.EmpleadoId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var partida = planilla.FechaPago.Month switch
        {
            4 => 1,
            8 => 2,
            12 => 3,
            _ => 0
        };

        var empleadoItems = planilla.Detalles
            .OrderBy(d => d.Empleado.Apellido)
                .ThenBy(d => d.Empleado.Nombre)
            .Select(detalle =>
            {
                var periodos = detailsPorEmpleado.TryGetValue(detalle.EmpleadoId, out var dets)
                    ? dets.Select(d => new PeriodoPagoDecimoItem(
                        d.PayrollHeader.PayrollNumber,
                        d.PayrollHeader.PeriodStartDate,
                        d.PayrollHeader.PeriodEndDate,
                        GetTipoPeriodoTexto(d.PayrollHeader.PayPeriodType),
                        d.GrossPay
                    )).ToList()
                    : new List<PeriodoPagoDecimoItem>();

                return new EmpleadoDesgloseDecimoItem(
                    detalle.Empleado.NumeroIdentificacion ?? "—",
                    $"{detalle.Empleado.Nombre} {detalle.Empleado.Apellido}".Trim(),
                    periodos,
                    detalle.TotalDevengado,
                    detalle.MontoDecimo,
                    detalle.CssEmpleado,
                    detalle.SeEmpleado,
                    detalle.ISR,
                    detalle.TotalDeducciones,
                    detalle.NetoPago
                );
            }).ToList();

        var totales = new TotalesDesgloseDecimo(
            empleadoItems.Count,
            planilla.TotalDevengado,
            planilla.TotalDecimo,
            planilla.TotalCssEmpleado,
            planilla.TotalSeEmpleado,
            planilla.TotalISR,
            planilla.TotalCssEmpleado + planilla.TotalSeEmpleado + planilla.TotalISR,
            planilla.TotalNetoPago
        );

        return new ReporteDesgloseDecimoDto(
            nombre, ruc,
            planilla.Numero,
            $"{planilla.PeriodoDesde:dd/MM/yyyy} - {planilla.PeriodoHasta:dd/MM/yyyy}",
            planilla.FechaPago,
            planilla.Estado.ToString(),
            partida,
            empleadoItems,
            totales
        );
    }

    private static string GetTipoPeriodoTexto(PayPeriodType type) => type switch
    {
        PayPeriodType.Quincenal => "Quincenal",
        PayPeriodType.Mensual => "Mensual",
        PayPeriodType.Semanal => "Semanal",
        PayPeriodType.Bisemanal => "Bisemanal",
        _ => type.ToString()
    };
}
