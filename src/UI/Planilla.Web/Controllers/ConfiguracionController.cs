// ====================================================================
// Planilla - ConfiguracionController
// Descripción: Endpoints para consultar y gestionar configuración de planilla (CSS, SE, ISR)
// ====================================================================

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Vorluno.Planilla.Application.DTOs;
using Vorluno.Planilla.Application.Interfaces;
using Vorluno.Planilla.Infrastructure.Data;

namespace Vorluno.Planilla.Web.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class ConfiguracionController : ControllerBase
{
    private readonly IPayrollConfigProvider _configProvider;
    private readonly ITenantContext _tenantContext;
    private readonly ApplicationDbContext _context;
    private readonly ILogger<ConfiguracionController> _logger;

    public ConfiguracionController(
        IPayrollConfigProvider configProvider,
        ITenantContext tenantContext,
        ApplicationDbContext context,
        ILogger<ConfiguracionController> logger)
    {
        _configProvider = configProvider ?? throw new ArgumentNullException(nameof(configProvider));
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Obtiene la configuración de impuestos (CSS, SE, ISR) vigente para el tenant actual.
    /// GET /api/configuracion/tax-config
    /// </summary>
    [HttpGet("tax-config")]
    [Authorize(Roles = "Owner,Admin,Manager,Accountant")]
    public async Task<ActionResult<PayrollTaxConfigDto>> GetTaxConfig()
    {
        var tenantId = _tenantContext.TenantId;
        if (tenantId <= 0)
            return BadRequest(new { message = "No se pudo determinar la empresa" });

        var config = await _configProvider.GetTaxConfigAsync(tenantId, DateTime.UtcNow);
        if (config == null)
            return NotFound(new { message = "No existe configuración de impuestos vigente. Créala desde el botón inferior." });

        return Ok(config);
    }

    /// <summary>
    /// Crea la configuración de impuestos por defecto (Ley 462 Panamá) si no existe.
    /// POST /api/configuracion/ensure-tax-config
    /// </summary>
    [HttpPost("ensure-tax-config")]
    [Authorize(Roles = "Owner,Admin,Manager")]
    public async Task<ActionResult> EnsureTaxConfig()
    {
        var tenantId = _tenantContext.TenantId;
        if (tenantId <= 0)
            return BadRequest(new { message = "No se pudo determinar la empresa" });

        try
        {
            await PayrollConfigSeeder.SeedForNewTenantAsync(_context, tenantId, _logger);
            return Ok(new { message = "Configuración de planilla creada o verificada correctamente" });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "EnsureTaxConfig failed for tenant {TenantId}", tenantId);
            return StatusCode(500, new { message = "No se pudo crear la configuración. Intente de nuevo o contacte al administrador." });
        }
    }

    /// <summary>
    /// Obtiene el salario mínimo legal configurado para el tenant.
    /// GET /api/configuracion/salario-minimo
    /// </summary>
    [HttpGet("salario-minimo")]
    [Authorize(Roles = "Owner,Admin,Manager,Accountant")]
    public async Task<ActionResult> GetSalarioMinimo()
    {
        var tenantId = _tenantContext.TenantId;

        var config = await _context.PayrollTaxConfigurations
            .Where(c => c.TenantId == tenantId && c.IsActive &&
                        c.EffectiveStartDate <= DateTime.UtcNow &&
                        (c.EffectiveEndDate == null || c.EffectiveEndDate >= DateTime.UtcNow))
            .OrderByDescending(c => c.EffectiveStartDate)
            .FirstOrDefaultAsync();

        if (config == null)
            return NotFound(new { message = "No existe configuracion de planilla vigente" });

        return Ok(new
        {
            salarioMinimoLegal = config.SalarioMinimoLegal,
            actividadEconomica = config.ActividadEconomica,
            configId = config.Id
        });
    }

    /// <summary>
    /// Actualiza el salario mínimo legal y actividad económica del tenant.
    /// PUT /api/configuracion/salario-minimo
    /// </summary>
    [HttpPut("salario-minimo")]
    [Authorize(Roles = "Owner,Admin")]
    public async Task<ActionResult> UpdateSalarioMinimo([FromBody] UpdateSalarioMinimoRequest request)
    {
        var tenantId = _tenantContext.TenantId;

        var config = await _context.PayrollTaxConfigurations
            .Where(c => c.TenantId == tenantId && c.IsActive &&
                        c.EffectiveStartDate <= DateTime.UtcNow &&
                        (c.EffectiveEndDate == null || c.EffectiveEndDate >= DateTime.UtcNow))
            .OrderByDescending(c => c.EffectiveStartDate)
            .FirstOrDefaultAsync();

        if (config == null)
            return NotFound(new { message = "No existe configuracion de planilla vigente" });

        if (request.SalarioMinimoLegal <= 0)
            return BadRequest(new { message = "El salario minimo debe ser mayor a cero" });

        config.SalarioMinimoLegal = request.SalarioMinimoLegal;
        config.ActividadEconomica = request.ActividadEconomica;
        config.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Salario minimo actualizado exitosamente",
            salarioMinimoLegal = config.SalarioMinimoLegal,
            actividadEconomica = config.ActividadEconomica
        });
    }
}

public record UpdateSalarioMinimoRequest(
    decimal SalarioMinimoLegal,
    string? ActividadEconomica = null
);
