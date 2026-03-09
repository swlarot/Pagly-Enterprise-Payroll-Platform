using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Vorluno.Planilla.Application.Helpers;
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
    private readonly IPayrollConfigProvider _configProvider;

    public DecimoController(
        ApplicationDbContext context,
        ITenantContext tenantContext,
        IPayrollConfigProvider configProvider)
    {
        _context = context;
        _tenantContext = tenantContext;
        _configProvider = configProvider;
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

        var planilla = await _context.PlanillasDecimo
            .Include(p => p.Detalles)
            .FirstOrDefaultAsync(p => p.Id == id && p.TenantId == tenantId);

        if (planilla == null)
            return NotFound(new { message = "Planilla de décimo no encontrada" });

        if (planilla.Estado == EstadoDecimo.Pagada)
            return BadRequest(new { message = "No se puede recalcular una planilla ya pagada" });

        var empleados = await _context.Empleados
            .Where(e => e.TenantId == tenantId && e.EstaActivo)
            .ToListAsync();

        // Cargar tax config para ISR (usamos fecha de pago como fecha referencia)
        var taxConfig = await _configProvider.GetTaxConfigAsync(tenantId, planilla.FechaPago);
        var taxBrackets = await _configProvider.GetTaxBracketsAsync(tenantId, planilla.FechaPago.Year);

        // Limpiar detalles existentes para recalcular
        if (planilla.Detalles.Any())
        {
            _context.DetallesDecimo.RemoveRange(planilla.Detalles);
            planilla.Detalles.Clear();
        }

        decimal totalDevengado = 0;
        decimal totalDecimo = 0;
        decimal totalCssEmp = 0;
        decimal totalCssPat = 0;
        decimal totalSeEmp = 0;
        decimal totalSePat = 0;
        decimal totalIsr = 0;
        decimal totalNeto = 0;

        foreach (var empleado in empleados)
        {
            // 1. Obtener PayrollDetails del empleado dentro del período (solo planillas regulares)
            var details = await _context.PayrollDetails
                .Include(d => d.PayrollHeader)
                .Where(d => d.TenantId == tenantId
                         && d.EmpleadoId == empleado.Id
                         && d.PayrollHeader.PeriodStartDate >= planilla.PeriodoDesde
                         && d.PayrollHeader.PeriodEndDate <= planilla.PeriodoHasta)
                .ToListAsync();

            if (details.Count == 0)
                continue;

            // 2. Desglose mensual: agrupar por año+mes según PeriodStartDate del header
            var desgloseMensual = details
                .GroupBy(d => new { d.PayrollHeader.PeriodStartDate.Year, d.PayrollHeader.PeriodStartDate.Month })
                .Select(g => new DesgloseMensualItem(g.Key.Year, g.Key.Month, g.Sum(d => d.GrossPay)))
                .OrderBy(x => x.Ano).ThenBy(x => x.Mes)
                .ToList();

            decimal totalDev = details.Sum(d => d.GrossPay);

            // 3. MontoDecimo = TotalDevengado / 12
            decimal montoDecimo = Math.Round(totalDev / 12m, 2);

            // 4. CSS especiales (Art. 59 Ley 29/1976)
            decimal cssEmp = Math.Round(montoDecimo * PayrollConstants.CssTasaDecimoEmpleado, 2);
            decimal cssPat = Math.Round(montoDecimo * PayrollConstants.CssTasaDecimoPatronal, 2);

            // 5. Seguro Educativo (mismo que regular)
            bool seActivo = empleado.IsSubjectToEducationalInsurance;
            decimal seEmp = seActivo ? Math.Round(montoDecimo * PayrollConstants.SeTasaEmpleado, 2) : 0;
            decimal sePat = seActivo ? Math.Round(montoDecimo * PayrollConstants.SeTasaPatronal, 2) : 0;

            // 6. ISR del décimo: (última quincena + monto_décimo) × 13 → tramos → / 13
            decimal isr = 0;
            if (empleado.IsSubjectToIncomeTax && taxBrackets.Count > 0)
            {
                var ultimaQuincena = await _context.PayrollDetails
                    .Where(d => d.TenantId == tenantId
                             && d.EmpleadoId == empleado.Id
                             && d.PayrollHeader.PeriodEndDate <= planilla.PeriodoHasta)
                    .OrderByDescending(d => d.PayrollHeader.PeriodEndDate)
                    .Select(d => d.GrossPay)
                    .FirstOrDefaultAsync();

                decimal baseDecimo = ultimaQuincena + montoDecimo;
                decimal annualBase = baseDecimo * 13m;

                // Deducción por dependientes
                decimal depDeduccion = 0;
                if (taxConfig != null)
                {
                    var validDeps = Math.Min(empleado.Dependents, taxConfig.MaxDependents);
                    depDeduccion = validDeps * taxConfig.DependentDeductionAmount;
                }

                decimal netGravable = Math.Max(0, annualBase - depDeduccion);
                decimal isrAnual = CalcularIsrAnual(netGravable, taxBrackets);
                isr = Math.Round(isrAnual / 13m, 2);
            }

            // 7. Totales
            decimal totalDedEmp = cssEmp + seEmp + isr;
            decimal neto = montoDecimo - totalDedEmp;

            // 8. Guardar DesgloseMensual como JSON
            var desgloseJson = JsonSerializer.Serialize(desgloseMensual);

            var detalle = new DetalleDecimo
            {
                TenantId = tenantId,
                PlanillaDecimoId = planilla.Id,
                EmpleadoId = empleado.Id,
                DesgloseMensualJson = desgloseJson,
                TotalDevengado = totalDev,
                MontoDecimo = montoDecimo,
                CssEmpleado = cssEmp,
                CssPatrono = cssPat,
                SeEmpleado = seEmp,
                SePatrono = sePat,
                ISR = isr,
                TotalDeducciones = totalDedEmp,
                NetoPago = neto,
                CreatedAt = DateTime.UtcNow
            };

            _context.DetallesDecimo.Add(detalle);

            totalDevengado += totalDev;
            totalDecimo += montoDecimo;
            totalCssEmp += cssEmp;
            totalCssPat += cssPat;
            totalSeEmp += seEmp;
            totalSePat += sePat;
            totalIsr += isr;
            totalNeto += neto;
        }

        // 9. Actualizar cabecera
        planilla.TotalDevengado = totalDevengado;
        planilla.TotalDecimo = totalDecimo;
        planilla.TotalCssEmpleado = totalCssEmp;
        planilla.TotalCssPatrono = totalCssPat;
        planilla.TotalSeEmpleado = totalSeEmp;
        planilla.TotalSePatrono = totalSePat;
        planilla.TotalISR = totalIsr;
        planilla.TotalNetoPago = totalNeto;
        planilla.Estado = EstadoDecimo.Calculada;
        planilla.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return Ok(new { message = $"Décimo calculado para {empleados.Count} empleados", totalDecimo });
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

    // ====================================================================
    // Cálculo ISR anual con tramos progresivos (igual que IncomeTaxService)
    // ====================================================================
    private static decimal CalcularIsrAnual(decimal netGravable, List<Vorluno.Planilla.Application.DTOs.TaxBracketDto> brackets)
    {
        var ordered = brackets.OrderBy(b => b.MinIncome).ToList();

        Vorluno.Planilla.Application.DTOs.TaxBracketDto? bracket = null;
        foreach (var b in ordered)
        {
            if (netGravable > b.MinIncome)
                bracket = b;
            else
                break;
        }

        if (bracket == null) return 0m;

        var excess = netGravable - bracket.MinIncome;
        var bracketTax = Math.Round(excess * bracket.Rate / 100m, 2);
        return Math.Round(bracket.FixedAmount + bracketTax, 2);
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

public record DesgloseMensualItem(int Ano, int Mes, decimal Bruto);
