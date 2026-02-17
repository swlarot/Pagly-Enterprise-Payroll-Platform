// ====================================================================
// Planilla - ReportesController
// Creado: 2025-12-28
// Descripción: Controller para generar y exportar reportes de planilla
// ====================================================================

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vorluno.Planilla.Application.DTOs.Reportes;
using Vorluno.Planilla.Application.Interfaces;
using Vorluno.Planilla.Infrastructure.Services;
using Vorluno.Planilla.Web.Filters;

namespace Vorluno.Planilla.Web.Controllers;

/// <summary>
/// Controller para gestionar reportes de planilla
/// </summary>
[Authorize]
[ApiController]
[Route("api/[controller]")]
public class ReportesController : ControllerBase
{
    private readonly ReportesService _reportesService;
    private readonly ExportacionService _exportacionService;
    private readonly ITenantContext _tenantContext;
    private readonly IPlanLimitService _planLimitService;

    public ReportesController(
        ReportesService reportesService,
        ExportacionService exportacionService,
        ITenantContext tenantContext,
        IPlanLimitService planLimitService)
    {
        _reportesService = reportesService ?? throw new ArgumentNullException(nameof(reportesService));
        _exportacionService = exportacionService ?? throw new ArgumentNullException(nameof(exportacionService));
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _planLimitService = planLimitService ?? throw new ArgumentNullException(nameof(planLimitService));
    }

    #region Visualización JSON

    /// <summary>
    /// Obtiene el reporte de CSS en formato JSON
    /// </summary>
    [HttpGet("css/{planillaId}")]
    public async Task<ActionResult<ReporteCssDto>> GetReporteCss(int planillaId)
    {
        try
        {
            var reporte = await _reportesService.GenerarReporteCss(planillaId);
            return Ok(reporte);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = $"Error al generar reporte: {ex.Message}" });
        }
    }

    /// <summary>
    /// Obtiene el reporte de Seguro Educativo en formato JSON
    /// </summary>
    [HttpGet("seguro-educativo/{planillaId}")]
    public async Task<ActionResult<ReporteSeDto>> GetReporteSe(int planillaId)
    {
        try
        {
            var reporte = await _reportesService.GenerarReporteSe(planillaId);
            return Ok(reporte);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = $"Error al generar reporte: {ex.Message}" });
        }
    }

    /// <summary>
    /// Obtiene el reporte de ISR en formato JSON
    /// </summary>
    [HttpGet("isr/{planillaId}")]
    public async Task<ActionResult<ReporteIsrDto>> GetReporteIsr(int planillaId)
    {
        try
        {
            var reporte = await _reportesService.GenerarReporteIsr(planillaId);
            return Ok(reporte);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = $"Error al generar reporte: {ex.Message}" });
        }
    }

    /// <summary>
    /// Obtiene el reporte de planilla detallado en formato JSON
    /// </summary>
    [HttpGet("planilla-detallada/{planillaId}")]
    public async Task<ActionResult<ReportePlanillaDetalladoDto>> GetReportePlanilla(int planillaId)
    {
        try
        {
            var reporte = await _reportesService.GenerarReportePlanillaDetallada(planillaId);
            return Ok(reporte);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = $"Error al generar reporte: {ex.Message}" });
        }
    }

    /// <summary>
    /// Obtiene el reporte de horas extra en formato JSON
    /// </summary>
    [HttpGet("horas-extra/{planillaId}")]
    public async Task<ActionResult<ReporteHorasExtraDto>> GetReporteHorasExtra(int planillaId)
    {
        try
        {
            var reporte = await _reportesService.GenerarReporteHorasExtra(planillaId);
            return Ok(reporte);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = $"Error al generar reporte: {ex.Message}" });
        }
    }

    /// <summary>
    /// Obtiene el reporte consolidado de acreedores en formato JSON.
    /// Agrupa todas las deducciones aplicadas por beneficiario para facilitar
    /// la conciliación y transferencia bancaria a cada acreedor.
    /// </summary>
    [HttpGet("consolidado-acreedor/{planillaId}")]
    [Authorize(Roles = "Owner,Admin,Manager,Accountant")]
    public async Task<ActionResult<ReporteConsolidadoAcreedorDto>> GetReporteConsolidadoAcreedor(int planillaId)
    {
        try
        {
            var reporte = await _reportesService.GenerarReporteConsolidadoAcreedor(planillaId);
            return Ok(reporte);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = $"Error al generar reporte: {ex.Message}" });
        }
    }

    /// <summary>
    /// Obtiene el reporte de deducciones adicionales por empleado en formato JSON.
    /// Muestra la cascada de prelación (pensiones, embargos, voluntarias) con
    /// saldos disponibles y razones de limitación por salario mínimo.
    /// </summary>
    [HttpGet("deducciones-empleado/{planillaId}")]
    [Authorize(Roles = "Owner,Admin,Manager,Accountant")]
    public async Task<ActionResult<ReporteDeduccionesEmpleadoDto>> GetReporteDeduccionesEmpleado(int planillaId)
    {
        try
        {
            var reporte = await _reportesService.GenerarReporteDeduccionesEmpleado(planillaId);
            return Ok(reporte);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = $"Error al generar reporte: {ex.Message}" });
        }
    }

    #endregion

    #region Exportación Excel

    /// <summary>
    /// Exporta el reporte de CSS a Excel
    /// </summary>
    [HttpGet("css/{planillaId}/excel")]
    [PlanLimits(PlanLimitType.ExportExcel)] // ✅ PLAN LIMITS: Verifica automáticamente permiso de exportación Excel
    public async Task<IActionResult> ExportarCssExcel(int planillaId)
    {
        try
        {
            var reporte = await _reportesService.GenerarReporteCss(planillaId);
            var bytes = _exportacionService.ExportarExcelCss(reporte);
            var fileName = $"PlanillaCSS_{planillaId}_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";

            return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = $"Error al exportar: {ex.Message}" });
        }
    }

    /// <summary>
    /// Exporta el reporte de Seguro Educativo a Excel
    /// </summary>
    [HttpGet("seguro-educativo/{planillaId}/excel")]
    [PlanLimits(PlanLimitType.ExportExcel)] // ✅ PLAN LIMITS: Verifica automáticamente permiso de exportación Excel
    public async Task<IActionResult> ExportarSeExcel(int planillaId)
    {
        try
        {
            var reporte = await _reportesService.GenerarReporteSe(planillaId);
            var bytes = _exportacionService.ExportarExcelSe(reporte);
            var fileName = $"SeguroEducativo_{planillaId}_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";

            return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = $"Error al exportar: {ex.Message}" });
        }
    }

    /// <summary>
    /// Exporta el reporte consolidado de acreedores a Excel.
    /// Sheet 1: resumen por acreedor con datos bancarios para transferencias.
    /// Sheet 2: detalle por acreedor mostrando el desglose por empleado.
    /// </summary>
    [HttpGet("consolidado-acreedor/{planillaId}/excel")]
    [Authorize(Roles = "Owner,Admin,Manager,Accountant")]
    [PlanLimits(PlanLimitType.ExportExcel)]
    public async Task<IActionResult> ExportarConsolidadoAcreedorExcel(int planillaId)
    {
        try
        {
            var reporte = await _reportesService.GenerarReporteConsolidadoAcreedor(planillaId);
            var bytes = _exportacionService.ExportarExcelConsolidadoAcreedor(reporte);
            var fileName = $"ConsolidadoAcreedores_{planillaId}_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";

            return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = $"Error al exportar: {ex.Message}" });
        }
    }

    /// <summary>
    /// Exporta el reporte de deducciones adicionales por empleado a Excel.
    /// Genera un bloque por empleado mostrando la cascada de prelación
    /// con saldos disponibles antes/después de cada deducción.
    /// </summary>
    [HttpGet("deducciones-empleado/{planillaId}/excel")]
    [Authorize(Roles = "Owner,Admin,Manager,Accountant")]
    [PlanLimits(PlanLimitType.ExportExcel)]
    public async Task<IActionResult> ExportarDeduccionesEmpleadoExcel(int planillaId)
    {
        try
        {
            var reporte = await _reportesService.GenerarReporteDeduccionesEmpleado(planillaId);
            var bytes = _exportacionService.ExportarExcelDeduccionesEmpleado(reporte);
            var fileName = $"DeduccionesEmpleado_{planillaId}_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";

            return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = $"Error al exportar: {ex.Message}" });
        }
    }

    /// <summary>
    /// Exporta el reporte de horas extra a Excel
    /// </summary>
    [HttpGet("horas-extra/{planillaId}/excel")]
    [PlanLimits(PlanLimitType.ExportExcel)]
    public async Task<IActionResult> ExportarHorasExtraExcel(int planillaId)
    {
        try
        {
            var reporte = await _reportesService.GenerarReporteHorasExtra(planillaId);
            // TODO: Implementar ExportarExcelHorasExtra en ExportacionService
            // Por ahora retornar JSON como placeholder
            return Ok(new { message = "Exportación Excel de horas extra pendiente de implementación", reporte });
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = $"Error al exportar: {ex.Message}" });
        }
    }

    #endregion

    #region Exportación PDF

    /// <summary>
    /// Exporta el reporte de CSS a PDF
    /// </summary>
    [HttpGet("css/{planillaId}/pdf")]
    [PlanLimits(PlanLimitType.ExportPdf)] // ✅ PLAN LIMITS: Verifica automáticamente permiso de exportación PDF
    public async Task<IActionResult> ExportarCssPdf(int planillaId)
    {
        try
        {
            var reporte = await _reportesService.GenerarReporteCss(planillaId);
            var bytes = _exportacionService.ExportarPdfCss(reporte);
            var fileName = $"PlanillaCSS_{planillaId}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";

            return File(bytes, "application/pdf", fileName);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = $"Error al exportar: {ex.Message}" });
        }
    }

    /// <summary>
    /// Exporta el reporte de Seguro Educativo a PDF
    /// </summary>
    [HttpGet("seguro-educativo/{planillaId}/pdf")]
    [PlanLimits(PlanLimitType.ExportPdf)] // ✅ PLAN LIMITS: Verifica automáticamente permiso de exportación PDF
    public async Task<IActionResult> ExportarSePdf(int planillaId)
    {
        try
        {
            var reporte = await _reportesService.GenerarReporteSe(planillaId);
            var bytes = _exportacionService.ExportarPdfSe(reporte);
            var fileName = $"SeguroEducativo_{planillaId}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";

            return File(bytes, "application/pdf", fileName);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = $"Error al exportar: {ex.Message}" });
        }
    }

    /// <summary>
    /// Exporta el reporte consolidado de acreedores a PDF.
    /// Incluye tabla resumen y detalle por acreedor con desglose por empleado.
    /// </summary>
    [HttpGet("consolidado-acreedor/{planillaId}/pdf")]
    [Authorize(Roles = "Owner,Admin,Manager,Accountant")]
    [PlanLimits(PlanLimitType.ExportPdf)]
    public async Task<IActionResult> ExportarConsolidadoAcreedorPdf(int planillaId)
    {
        try
        {
            var reporte = await _reportesService.GenerarReporteConsolidadoAcreedor(planillaId);
            var bytes = _exportacionService.ExportarPdfConsolidadoAcreedor(reporte);
            var fileName = $"ConsolidadoAcreedores_{planillaId}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";

            return File(bytes, "application/pdf", fileName);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = $"Error al exportar: {ex.Message}" });
        }
    }

    /// <summary>
    /// Exporta el reporte de deducciones adicionales por empleado a PDF.
    /// Muestra la cascada de prelación con resaltado de limitaciones por salario mínimo.
    /// </summary>
    [HttpGet("deducciones-empleado/{planillaId}/pdf")]
    [Authorize(Roles = "Owner,Admin,Manager,Accountant")]
    [PlanLimits(PlanLimitType.ExportPdf)]
    public async Task<IActionResult> ExportarDeduccionesEmpleadoPdf(int planillaId)
    {
        try
        {
            var reporte = await _reportesService.GenerarReporteDeduccionesEmpleado(planillaId);
            var bytes = _exportacionService.ExportarPdfDeduccionesEmpleado(reporte);
            var fileName = $"DeduccionesEmpleado_{planillaId}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";

            return File(bytes, "application/pdf", fileName);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = $"Error al exportar: {ex.Message}" });
        }
    }

    /// <summary>
    /// Exporta el reporte de horas extra a PDF
    /// </summary>
    [HttpGet("horas-extra/{planillaId}/pdf")]
    [PlanLimits(PlanLimitType.ExportPdf)]
    public async Task<IActionResult> ExportarHorasExtraPdf(int planillaId)
    {
        try
        {
            var reporte = await _reportesService.GenerarReporteHorasExtra(planillaId);
            // TODO: Implementar ExportarPdfHorasExtra en ExportacionService
            // Por ahora retornar JSON como placeholder
            return Ok(new { message = "Exportación PDF de horas extra pendiente de implementación", reporte });
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = $"Error al exportar: {ex.Message}" });
        }
    }

    #endregion
}
