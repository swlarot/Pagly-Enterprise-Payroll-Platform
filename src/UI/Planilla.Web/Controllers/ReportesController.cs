// ====================================================================
// Planilla - ReportesController (Rediseño C-012)
// Actualizado: 2026-02-20
// 14 endpoints: 5 reportes × JSON + Excel/PDF (Comprobantes solo PDF)
// ====================================================================

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vorluno.Planilla.Application.Helpers;
using Vorluno.Planilla.Application.Interfaces;
using Vorluno.Planilla.Domain.Enums;
using Vorluno.Planilla.Infrastructure.Services;
using Vorluno.Planilla.Web.Authorization;
using Vorluno.Planilla.Web.Filters;

namespace Vorluno.Planilla.Web.Controllers;

/// <summary>
/// Controller para los 5 reportes de planilla: Regular, Mensual, Acreedores, SIP y Comprobantes.
/// Todos los endpoints requieren autenticación y rol Accountant o superior.
/// Los endpoints de exportación verifican los límites del plan de suscripción.
/// </summary>
[RequirePermission(SystemPermission.ReportsView)]
[ApiController]
[Route("api/[controller]")]
public class ReportesController : ControllerBase
{
    private readonly ReportesService _reportesService;
    private readonly ExportacionService _exportacionService;

    public ReportesController(
        ReportesService reportesService,
        ExportacionService exportacionService)
    {
        _reportesService = reportesService ?? throw new ArgumentNullException(nameof(reportesService));
        _exportacionService = exportacionService ?? throw new ArgumentNullException(nameof(exportacionService));
    }

    // ====================================================================
    // REPORTE 1: Planilla Regular
    // ====================================================================

    /// <summary>
    /// Obtiene el reporte operativo de planilla regular en formato JSON.
    /// Incluye horas trabajadas (regular, domingo, feriado, extra) y deducciones por empleado.
    /// </summary>
    [HttpGet("planilla-regular/{planillaId}")]
    public async Task<IActionResult> GetPlanillaRegular(int planillaId)
    {
        try
        {
            var reporte = await _reportesService.GenerarReportePlanillaRegular(planillaId);
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

    /// <summary>Exporta el reporte de Planilla Regular a Excel.</summary>
    [HttpGet("planilla-regular/{planillaId}/excel")]
    [PlanLimits(PlanLimitType.ExportExcel)]
    public async Task<IActionResult> ExportarPlanillaRegularExcel(int planillaId)
    {
        try
        {
            var reporte = await _reportesService.GenerarReportePlanillaRegular(planillaId);
            var bytes = _exportacionService.ExportarExcelPlanillaRegular(reporte);
            if (bytes.Length == 0)
                return BadRequest(new { message = "No hay datos para exportar." });
            var fileName = $"PlanillaRegular_{planillaId}_{DateTimeHelper.NowPanama():yyyyMMdd_HHmmss}.xlsx";
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

    /// <summary>Exporta el reporte de Planilla Regular a PDF.</summary>
    [HttpGet("planilla-regular/{planillaId}/pdf")]
    [PlanLimits(PlanLimitType.ExportPdf)]
    public async Task<IActionResult> ExportarPlanillaRegularPdf(int planillaId)
    {
        try
        {
            var reporte = await _reportesService.GenerarReportePlanillaRegular(planillaId);
            var bytes = _exportacionService.ExportarPdfPlanillaRegular(reporte);
            if (bytes.Length == 0)
                return BadRequest(new { message = "No hay datos para exportar." });
            var fileName = $"PlanillaRegular_{planillaId}_{DateTimeHelper.NowPanama():yyyyMMdd_HHmmss}.pdf";
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

    // ====================================================================
    // REPORTE 2: Mensual
    // ====================================================================

    /// <summary>
    /// Obtiene el reporte mensual consolidado en formato JSON.
    /// Agrega todas las planillas calculadas/aprobadas del mes especificado.
    /// </summary>
    [HttpGet("mensual")]
    public async Task<IActionResult> GetMensual([FromQuery] int mes, [FromQuery] int anio)
    {
        try
        {
            if (mes < 1 || mes > 12)
                return BadRequest(new { message = "El mes debe estar entre 1 y 12." });
            if (anio < 2000 || anio > 2100)
                return BadRequest(new { message = "El año especificado no es válido." });

            var reporte = await _reportesService.GenerarReporteMensual(mes, anio);
            return Ok(reporte);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = $"Error al generar reporte: {ex.Message}" });
        }
    }

    /// <summary>Exporta el reporte Mensual a Excel.</summary>
    [HttpGet("mensual/excel")]
    [PlanLimits(PlanLimitType.ExportExcel)]
    public async Task<IActionResult> ExportarMensualExcel([FromQuery] int mes, [FromQuery] int anio)
    {
        try
        {
            if (mes < 1 || mes > 12)
                return BadRequest(new { message = "El mes debe estar entre 1 y 12." });

            var reporte = await _reportesService.GenerarReporteMensual(mes, anio);
            var bytes = _exportacionService.ExportarExcelMensual(reporte);
            if (bytes.Length == 0)
                return BadRequest(new { message = "No hay datos para exportar." });
            var fileName = $"ReporteMensual_{mes:D2}_{anio}_{DateTimeHelper.NowPanama():yyyyMMdd_HHmmss}.xlsx";
            return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = $"Error al exportar: {ex.Message}" });
        }
    }

    /// <summary>Exporta el reporte Mensual a PDF.</summary>
    [HttpGet("mensual/pdf")]
    [PlanLimits(PlanLimitType.ExportPdf)]
    public async Task<IActionResult> ExportarMensualPdf([FromQuery] int mes, [FromQuery] int anio)
    {
        try
        {
            if (mes < 1 || mes > 12)
                return BadRequest(new { message = "El mes debe estar entre 1 y 12." });

            var reporte = await _reportesService.GenerarReporteMensual(mes, anio);
            var bytes = _exportacionService.ExportarPdfMensual(reporte);
            if (bytes.Length == 0)
                return BadRequest(new { message = "No hay datos para exportar." });
            var fileName = $"ReporteMensual_{mes:D2}_{anio}_{DateTimeHelper.NowPanama():yyyyMMdd_HHmmss}.pdf";
            return File(bytes, "application/pdf", fileName);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = $"Error al exportar: {ex.Message}" });
        }
    }

    // ====================================================================
    // REPORTE 3: Acreedores
    // ====================================================================

    /// <summary>
    /// Obtiene el reporte de acreedores en formato JSON.
    /// Agrupa deducciones por beneficiario para facilitar la conciliación bancaria.
    /// </summary>
    [HttpGet("acreedores/{planillaId}")]
    public async Task<IActionResult> GetAcreedores(int planillaId)
    {
        try
        {
            var reporte = await _reportesService.GenerarReporteAcreedores(planillaId);
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

    /// <summary>Exporta el reporte de Acreedores a Excel (2 hojas: resumen + detalle).</summary>
    [HttpGet("acreedores/{planillaId}/excel")]
    [PlanLimits(PlanLimitType.ExportExcel)]
    public async Task<IActionResult> ExportarAcreedoresExcel(int planillaId)
    {
        try
        {
            var reporte = await _reportesService.GenerarReporteAcreedores(planillaId);
            var bytes = _exportacionService.ExportarExcelAcreedores(reporte);
            if (bytes.Length == 0)
                return BadRequest(new { message = "No hay datos para exportar." });
            var fileName = $"Acreedores_{planillaId}_{DateTimeHelper.NowPanama():yyyyMMdd_HHmmss}.xlsx";
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

    /// <summary>Exporta el reporte de Acreedores a PDF.</summary>
    [HttpGet("acreedores/{planillaId}/pdf")]
    [PlanLimits(PlanLimitType.ExportPdf)]
    public async Task<IActionResult> ExportarAcreedoresPdf(int planillaId)
    {
        try
        {
            var reporte = await _reportesService.GenerarReporteAcreedores(planillaId);
            var bytes = _exportacionService.ExportarPdfAcreedores(reporte);
            if (bytes.Length == 0)
                return BadRequest(new { message = "No hay datos para exportar." });
            var fileName = $"Acreedores_{planillaId}_{DateTimeHelper.NowPanama():yyyyMMdd_HHmmss}.pdf";
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

    // ====================================================================
    // REPORTE 4: SIP (CSS)
    // ====================================================================

    /// <summary>
    /// Obtiene el reporte SIP en formato JSON.
    /// Contiene los datos necesarios para subir a la plataforma CSS de Panamá.
    /// </summary>
    [HttpGet("sip/{planillaId}")]
    public async Task<IActionResult> GetSip(int planillaId)
    {
        try
        {
            var reporte = await _reportesService.GenerarReporteSip(planillaId);
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

    /// <summary>Exporta el reporte SIP a Excel.</summary>
    [HttpGet("sip/{planillaId}/excel")]
    [PlanLimits(PlanLimitType.ExportExcel)]
    public async Task<IActionResult> ExportarSipExcel(int planillaId)
    {
        try
        {
            var reporte = await _reportesService.GenerarReporteSip(planillaId);
            var bytes = _exportacionService.ExportarExcelSip(reporte);
            if (bytes.Length == 0)
                return BadRequest(new { message = "No hay datos para exportar." });
            var fileName = $"SIP_{planillaId}_{DateTimeHelper.NowPanama():yyyyMMdd_HHmmss}.xlsx";
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

    /// <summary>Exporta el reporte SIP a PDF.</summary>
    [HttpGet("sip/{planillaId}/pdf")]
    [PlanLimits(PlanLimitType.ExportPdf)]
    public async Task<IActionResult> ExportarSipPdf(int planillaId)
    {
        try
        {
            var reporte = await _reportesService.GenerarReporteSip(planillaId);
            var bytes = _exportacionService.ExportarPdfSip(reporte);
            if (bytes.Length == 0)
                return BadRequest(new { message = "No hay datos para exportar." });
            var fileName = $"SIP_{planillaId}_{DateTimeHelper.NowPanama():yyyyMMdd_HHmmss}.pdf";
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

    // ====================================================================
    // REPORTE 5: Comprobantes de Pago
    // ====================================================================

    /// <summary>
    /// Obtiene los comprobantes de pago en formato JSON.
    /// Devuelve un comprobante por empleado con el desglose completo de ingresos y deducciones.
    /// </summary>
    [HttpGet("comprobantes/{planillaId}")]
    public async Task<IActionResult> GetComprobantes(int planillaId)
    {
        try
        {
            var reporte = await _reportesService.GenerarReporteComprobantes(planillaId);
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
    /// Exporta los Comprobantes de Pago a PDF.
    /// Genera una página por empleado en formato Letter Portrait con diseño de recibo.
    /// No disponible en Excel por la naturaleza del layout individual.
    /// </summary>
    [HttpGet("comprobantes/{planillaId}/pdf")]
    [PlanLimits(PlanLimitType.ExportPdf)]
    public async Task<IActionResult> ExportarComprobantesPdf(int planillaId)
    {
        try
        {
            var reporte = await _reportesService.GenerarReporteComprobantes(planillaId);
            var bytes = _exportacionService.ExportarPdfComprobantes(reporte);
            if (bytes.Length == 0)
                return BadRequest(new { message = "No hay datos para exportar." });
            var fileName = $"Comprobantes_{planillaId}_{DateTimeHelper.NowPanama():yyyyMMdd_HHmmss}.pdf";
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
    /// Exporta los Comprobantes de Pago a PDF en formato compacto para impresión física.
    /// Genera 2 empleados por página Letter con original y copia lado a lado,
    /// separados por una línea de corte horizontal. Ideal para entrega de recibos en papel.
    /// </summary>
    [HttpGet("comprobantes/{planillaId}/pdf-compacto")]
    [PlanLimits(PlanLimitType.ExportPdf)]
    public async Task<IActionResult> ExportarComprobantesCompactosPdf(int planillaId)
    {
        try
        {
            var reporte = await _reportesService.GenerarReporteComprobantes(planillaId);
            var bytes = _exportacionService.ExportarPdfComprobantesCompactos(reporte);
            if (bytes.Length == 0)
                return BadRequest(new { message = "No hay datos para exportar." });
            var fileName = $"Comprobantes_Compacto_{planillaId}_{DateTimeHelper.NowPanama():yyyyMMdd_HHmmss}.pdf";
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

    // ====================================================================
    // REPORTE 6: Desglose Décimo Tercer Mes (DEV-171)
    // ====================================================================

    /// <summary>Obtiene el reporte de desglose del Décimo Tercer Mes en formato JSON.</summary>
    [HttpGet("desglose-decimo/{planillaDecimoId}")]
    public async Task<IActionResult> GetDesgloseDecimo(int planillaDecimoId)
    {
        try
        {
            var reporte = await _reportesService.GenerarReporteDesgloseDecimo(planillaDecimoId);
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

    /// <summary>Exporta el reporte de desglose del Décimo Tercer Mes a Excel.</summary>
    [HttpGet("desglose-decimo/{planillaDecimoId}/excel")]
    [PlanLimits(PlanLimitType.ExportExcel)]
    public async Task<IActionResult> ExportarDesgloseDecimoExcel(int planillaDecimoId)
    {
        try
        {
            var reporte = await _reportesService.GenerarReporteDesgloseDecimo(planillaDecimoId);
            var bytes = _exportacionService.ExportarExcelDesgloseDecimo(reporte);
            if (bytes.Length == 0)
                return BadRequest(new { message = "No hay datos para exportar." });
            var fileName = $"DesgloseDecimo_{planillaDecimoId}_{DateTimeHelper.NowPanama():yyyyMMdd_HHmmss}.xlsx";
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

    /// <summary>Exporta el reporte de desglose del Décimo Tercer Mes a PDF.</summary>
    [HttpGet("desglose-decimo/{planillaDecimoId}/pdf")]
    [PlanLimits(PlanLimitType.ExportPdf)]
    public async Task<IActionResult> ExportarDesgloseDecimoPdf(int planillaDecimoId)
    {
        try
        {
            var reporte = await _reportesService.GenerarReporteDesgloseDecimo(planillaDecimoId);
            var bytes = _exportacionService.ExportarPdfDesgloseDecimo(reporte);
            if (bytes.Length == 0)
                return BadRequest(new { message = "No hay datos para exportar." });
            var fileName = $"DesgloseDecimo_{planillaDecimoId}_{DateTimeHelper.NowPanama():yyyyMMdd_HHmmss}.pdf";
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
}
