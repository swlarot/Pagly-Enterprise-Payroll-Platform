// ====================================================================
// Planilla - ReportesService (Rediseño C-012)
// Actualizado: 2026-02-20
// 5 reportes: PlanillaRegular, Mensual, Acreedores, SIP, Comprobantes
// ====================================================================

using Microsoft.EntityFrameworkCore;
using Vorluno.Planilla.Application.DTOs.Reportes;
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

                var totalAcreedores = d.DeduccionesAplicadas.Sum(da => da.MontoAplicado);
                var razonLimitacion = d.TuvoLimitacionSalarioMinimo
                    ? d.DeduccionesAplicadas.FirstOrDefault(da => da.MontoLimitado > 0)?.RazonLimitacion
                    : null;

                return new EmpleadoPlanillaRegularItem(
                    d.Empleado!.NumeroIdentificacion,
                    $"{d.Empleado.Nombre} {d.Empleado.Apellido}",
                    horas?.RegularHours ?? 0,
                    horas?.SundayHours ?? 0,
                    horas?.HolidayHours ?? 0,
                    horasExtra,
                    d.GrossPay,
                    d.CssEmployee,
                    d.EducationalInsuranceEmployee,
                    d.IncomeTax,
                    totalAcreedores,
                    d.NetPay,
                    d.TuvoLimitacionSalarioMinimo,
                    razonLimitacion
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
            empleados.Sum(e => e.SalarioNeto)
        );

        return new ReportePlanillaRegularDto(
            nombre, ruc,
            planilla.PayrollNumber,
            $"{planilla.PeriodStartDate:dd/MM/yyyy} - {planilla.PeriodEndDate:dd/MM/yyyy}",
            planilla.PayDate,
            GetEstadoTexto(planilla.Status),
            empleados, totales
        );
    }

    /// <summary>Reporte 2: Mensual — consolida todas las planillas del mes</summary>
    public async Task<ReporteMensualDto> GenerarReporteMensual(int mes, int anio)
    {
        var tenantId = _tenantContext.TenantId;

        // SEGURIDAD CRÍTICA: Filtrar por TenantId para aislar datos entre tenants
        var planillas = await _context.PayrollHeaders
            .Where(p => p.TenantId == tenantId
                && p.PeriodStartDate.Month == mes
                && p.PeriodStartDate.Year == anio
                && p.Status >= PayrollStatus.Calculated)
            .Include(p => p.Details)
                .ThenInclude(d => d.Empleado)
            .Include(p => p.Details)
                .ThenInclude(d => d.DeduccionesAplicadas)
            .ToListAsync();

        var (nombre, ruc) = await GetTenantInfo();

        var periodosIncluidos = planillas
            .OrderBy(p => p.PeriodStartDate)
            .Select(p => p.PayrollNumber)
            .ToList();

        // Agrupar todos los details por empleado y sumar
        var todosDetails = planillas.SelectMany(p => p.Details).ToList();

        var empleados = todosDetails
            .Where(d => d.Empleado != null)
            .GroupBy(d => d.EmpleadoId)
            .Select(g =>
            {
                var primer = g.First();
                return new EmpleadoMensualItem(
                    primer.Empleado!.NumeroIdentificacion,
                    $"{primer.Empleado.Nombre} {primer.Empleado.Apellido}",
                    g.Sum(d => d.GrossPay),
                    g.Sum(d => d.CssEmployee),
                    g.Sum(d => d.EducationalInsuranceEmployee),
                    g.Sum(d => d.IncomeTax),
                    g.Sum(d => d.DeduccionesAplicadas.Sum(da => da.MontoAplicado)),
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
                .Where(da => !string.IsNullOrEmpty(da.NombreAcreedor))
                .Select(da => new
                {
                    EmpleadoNombre = $"{d.Empleado!.Nombre} {d.Empleado.Apellido}",
                    EmpleadoCedula = d.Empleado.NumeroIdentificacion,
                    da.NombreAcreedor,
                    Acreedor = da.DeduccionFija?.Acreedor,
                    DeduccionFija = da.DeduccionFija,
                    da.TipoDeduccion,
                    da.Descripcion,
                    da.MontoSolicitado,
                    da.MontoAplicado,
                    da.MontoLimitado,
                    da.RazonLimitacion
                }))
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
            DateTime.Now,
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
                var baseCss = d.CssEmployee > 0 ? Math.Round(d.CssEmployee / 0.0975m, 2) : d.GrossPay;
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
            DateTime.Now,
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
                    .Where(da => !string.IsNullOrEmpty(da.NombreAcreedor))
                    .OrderBy(da => da.OrdenAplicacion)
                    .Select(da => new LineaDeduccionComprobante(
                        da.NombreAcreedor ?? "Sin nombre",
                        da.TipoDeduccion.ToString(),
                        da.Descripcion,
                        da.MontoAplicado
                    ))
                    .ToList();

                var totalAcreedores = lineasAcreedores.Sum(l => l.Monto);

                // Reserva décimo = 1/12 del salario bruto (referencial, no deducción)
                var reservaDecimo = Math.Round(d.GrossPay / 12m, 2);

                return new ComprobanteEmpleado(
                    d.Empleado!.NumeroIdentificacion,
                    $"{d.Empleado.Nombre} {d.Empleado.Apellido}",
                    d.Empleado.Posicion?.Nombre,
                    d.Empleado.Departamento?.Nombre,
                    d.BaseSalary,
                    horas?.SundayPay ?? 0,
                    horas?.HolidayPay ?? 0,
                    d.MontoHorasExtra,
                    d.GrossPay,
                    d.CssEmployee,
                    d.EducationalInsuranceEmployee,
                    d.IncomeTax,
                    lineasAcreedores,
                    totalAcreedores,
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
            comprobantes
        );
    }
}
