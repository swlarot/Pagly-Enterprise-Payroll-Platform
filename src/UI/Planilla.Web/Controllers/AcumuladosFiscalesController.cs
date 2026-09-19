using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Vorluno.Planilla.Application.DTOs;
using Vorluno.Planilla.Application.Interfaces;
using Vorluno.Planilla.Domain.Entities;
using Vorluno.Planilla.Domain.Enums;
using Vorluno.Planilla.Infrastructure.Data;
using Vorluno.Planilla.Web.Authorization;

namespace Vorluno.Planilla.Web.Controllers;

/// <summary>
/// Ficha anual de ISR por empleado y saldos iniciales de migración.
///
/// Los saldos iniciales son lo que el empleado ya tenía acumulado en el año
/// cuando la empresa entró a esta plataforma. Sin ellos, migrar a mitad de año
/// le cobraría de nuevo un impuesto que ya se le retuvo.
/// </summary>
[ApiController]
[Route("api/acumulados-fiscales")]
[Authorize]
public class AcumuladosFiscalesController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ITenantContext _tenantContext;
    private readonly IAcumuladoFiscalService _acumuladoFiscalService;

    public AcumuladosFiscalesController(
        ApplicationDbContext context,
        ITenantContext tenantContext,
        IAcumuladoFiscalService acumuladoFiscalService)
    {
        _context = context;
        _tenantContext = tenantContext;
        _acumuladoFiscalService = acumuladoFiscalService;
    }

    // ====================================================================
    // GET /api/acumulados-fiscales/{empleadoId}/ficha/{anio}
    // Ficha anual de ISR: una fila por corrida con proyección y retención.
    // ====================================================================
    [HttpGet("{empleadoId:int}/ficha/{anio:int}")]
    [RequirePermission(SystemPermission.PayrollView)]
    public async Task<ActionResult<FichaIsrAnualDto>> GetFicha(int empleadoId, int anio)
    {
        if (anio < 2000 || anio > 2100)
            return BadRequest(new { message = "Año fuera de rango." });

        var ficha = await _acumuladoFiscalService.ObtenerFichaAnualAsync(empleadoId, anio);

        if (ficha is null)
            return NotFound(new { message = "Empleado no encontrado." });

        return Ok(ficha);
    }

    // ====================================================================
    // GET /api/acumulados-fiscales/{empleadoId}/ficha/{anio}/excel
    // La misma hoja del contador, con sus fórmulas, para comparar celda a celda.
    // ====================================================================
    [HttpGet("{empleadoId:int}/ficha/{anio:int}/excel")]
    [RequirePermission(SystemPermission.PayrollView)]
    public async Task<IActionResult> GetFichaExcel(int empleadoId, int anio)
    {
        if (anio < 2000 || anio > 2100)
            return BadRequest(new { message = "Año fuera de rango." });

        var ficha = await _acumuladoFiscalService.ObtenerFichaAnualAsync(empleadoId, anio);
        if (ficha is null)
            return NotFound(new { message = "Empleado no encontrado." });

        var bytes = Infrastructure.Services.FichaIsrExcelExporter.Exportar(ficha);
        var nombre = $"CALCULO RENTA {ficha.Empleado} {anio}.xlsx";
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", nombre);
    }

    // ====================================================================
    // GET /api/acumulados-fiscales/{empleadoId}/saldos/{anio}
    // ====================================================================
    [HttpGet("{empleadoId:int}/saldos/{anio:int}")]
    [RequirePermission(SystemPermission.PayrollView)]
    public async Task<ActionResult<SaldosInicialesDto>> GetSaldos(int empleadoId, int anio)
    {
        var tenantId = _tenantContext.TenantId;

        var saldos = await _context.AcumuladosFiscalesEmpleados
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.TenantId == tenantId
                                   && a.EmpleadoId == empleadoId
                                   && a.Anio == anio);

        // Un empleado sin saldos cargados arranca el año en cero; no es un error.
        return Ok(new SaldosInicialesDto
        {
            EmpleadoId = empleadoId,
            Anio = anio,
            IngresoGravableInicial = saldos?.IngresoGravableInicial ?? 0m,
            DecimoInicial = saldos?.DecimoInicial ?? 0m,
            PartidasDecimoInicial = saldos?.PartidasDecimoInicial ?? 0,
            IsrRetenidoInicial = saldos?.IsrRetenidoInicial ?? 0m,
            GastoRepresentacionInicial = saldos?.GastoRepresentacionInicial ?? 0m,
            IsrGastoRepresentacionInicial = saldos?.IsrGastoRepresentacionInicial ?? 0m
        });
    }

    // ====================================================================
    // PUT /api/acumulados-fiscales/{empleadoId}/saldos/{anio}
    // Carga o corrige los saldos con los que el empleado llegó al sistema.
    // ====================================================================
    [HttpPut("{empleadoId:int}/saldos/{anio:int}")]
    [RequirePermission(SystemPermission.PayrollCalculate)]
    public async Task<ActionResult<SaldosInicialesDto>> GuardarSaldos(
        int empleadoId, int anio, [FromBody] SaldosInicialesDto dto)
    {
        if (anio < 2000 || anio > 2100)
            return BadRequest(new { message = "Año fuera de rango." });

        if (dto.IngresoGravableInicial < 0 || dto.DecimoInicial < 0 || dto.IsrRetenidoInicial < 0
            || dto.GastoRepresentacionInicial < 0 || dto.IsrGastoRepresentacionInicial < 0)
            return BadRequest(new { message = "Los saldos iniciales no pueden ser negativos." });

        var tenantId = _tenantContext.TenantId;

        var empleadoExiste = await _context.Empleados
            .AnyAsync(e => e.Id == empleadoId && e.TenantId == tenantId);

        if (!empleadoExiste)
            return NotFound(new { message = "Empleado no encontrado." });

        var saldos = await _context.AcumuladosFiscalesEmpleados
            .FirstOrDefaultAsync(a => a.TenantId == tenantId
                                   && a.EmpleadoId == empleadoId
                                   && a.Anio == anio);

        if (saldos is null)
        {
            saldos = new AcumuladoFiscalEmpleado
            {
                TenantId = tenantId,
                EmpleadoId = empleadoId,
                Anio = anio
            };
            _context.AcumuladosFiscalesEmpleados.Add(saldos);
        }
        else
        {
            saldos.UpdatedAt = DateTime.UtcNow;
        }

        saldos.IngresoGravableInicial = dto.IngresoGravableInicial;
        saldos.DecimoInicial = dto.DecimoInicial;
        saldos.PartidasDecimoInicial = Math.Clamp(dto.PartidasDecimoInicial, 0, 3);
        saldos.IsrRetenidoInicial = dto.IsrRetenidoInicial;
        saldos.GastoRepresentacionInicial = dto.GastoRepresentacionInicial;
        saldos.IsrGastoRepresentacionInicial = dto.IsrGastoRepresentacionInicial;

        await _context.SaveChangesAsync();

        return Ok(new SaldosInicialesDto
        {
            EmpleadoId = empleadoId,
            Anio = anio,
            IngresoGravableInicial = saldos.IngresoGravableInicial,
            DecimoInicial = saldos.DecimoInicial,
            PartidasDecimoInicial = saldos.PartidasDecimoInicial,
            IsrRetenidoInicial = saldos.IsrRetenidoInicial,
            GastoRepresentacionInicial = saldos.GastoRepresentacionInicial,
            IsrGastoRepresentacionInicial = saldos.IsrGastoRepresentacionInicial
        });
    }
}

/// <summary>Saldos con los que el empleado llegó al sistema en un año fiscal.</summary>
public class SaldosInicialesDto
{
    public int EmpleadoId { get; set; }
    public int Anio { get; set; }

    /// <summary>Ingreso gravable (bruto menos Seguro Social) ya acumulado al migrar.</summary>
    public decimal IngresoGravableInicial { get; set; }

    /// <summary>Décimo tercer mes ya pagado en el año antes de migrar.</summary>
    public decimal DecimoInicial { get; set; }

    /// <summary>Cuántas partidas de décimo (0 a 3) ya se habían pagado al migrar.</summary>
    public int PartidasDecimoInicial { get; set; }

    /// <summary>ISR ya retenido antes de migrar. Se descuenta del impuesto debido.</summary>
    public decimal IsrRetenidoInicial { get; set; }

    /// <summary>Gastos de representación ya pagados en el año antes de migrar.</summary>
    public decimal GastoRepresentacionInicial { get; set; }

    /// <summary>
    /// Impuesto ya retenido sobre ellos. Sin este dato el primer tramo del 10% se
    /// volvería a aplicar sobre dinero que ya lo agotó.
    /// </summary>
    public decimal IsrGastoRepresentacionInicial { get; set; }
}
