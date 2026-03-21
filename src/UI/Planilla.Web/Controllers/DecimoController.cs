using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Vorluno.Planilla.Application.DTOs;
using Vorluno.Planilla.Application.Interfaces;
using Vorluno.Planilla.Domain.Entities;
using Vorluno.Planilla.Domain.Enums;
using Vorluno.Planilla.Infrastructure.Data;
using Vorluno.Planilla.Web.Authorization;

namespace Vorluno.Planilla.Web.Controllers;

[ApiController]
[Route("api/decimo")]
[Authorize]
public class DecimoController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ITenantContext _tenantContext;
    private readonly IDecimoCalculationService _decimoCalculationService;

    public DecimoController(
        ApplicationDbContext context,
        ITenantContext tenantContext,
        IDecimoCalculationService decimoCalculationService)
    {
        _context = context;
        _tenantContext = tenantContext;
        _decimoCalculationService = decimoCalculationService;
    }

    // ====================================================================
    // GET /api/decimo
    // Lista todas las planillas de décimo del tenant (filtro año opcional)
    // ====================================================================
    [HttpGet]
    [RequirePermission(SystemPermission.PayrollView)]
    public async Task<ActionResult<List<PlanillaDecimoListDto>>> GetAll([FromQuery] int? ano)
    {
        var tenantId = _tenantContext.TenantId;

        var query = _context.PlanillasDecimo
            .Where(p => p.TenantId == tenantId);

        if (ano.HasValue)
            query = query.Where(p => p.FechaPago.Year == ano.Value);

        var planillas = await query
            .OrderByDescending(p => p.FechaPago)
            .Select(p => new PlanillaDecimoListDto(
                p.Id,
                p.Numero,
                p.PeriodoDesde,
                p.PeriodoHasta,
                p.FechaPago,
                p.Estado.ToString(),
                p.TotalDecimo,
                p.TotalNetoPago,
                p.Detalles.Count
            ))
            .ToListAsync();

        return Ok(planillas);
    }

    // ====================================================================
    // GET /api/decimo/{id}
    // Detalle con todos los empleados y desglose mensual
    // ====================================================================
    [HttpGet("{id}")]
    [RequirePermission(SystemPermission.PayrollView)]
    public async Task<ActionResult<PlanillaDecimoDetalleDto>> GetById(int id)
    {
        var tenantId = _tenantContext.TenantId;

        var planilla = await _context.PlanillasDecimo
            .Include(p => p.Detalles)
                .ThenInclude(d => d.Empleado)
            .FirstOrDefaultAsync(p => p.Id == id && p.TenantId == tenantId);

        if (planilla == null)
            return NotFound(new { message = "Planilla de décimo no encontrada" });

        var detallesDto = planilla.Detalles.Select(d =>
        {
            var desglose = JsonSerializer.Deserialize<List<DesgloseMensualItem>>(
                d.DesgloseMensualJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
            ) ?? [];

            return new DetalleDecimoDto(
                d.Id,
                d.EmpleadoId,
                $"{d.Empleado?.Nombre} {d.Empleado?.Apellido}".Trim(),
                d.Empleado?.NumeroIdentificacion ?? "",
                desglose,
                d.TotalDevengado,
                d.MontoDecimo,
                d.CssEmpleado,
                d.CssPatrono,
                d.SeEmpleado,
                d.SePatrono,
                d.ISR,
                d.TotalDeducciones,
                d.NetoPago
            );
        }).ToList();

        var dto = new PlanillaDecimoDetalleDto(
            planilla.Id,
            planilla.Numero,
            planilla.PeriodoDesde,
            planilla.PeriodoHasta,
            planilla.FechaPago,
            planilla.Estado.ToString(),
            planilla.TotalDevengado,
            planilla.TotalDecimo,
            planilla.TotalCssEmpleado,
            planilla.TotalCssPatrono,
            planilla.TotalSeEmpleado,
            planilla.TotalSePatrono,
            planilla.TotalISR,
            planilla.TotalNetoPago,
            detallesDto
        );

        return Ok(dto);
    }

    // ====================================================================
    // POST /api/decimo
    // Crear una nueva planilla de décimo (en estado Borrador)
    // ====================================================================
    [HttpPost]
    [RequirePermission(SystemPermission.PayrollCalculate)]
    public async Task<ActionResult<PlanillaDecimoListDto>> Create([FromBody] CreatePlanillaDecimoRequest request)
    {
        var tenantId = _tenantContext.TenantId;

        if (request.PeriodoDesde >= request.PeriodoHasta)
            return BadRequest(new { message = "La fecha de inicio debe ser anterior a la fecha de fin" });

        // DEV-174: validar que FechaPago corresponde a una partida válida
        if (request.FechaPago.Month != 4 && request.FechaPago.Month != 8 && request.FechaPago.Month != 12)
            return BadRequest(new { message = "La fecha de pago debe ser en abril (partida 1), agosto (partida 2) o diciembre (partida 3)" });

        // Generar número correlativo
        var count = await _context.PlanillasDecimo
            .CountAsync(p => p.TenantId == tenantId && p.FechaPago.Year == request.FechaPago.Year);
        var numero = $"DEC-{request.FechaPago.Year}-{(count + 1):D2}";

        var planilla = new PlanillaDecimo
        {
            TenantId = tenantId,
            Numero = numero,
            PeriodoDesde = request.PeriodoDesde.Date,
            PeriodoHasta = request.PeriodoHasta.Date,
            FechaPago = request.FechaPago.Date,
            Estado = EstadoDecimo.Borrador,
            CreatedAt = DateTime.UtcNow
        };

        _context.PlanillasDecimo.Add(planilla);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = planilla.Id },
            new PlanillaDecimoListDto(
                planilla.Id, planilla.Numero, planilla.PeriodoDesde, planilla.PeriodoHasta,
                planilla.FechaPago, planilla.Estado.ToString(), 0, 0, 0
            ));
    }

    // ====================================================================
    // POST /api/decimo/{id}/calcular
    // Calcula el décimo para todos los empleados activos
    // ====================================================================
    [HttpPost("{id}/calcular")]
    [RequirePermission(SystemPermission.PayrollCalculate)]
    public async Task<ActionResult> Calcular(int id)
    {
        var tenantId = _tenantContext.TenantId;
        try
        {
            var resultado = await _decimoCalculationService.CalcularAsync(id, tenantId);
            return Ok(new { message = $"Décimo calculado para {resultado.EmpleadosProcesados} empleados", totalDecimo = resultado.TotalDecimo });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    // ====================================================================
    // PATCH /api/decimo/{id}/pagar
    // Marcar planilla de décimo como pagada
    // ====================================================================
    [HttpPatch("{id}/pagar")]
    [RequirePermission(SystemPermission.PayrollApprove)]
    public async Task<ActionResult> Pagar(int id)
    {
        var tenantId = _tenantContext.TenantId;

        var planilla = await _context.PlanillasDecimo
            .FirstOrDefaultAsync(p => p.Id == id && p.TenantId == tenantId);

        if (planilla == null)
            return NotFound(new { message = "Planilla de décimo no encontrada" });

        if (planilla.Estado != EstadoDecimo.Calculada)
            return BadRequest(new { message = "Solo se pueden pagar planillas en estado Calculada" });

        planilla.Estado = EstadoDecimo.Pagada;
        planilla.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return Ok(new { message = "Planilla de décimo marcada como pagada" });
    }

}

// ====================================================================
// DTOs
// ====================================================================

public record CreatePlanillaDecimoRequest(
    DateTime PeriodoDesde,
    DateTime PeriodoHasta,
    DateTime FechaPago
);

public record PlanillaDecimoListDto(
    int Id,
    string Numero,
    DateTime PeriodoDesde,
    DateTime PeriodoHasta,
    DateTime FechaPago,
    string Estado,
    decimal TotalDecimo,
    decimal TotalNetoPago,
    int NumEmpleados
);

public record PlanillaDecimoDetalleDto(
    int Id,
    string Numero,
    DateTime PeriodoDesde,
    DateTime PeriodoHasta,
    DateTime FechaPago,
    string Estado,
    decimal TotalDevengado,
    decimal TotalDecimo,
    decimal TotalCssEmpleado,
    decimal TotalCssPatrono,
    decimal TotalSeEmpleado,
    decimal TotalSePatrono,
    decimal TotalISR,
    decimal TotalNetoPago,
    List<DetalleDecimoDto> Detalles
);

public record DetalleDecimoDto(
    int Id,
    int EmpleadoId,
    string NombreCompleto,
    string NumeroIdentificacion,
    List<DesgloseMensualItem> DesgloseMensual,
    decimal TotalDevengado,
    decimal MontoDecimo,
    decimal CssEmpleado,
    decimal CssPatrono,
    decimal SeEmpleado,
    decimal SePatrono,
    decimal ISR,
    decimal TotalDeducciones,
    decimal NetoPago
);

// DesgloseMensualItem movido a Vorluno.Planilla.Application.DTOs.DesgloseMensualItem (DEV-173)
