using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Vorluno.Planilla.Application.DTOs;
using Vorluno.Planilla.Application.Interfaces;
using Vorluno.Planilla.Application.Services;
using Vorluno.Planilla.Domain.Entities;
using Vorluno.Planilla.Domain.Enums;
using Vorluno.Planilla.Infrastructure.Data;
using Vorluno.Planilla.Web.Authorization;

namespace Vorluno.Planilla.Web.Controllers;

/// <summary>
/// Controlador de API para gestionar liquidaciones laborales (settlements).
/// </summary>
[Authorize]
[ApiController]
[Route("api/[controller]")]
public class LiquidacionesController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ITenantContext _tenantContext;
    private readonly IAuditLogService _auditLogService;
    private readonly LiquidacionCalculationService _calculationService;

    public LiquidacionesController(
        ApplicationDbContext context,
        ITenantContext tenantContext,
        IAuditLogService auditLogService,
        LiquidacionCalculationService calculationService)
    {
        _context = context;
        _tenantContext = tenantContext;
        _auditLogService = auditLogService;
        _calculationService = calculationService;
    }

    /// <summary>
    /// Lista liquidaciones con filtros opcionales.
    /// </summary>
    [HttpGet]
    [RequirePermission(SystemPermission.PayrollView)]
    public async Task<IActionResult> GetAll(
        [FromQuery] int? empleadoId,
        [FromQuery] EstadoLiquidacion? estado)
    {
        var tenantId = _tenantContext.TenantId;
        var query = _context.Liquidaciones
            .Where(l => l.TenantId == tenantId)
            .Include(l => l.Empleado)
            .AsNoTracking()
            .AsQueryable();

        if (empleadoId.HasValue)
            query = query.Where(l => l.EmpleadoId == empleadoId.Value);

        if (estado.HasValue)
            query = query.Where(l => l.Estado == estado.Value);

        var liquidaciones = await query
            .OrderByDescending(l => l.FechaLiquidacion)
            .ToListAsync();

        var dtos = liquidaciones.Select(MapToDto);
        return Ok(dtos);
    }

    /// <summary>
    /// Obtiene detalle de una liquidación por ID.
    /// </summary>
    [HttpGet("{id}")]
    [RequirePermission(SystemPermission.PayrollView)]
    public async Task<IActionResult> GetById(int id)
    {
        var tenantId = _tenantContext.TenantId;
        var liquidacion = await _context.Liquidaciones
            .Where(l => l.Id == id && l.TenantId == tenantId)
            .Include(l => l.Empleado)
            .AsNoTracking()
            .FirstOrDefaultAsync();

        if (liquidacion == null)
            return NotFound();

        return Ok(MapToDto(liquidacion));
    }

    /// <summary>
    /// Crea y calcula una nueva liquidación.
    /// </summary>
    [HttpPost]
    [RequirePermission(SystemPermission.PayrollCalculate)]
    public async Task<IActionResult> Create(CreateLiquidacionRequest request)
    {
        var tenantId = _tenantContext.TenantId;

        // Validar empleado
        var empleado = await _context.Empleados
            .Where(e => e.Id == request.EmpleadoId && e.TenantId == tenantId)
            .Include(e => e.HistorialSalarial)
            .FirstOrDefaultAsync();

        if (empleado == null)
            return BadRequest(new { message = "Empleado no encontrado" });

        if (request.FechaTerminacion < empleado.FechaContratacion)
            return BadRequest(new { message = "La fecha de terminación no puede ser anterior a la fecha de contratación" });

        // Verificar que no exista una liquidación activa (no pagada) para este empleado
        var liquidacionExistente = await _context.Liquidaciones
            .Where(l => l.EmpleadoId == request.EmpleadoId
                && l.TenantId == tenantId
                && l.Estado != EstadoLiquidacion.Pagada)
            .FirstOrDefaultAsync();

        if (liquidacionExistente != null)
            return BadRequest(new { message = "Ya existe una liquidación pendiente para este empleado. Debe pagarla o eliminarla antes de crear otra." });

        // Calcular liquidación
        var resultado = _calculationService.Calcular(empleado, request);

        // Generar número correlativo
        var anio = DateTime.UtcNow.Year;
        var count = await _context.Liquidaciones
            .Where(l => l.TenantId == tenantId && l.FechaLiquidacion.Year == anio)
            .CountAsync();
        var numero = $"LIQ-{anio}-{(count + 1):D3}";

        // Obtener userId del JWT
        var userId = User.FindFirst("sub")?.Value ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        var liquidacion = new Liquidacion
        {
            TenantId = tenantId,
            EmpleadoId = request.EmpleadoId,
            Numero = numero,
            FechaLiquidacion = DateTime.UtcNow,
            FechaContratacion = empleado.FechaContratacion,
            FechaTerminacion = request.FechaTerminacion,
            TipoTerminacion = request.TipoTerminacion,
            SalarioBase = empleado.SalarioBase,
            SalarioPromedio = empleado.SalarioBase,
            AnosServicio = resultado.AnosServicio,

            PrimaAntiguedad = resultado.PrimaAntiguedad,
            Indemnizacion = resultado.Indemnizacion,
            IndemnizacionSemanas = resultado.IndemnizacionSemanas,
            RecargoArt219 = resultado.RecargoArt219,
            Preaviso = resultado.Preaviso,
            VacacionesProporcionales = resultado.VacacionesProporcionales,
            DiasVacacionesProporcionales = resultado.DiasVacacionesProporcionales,
            DecimoTercerMesProporcional = resultado.DecimoTercerMesProporcional,
            Cesantia = resultado.Cesantia,
            SalarioPendiente = resultado.SalarioPendiente,
            DiasSalarioPendiente = resultado.DiasSalarioPendiente,
            IncluyePreaviso = request.IncluyePreaviso,

            CssEmpleado = resultado.CssEmpleado,
            SeEmpleado = resultado.SeEmpleado,
            Isr = resultado.Isr,
            CssPatronal = resultado.CssPatronal,
            SePatronal = resultado.SePatronal,

            TotalBruto = resultado.TotalBruto,
            TotalDeducciones = resultado.TotalDeducciones,
            TotalNeto = resultado.TotalNeto,

            Estado = EstadoLiquidacion.Calculada,
            Observaciones = request.Observaciones,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = userId
        };

        _context.Liquidaciones.Add(liquidacion);
        await _context.SaveChangesAsync();

        // Recargar con navegación
        liquidacion = await _context.Liquidaciones
            .Where(l => l.Id == liquidacion.Id && l.TenantId == tenantId)
            .Include(l => l.Empleado)
            .FirstOrDefaultAsync();

        // Audit log
        try
        {
            await _auditLogService.LogAsync(
                "LiquidacionCreated",
                "Liquidacion",
                liquidacion!.Id.ToString(),
                new Dictionary<string, string>
                {
                    ["EmployeeId"] = liquidacion.EmpleadoId.ToString(),
                    ["EmployeeName"] = liquidacion.Empleado != null
                        ? $"{liquidacion.Empleado.Nombre} {liquidacion.Empleado.Apellido}" : "N/A",
                    ["TipoTerminacion"] = liquidacion.TipoTerminacion.ToNombre(),
                    ["TotalBruto"] = liquidacion.TotalBruto.ToString("N2"),
                    ["TotalNeto"] = liquidacion.TotalNeto.ToString("N2")
                });
        }
        catch (Exception)
        {
            // No bloquear si falla el audit log
        }

        return CreatedAtAction(nameof(GetById), new { id = liquidacion!.Id }, MapToDto(liquidacion));
    }

    /// <summary>
    /// Aprueba una liquidación calculada.
    /// </summary>
    [HttpPost("{id}/aprobar")]
    [RequirePermission(SystemPermission.PayrollApprove)]
    public async Task<IActionResult> Aprobar(int id)
    {
        var tenantId = _tenantContext.TenantId;
        var liquidacion = await _context.Liquidaciones
            .FirstOrDefaultAsync(l => l.Id == id && l.TenantId == tenantId);

        if (liquidacion == null)
            return NotFound();

        if (liquidacion.Estado != EstadoLiquidacion.Calculada)
            return BadRequest(new { message = "Solo se pueden aprobar liquidaciones en estado Calculada" });

        var userId = User.FindFirst("sub")?.Value ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        liquidacion.Estado = EstadoLiquidacion.Aprobada;
        liquidacion.ApprovedBy = userId;
        liquidacion.ApprovedDate = DateTime.UtcNow;
        liquidacion.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        // Audit log
        try
        {
            await _auditLogService.LogAsync(
                "LiquidacionApproved",
                "Liquidacion",
                id.ToString(),
                new Dictionary<string, string>
                {
                    ["ApprovedBy"] = userId ?? "unknown"
                });
        }
        catch (Exception) { }

        return NoContent();
    }

    /// <summary>
    /// Marca una liquidación aprobada como pagada.
    /// </summary>
    [HttpPost("{id}/pagar")]
    [RequirePermission(SystemPermission.PayrollApprove)]
    public async Task<IActionResult> Pagar(int id)
    {
        var tenantId = _tenantContext.TenantId;
        var liquidacion = await _context.Liquidaciones
            .FirstOrDefaultAsync(l => l.Id == id && l.TenantId == tenantId);

        if (liquidacion == null)
            return NotFound();

        if (liquidacion.Estado != EstadoLiquidacion.Aprobada)
            return BadRequest(new { message = "Solo se pueden pagar liquidaciones en estado Aprobada" });

        liquidacion.Estado = EstadoLiquidacion.Pagada;
        liquidacion.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        // Audit log
        try
        {
            await _auditLogService.LogAsync(
                "LiquidacionPaid",
                "Liquidacion",
                id.ToString(),
                new Dictionary<string, string>
                {
                    ["TotalNeto"] = liquidacion.TotalNeto.ToString("N2")
                });
        }
        catch (Exception) { }

        return NoContent();
    }

    /// <summary>
    /// Elimina una liquidación en estado Calculada (borrador).
    /// </summary>
    [HttpDelete("{id}")]
    [RequirePermission(SystemPermission.PayrollDelete)]
    public async Task<IActionResult> Delete(int id)
    {
        var tenantId = _tenantContext.TenantId;
        var liquidacion = await _context.Liquidaciones
            .FirstOrDefaultAsync(l => l.Id == id && l.TenantId == tenantId);

        if (liquidacion == null)
            return NotFound();

        if (liquidacion.Estado != EstadoLiquidacion.Calculada)
            return BadRequest(new { message = "Solo se pueden eliminar liquidaciones en estado Calculada" });

        _context.Liquidaciones.Remove(liquidacion);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    /// <summary>
    /// Exportar PDF de liquidación (stub — retorna NotImplemented por ahora).
    /// </summary>
    [HttpGet("{id}/pdf")]
    [RequirePermission(SystemPermission.ReportsExport)]
    public IActionResult ExportPdf(int id)
    {
        return StatusCode(501, new { message = "Exportación PDF de liquidación aún no implementada" });
    }

    /// <summary>
    /// Mapea entidad Liquidacion a LiquidacionDto.
    /// </summary>
    private static LiquidacionDto MapToDto(Liquidacion l)
    {
        var nombreCompleto = l.Empleado != null
            ? $"{l.Empleado.Nombre} {l.Empleado.Apellido}".Trim()
            : string.Empty;

        return new LiquidacionDto(
            Id: l.Id,
            EmpleadoId: l.EmpleadoId,
            EmpleadoNombre: nombreCompleto,
            Numero: l.Numero,
            FechaLiquidacion: l.FechaLiquidacion,
            FechaContratacion: l.FechaContratacion,
            FechaTerminacion: l.FechaTerminacion,
            TipoTerminacion: l.TipoTerminacion,
            TipoTerminacionNombre: l.TipoTerminacion.ToNombre(),
            SalarioBase: l.SalarioBase,
            SalarioPromedio: l.SalarioPromedio,
            AnosServicio: l.AnosServicio,
            PrimaAntiguedad: l.PrimaAntiguedad,
            Indemnizacion: l.Indemnizacion,
            IndemnizacionSemanas: l.IndemnizacionSemanas,
            RecargoArt219: l.RecargoArt219,
            Preaviso: l.Preaviso,
            VacacionesProporcionales: l.VacacionesProporcionales,
            DiasVacacionesProporcionales: l.DiasVacacionesProporcionales,
            DecimoTercerMesProporcional: l.DecimoTercerMesProporcional,
            Cesantia: l.Cesantia,
            SalarioPendiente: l.SalarioPendiente,
            DiasSalarioPendiente: l.DiasSalarioPendiente,
            IncluyePreaviso: l.IncluyePreaviso,
            CssEmpleado: l.CssEmpleado,
            SeEmpleado: l.SeEmpleado,
            Isr: l.Isr,
            CssPatronal: l.CssPatronal,
            SePatronal: l.SePatronal,
            TotalBruto: l.TotalBruto,
            TotalDeducciones: l.TotalDeducciones,
            TotalNeto: l.TotalNeto,
            Estado: l.Estado,
            EstadoNombre: l.Estado.ToNombre(),
            Observaciones: l.Observaciones,
            CreatedAt: l.CreatedAt,
            CreatedBy: l.CreatedBy,
            ApprovedBy: l.ApprovedBy,
            ApprovedDate: l.ApprovedDate
        );
    }
}
