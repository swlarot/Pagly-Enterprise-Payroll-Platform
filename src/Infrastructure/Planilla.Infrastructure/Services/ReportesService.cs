// ====================================================================
// Planilla - ReportesService
// Creado: 2025-12-28
// Descripción: Servicio para generar reportes de planilla
// (CSS, Seguro Educativo, ISR, Planilla Detallada)
// ====================================================================

using Microsoft.EntityFrameworkCore;
using Vorluno.Planilla.Application.DTOs.Reportes;
using Vorluno.Planilla.Application.Interfaces;
using Vorluno.Planilla.Domain.Enums;
using Vorluno.Planilla.Infrastructure.Data;

namespace Vorluno.Planilla.Infrastructure.Services;

/// <summary>
/// Servicio para generar diferentes tipos de reportes de planilla
/// </summary>
public class ReportesService
{
    private readonly ApplicationDbContext _context;
    private readonly ITenantContext _tenantContext;

    public ReportesService(
        ApplicationDbContext context,
        ITenantContext tenantContext)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
    }

    /// <summary>
    /// Genera el reporte de CSS (Caja de Seguro Social)
    /// </summary>
    public async Task<ReporteCssDto> GenerarReporteCss(int planillaId)
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

        // Obtener información del tenant para datos de la empresa
        var tenant = await _tenantContext.GetCurrentTenantAsync();

        var empleados = planilla.Details
            .Where(d => d.Empleado != null)
            .Select(d => {
                // Calcular base CSS real (reversa del cálculo de 9.75%)
                // El orquestador ya aplicó los topes correctos según Ley 462
                decimal baseCss = d.CssEmployee > 0 ? d.CssEmployee / 0.0975m : 0;

                return new EmpleadoCssDto(
                    d.Empleado!.NumeroIdentificacion,
                    $"{d.Empleado.Nombre} {d.Empleado.Apellido}",
                    d.GrossPay,
                    baseCss, // Base CSS real con topes aplicados por el orquestador
                    d.CssEmployee,
                    d.CssEmployer,
                    d.RiskContribution,
                    d.CssEmployee + d.CssEmployer + d.RiskContribution
                );
            })
            .OrderBy(e => e.NombreCompleto)
            .ToList();

        var totales = new TotalesCssDto(
            empleados.Sum(e => e.SalarioBruto),
            empleados.Sum(e => e.CssEmpleado),
            empleados.Sum(e => e.CssPatrono),
            empleados.Sum(e => e.RiesgoProfesional),
            empleados.Sum(e => e.TotalCss)
        );

        return new ReporteCssDto(
            tenant?.Name ?? "Sin nombre",
            tenant != null && !string.IsNullOrEmpty(tenant.RUC) && !string.IsNullOrEmpty(tenant.DV)
                ? $"{tenant.RUC}-{tenant.DV}"
                : "Sin RUC",
            $"{planilla.PeriodStartDate:dd/MM/yyyy} - {planilla.PeriodEndDate:dd/MM/yyyy}",
            DateTime.Now,
            empleados,
            totales
        );
    }

    /// <summary>
    /// Genera el reporte de Seguro Educativo
    /// </summary>
    public async Task<ReporteSeDto> GenerarReporteSe(int planillaId)
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

        // Obtener información del tenant para datos de la empresa
        var tenant = await _tenantContext.GetCurrentTenantAsync();

        var empleados = planilla.Details
            .Where(d => d.Empleado != null)
            .Select(d => new EmpleadoSeDto(
                d.Empleado!.NumeroIdentificacion,
                $"{d.Empleado.Nombre} {d.Empleado.Apellido}",
                d.GrossPay,
                d.EducationalInsuranceEmployee,
                d.EducationalInsuranceEmployer,
                d.EducationalInsuranceEmployee + d.EducationalInsuranceEmployer
            ))
            .OrderBy(e => e.NombreCompleto)
            .ToList();

        var totales = new TotalesSeDto(
            empleados.Sum(e => e.SalarioBruto),
            empleados.Sum(e => e.SeEmpleado),
            empleados.Sum(e => e.SePatrono),
            empleados.Sum(e => e.TotalSe)
        );

        return new ReporteSeDto(
            tenant?.Name ?? "Sin nombre",
            tenant != null && !string.IsNullOrEmpty(tenant.RUC) && !string.IsNullOrEmpty(tenant.DV)
                ? $"{tenant.RUC}-{tenant.DV}"
                : "Sin RUC",
            $"{planilla.PeriodStartDate:dd/MM/yyyy} - {planilla.PeriodEndDate:dd/MM/yyyy}",
            DateTime.Now,
            empleados,
            totales
        );
    }

    /// <summary>
    /// Genera el reporte de ISR (Impuesto Sobre la Renta)
    /// </summary>
    public async Task<ReporteIsrDto> GenerarReporteIsr(int planillaId)
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

        // Obtener información del tenant para datos de la empresa
        var tenant = await _tenantContext.GetCurrentTenantAsync();

        var empleados = planilla.Details
            .Where(d => d.Empleado != null)
            .Select(d => {
                // Proyección anual (asumiendo quincenal: 24 períodos)
                decimal ingresoAnualProyectado = d.GrossPay * 24;
                int dependientes = d.Empleado!.Dependents; // Obtener dependientes reales del empleado
                decimal deduccionDependientes = dependientes * 800m; // $800 por dependiente

                return new EmpleadoIsrDto(
                    d.Empleado!.NumeroIdentificacion,
                    $"{d.Empleado.Nombre} {d.Empleado.Apellido}",
                    ingresoAnualProyectado,
                    dependientes,
                    deduccionDependientes,
                    Math.Max(0, ingresoAnualProyectado - deduccionDependientes),
                    d.IncomeTax * 24, // ISR anual proyectado
                    d.IncomeTax
                );
            })
            .OrderBy(e => e.NombreCompleto)
            .ToList();

        var totales = new TotalesIsrDto(
            empleados.Sum(e => e.IngresoAnualProyectado),
            empleados.Sum(e => e.DeduccionDependientes),
            empleados.Sum(e => e.IsrAnual),
            empleados.Sum(e => e.IsrPeriodo)
        );

        return new ReporteIsrDto(
            tenant?.Name ?? "Sin nombre",
            tenant != null && !string.IsNullOrEmpty(tenant.RUC) && !string.IsNullOrEmpty(tenant.DV)
                ? $"{tenant.RUC}-{tenant.DV}"
                : "Sin RUC",
            $"{planilla.PeriodStartDate:dd/MM/yyyy} - {planilla.PeriodEndDate:dd/MM/yyyy}",
            planilla.PeriodStartDate.Year,
            DateTime.Now,
            empleados,
            totales
        );
    }

    /// <summary>
    /// Genera el reporte de planilla detallado completo
    /// </summary>
    public async Task<ReportePlanillaDetalladoDto> GenerarReportePlanillaDetallada(int planillaId)
    {
        var tenantId = _tenantContext.TenantId;

        // SEGURIDAD CRÍTICA: Filtrar por TenantId para aislar datos entre tenants
        var planilla = await _context.PayrollHeaders
            .Where(p => p.Id == planillaId && p.TenantId == tenantId)
            .Include(p => p.Details)
                .ThenInclude(d => d.Empleado)
                    .ThenInclude(e => e!.Departamento)
            .Include(p => p.Details)
                .ThenInclude(d => d.Empleado)
                    .ThenInclude(e => e!.Posicion)
            .FirstOrDefaultAsync();

        if (planilla == null)
            throw new InvalidOperationException($"Planilla {planillaId} no encontrada o no autorizada");

        // Obtener información del tenant para datos de la empresa
        var tenant = await _tenantContext.GetCurrentTenantAsync();

        var empleados = planilla.Details
            .Where(d => d.Empleado != null)
            .Select(d => new EmpleadoPlanillaDetalladoDto(
                d.Empleado!.NumeroIdentificacion,
                $"{d.Empleado.Nombre} {d.Empleado.Apellido}",
                d.Empleado.Departamento?.Nombre,
                d.Empleado.Posicion?.Nombre,

                // Ingresos
                d.BaseSalary,
                d.MontoHorasExtra,
                d.Bonuses,
                d.GrossPay,

                // Deducciones
                d.CssEmployee,
                d.EducationalInsuranceEmployee,
                d.IncomeTax,
                d.Prestamos,
                d.Anticipos,
                d.DeduccionesFijas,
                d.MontoDescuentoAusencias,
                d.OtherDeductions,
                d.TotalDeductions,

                // Neto
                d.NetPay,

                // Costos patronales
                d.CssEmployer,
                d.EducationalInsuranceEmployer,
                d.RiskContribution,
                d.EmployerCost
            ))
            .OrderBy(e => e.Departamento)
            .ThenBy(e => e.NombreCompleto)
            .ToList();

        // Resumen por departamento
        var resumenPorDepartamento = empleados
            .Where(e => !string.IsNullOrEmpty(e.Departamento))
            .GroupBy(e => e.Departamento!)
            .Select(g => new ResumenDepartamentoDto(
                g.Key,
                g.Count(),
                g.Sum(e => e.SalarioBruto),
                g.Sum(e => e.TotalDeducciones),
                g.Sum(e => e.SalarioNeto),
                g.Sum(e => e.CostoPatronal)
            ))
            .OrderBy(r => r.NombreDepartamento)
            .ToList();

        var estadoTexto = planilla.Status switch
        {
            PayrollStatus.Draft => "Borrador",
            PayrollStatus.Calculated => "Calculada",
            PayrollStatus.Approved => "Aprobada",
            PayrollStatus.Paid => "Pagada",
            _ => "Desconocido"
        };

        return new ReportePlanillaDetalladoDto(
            // Encabezado
            tenant?.Name ?? "Sin nombre",
            tenant != null && !string.IsNullOrEmpty(tenant.RUC) && !string.IsNullOrEmpty(tenant.DV)
                ? $"{tenant.RUC}-{tenant.DV}"
                : "Sin RUC",
            $"PL-{planilla.Id:D6}",
            $"{planilla.PeriodStartDate:dd/MM/yyyy} - {planilla.PeriodEndDate:dd/MM/yyyy}",
            planilla.PayDate,
            estadoTexto,
            DateTime.Now,

            // Resumen
            empleados.Count,
            empleados.Sum(e => e.SalarioBruto),
            empleados.Sum(e => e.TotalDeducciones),
            empleados.Sum(e => e.SalarioNeto),
            empleados.Sum(e => e.CostoPatronal),

            // Detalle
            empleados,

            // Por departamento
            resumenPorDepartamento.Any() ? resumenPorDepartamento : null
        );
    }

    /// <summary>
    /// Genera el reporte detallado de horas extra de una planilla
    /// </summary>
    public async Task<ReporteHorasExtraDto> GenerarReporteHorasExtra(int planillaId)
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

        // Obtener información del tenant para datos de la empresa
        var tenant = await _tenantContext.GetCurrentTenantAsync();

        // Obtener horas extra aprobadas del período de la planilla
        var horasExtra = await _context.HorasExtra
            .Where(h => h.TenantId == tenantId
                && h.EstaAprobada
                && h.Fecha >= planilla.PeriodStartDate
                && h.Fecha <= planilla.PeriodEndDate)
            .Include(h => h.Empleado)
            .ToListAsync();

        // Agrupar por empleado
        var empleados = planilla.Details
            .Where(d => d.Empleado != null)
            .Select(d => {
                var horasDelEmpleado = horasExtra.Where(h => h.EmpleadoId == d.EmpleadoId).ToList();

                decimal horasDiurnas = horasDelEmpleado
                    .Where(h => h.TipoHoraExtra == TipoHoraExtra.Diurna)
                    .Sum(h => h.CantidadHoras);

                decimal horasNocturnas = horasDelEmpleado
                    .Where(h => h.TipoHoraExtra == TipoHoraExtra.Nocturna)
                    .Sum(h => h.CantidadHoras);

                decimal horasDomingoFeriado = horasDelEmpleado
                    .Where(h => h.TipoHoraExtra == TipoHoraExtra.DomingoFeriado || h.TipoHoraExtra == TipoHoraExtra.NocturnaDomingoFeriado)
                    .Sum(h => h.CantidadHoras);

                decimal horasFestivos = horasDelEmpleado
                    .Where(h => h.TipoHoraExtra == TipoHoraExtra.FiestaNacionalDiurna || h.TipoHoraExtra == TipoHoraExtra.FiestaNacionalNocturna)
                    .Sum(h => h.CantidadHoras);

                decimal horasMixtas = horasDelEmpleado
                    .Where(h => h.TipoHoraExtra == TipoHoraExtra.MixtaDiurnaNocturna || h.TipoHoraExtra == TipoHoraExtra.MixtaNocturnaDiurna)
                    .Sum(h => h.CantidadHoras);

                decimal horasExceso = horasDelEmpleado
                    .Where(h => h.EsExceso)
                    .Sum(h => h.CantidadHoras);

                decimal totalHoras = horasDelEmpleado.Sum(h => h.CantidadHoras);

                decimal montoDiurnas = horasDelEmpleado
                    .Where(h => h.TipoHoraExtra == TipoHoraExtra.Diurna)
                    .Sum(h => h.MontoCalculado ?? 0);

                decimal montoNocturnas = horasDelEmpleado
                    .Where(h => h.TipoHoraExtra == TipoHoraExtra.Nocturna)
                    .Sum(h => h.MontoCalculado ?? 0);

                decimal montoDomingoFeriado = horasDelEmpleado
                    .Where(h => h.TipoHoraExtra == TipoHoraExtra.DomingoFeriado || h.TipoHoraExtra == TipoHoraExtra.NocturnaDomingoFeriado)
                    .Sum(h => h.MontoCalculado ?? 0);

                decimal montoFestivos = horasDelEmpleado
                    .Where(h => h.TipoHoraExtra == TipoHoraExtra.FiestaNacionalDiurna || h.TipoHoraExtra == TipoHoraExtra.FiestaNacionalNocturna)
                    .Sum(h => h.MontoCalculado ?? 0);

                decimal montoMixtas = horasDelEmpleado
                    .Where(h => h.TipoHoraExtra == TipoHoraExtra.MixtaDiurnaNocturna || h.TipoHoraExtra == TipoHoraExtra.MixtaNocturnaDiurna)
                    .Sum(h => h.MontoCalculado ?? 0);

                decimal montoExceso = horasDelEmpleado
                    .Where(h => h.EsExceso)
                    .Sum(h => h.MontoCalculado ?? 0);

                decimal montoTotal = horasDelEmpleado.Sum(h => h.MontoCalculado ?? 0);

                return new EmpleadoHorasExtraDto(
                    d.Empleado!.NumeroIdentificacion,
                    $"{d.Empleado.Nombre} {d.Empleado.Apellido}",
                    horasDiurnas,
                    horasNocturnas,
                    horasDomingoFeriado,
                    horasFestivos,
                    horasMixtas,
                    horasExceso,
                    totalHoras,
                    montoDiurnas,
                    montoNocturnas,
                    montoDomingoFeriado,
                    montoFestivos,
                    montoMixtas,
                    montoExceso,
                    montoTotal
                );
            })
            .Where(e => e.TotalHoras > 0) // Solo empleados con horas extra
            .OrderBy(e => e.NombreCompleto)
            .ToList();

        var totales = new TotalesHorasExtraDto(
            empleados.Sum(e => e.HorasDiurnas),
            empleados.Sum(e => e.HorasNocturnas),
            empleados.Sum(e => e.HorasDomingoFeriado),
            empleados.Sum(e => e.HorasFestivos),
            empleados.Sum(e => e.HorasMixtas),
            empleados.Sum(e => e.HorasExceso),
            empleados.Sum(e => e.TotalHoras),
            empleados.Sum(e => e.MontoDiurnas),
            empleados.Sum(e => e.MontoNocturnas),
            empleados.Sum(e => e.MontoDomingoFeriado),
            empleados.Sum(e => e.MontoFestivos),
            empleados.Sum(e => e.MontoMixtas),
            empleados.Sum(e => e.MontoExceso),
            empleados.Sum(e => e.MontoTotal)
        );

        return new ReporteHorasExtraDto(
            tenant?.Name ?? "Sin nombre",
            tenant != null && !string.IsNullOrEmpty(tenant.RUC) && !string.IsNullOrEmpty(tenant.DV)
                ? $"{tenant.RUC}-{tenant.DV}"
                : "Sin RUC",
            $"{planilla.PeriodStartDate:dd/MM/yyyy} - {planilla.PeriodEndDate:dd/MM/yyyy}",
            DateTime.Now,
            empleados,
            totales
        );
    }
}
