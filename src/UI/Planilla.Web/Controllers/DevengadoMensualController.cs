using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Vorluno.Planilla.Application.Interfaces;
using Vorluno.Planilla.Domain.Enums;
using Vorluno.Planilla.Infrastructure.Data;
using Vorluno.Planilla.Web.Authorization;

namespace Vorluno.Planilla.Web.Controllers;

/// <summary>
/// Salarios históricos por mes de un empleado: la cuadrícula de 12 × 5 años.
/// Los meses con planilla en Pagly se derivan y no se editan; los demás se
/// pueden escribir a mano (por ejemplo, un mes que faltaba para el décimo).
/// </summary>
[ApiController]
[Route("api/empleados/{empleadoId:int}/devengado-mensual")]
[Authorize]
public class DevengadoMensualController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ITenantContext _tenantContext;
    private readonly IDevengadoMensualService _devengado;

    public DevengadoMensualController(ApplicationDbContext context, ITenantContext tenantContext, IDevengadoMensualService devengado)
    {
        _context = context;
        _tenantContext = tenantContext;
        _devengado = devengado;
    }

    public sealed record MesDevengadoDto(
        int Anio, int Mes, decimal Salario, decimal Vacaciones, decimal Extras, decimal Comision,
        decimal Total, string Origen, bool Editable);

    public sealed record GuardarMesRequest(
        decimal Salario, decimal Vacaciones = 0m, decimal Extras = 0m, decimal Comision = 0m, string? Nota = null);

    // ====================================================================
    // GET /api/empleados/{id}/devengado-mensual?desde=2021-10&hasta=2026-09
    // Sin parámetros: los últimos 60 meses.
    // ====================================================================
    [HttpGet]
    [RequirePermission(SystemPermission.EmployeesRead)]
    public async Task<ActionResult<IEnumerable<MesDevengadoDto>>> Get(
        int empleadoId, [FromQuery] string? desde, [FromQuery] string? hasta, CancellationToken ct)
    {
        if (!await ExisteAsync(empleadoId, ct)) return NotFound(new { message = "Empleado no encontrado." });

        var hoy = Application.Helpers.DateTimeHelper.NowPanama();
        var fin = ParsearMes(hasta) ?? new DateTime(hoy.Year, hoy.Month, 1);
        var inicio = ParsearMes(desde) ?? fin.AddMonths(-59);
        if (inicio > fin) return BadRequest(new { message = "El rango de meses no es válido." });
        if ((fin.Year - inicio.Year) * 12 + fin.Month - inicio.Month > 240)
            return BadRequest(new { message = "El rango no puede pasar de 20 años." });

        var meses = await _devengado.ObtenerMesesAsync(empleadoId, inicio, fin, ct);
        return Ok(meses.Select(m => new MesDevengadoDto(
            m.Anio, m.Mes, m.Salario, m.Vacaciones, m.Extras, m.Comision, m.Total,
            m.Origen.ToString(), Editable: m.Origen != OrigenDevengado.Planilla)));
    }

    // ====================================================================
    // PUT /api/empleados/{id}/devengado-mensual/{anio}/{mes}
    // ====================================================================
    [HttpPut("{anio:int}/{mes:int}")]
    [RequirePermission(SystemPermission.EmployeesUpdate)]
    public async Task<IActionResult> Guardar(int empleadoId, int anio, int mes, [FromBody] GuardarMesRequest req, CancellationToken ct)
    {
        if (!await ExisteAsync(empleadoId, ct)) return NotFound(new { message = "Empleado no encontrado." });
        if (mes is < 1 or > 12 || anio < 1990 || anio > 2100) return BadRequest(new { message = "Mes o año fuera de rango." });
        if (req.Salario < 0 || req.Vacaciones < 0 || req.Extras < 0 || req.Comision < 0)
            return BadRequest(new { message = "Los montos no pueden ser negativos." });

        try
        {
            await _devengado.GuardarMesAsync(empleadoId, anio, mes, req.Salario, req.Vacaciones, req.Extras, req.Comision,
                OrigenDevengado.Manual, req.Nota, ct);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    // ====================================================================
    // DELETE /api/empleados/{id}/devengado-mensual/{anio}/{mes}
    // Solo quita un mes importado/manual (vuelve a "Sin datos"). Nunca toca planillas.
    // ====================================================================
    [HttpDelete("{anio:int}/{mes:int}")]
    [RequirePermission(SystemPermission.EmployeesUpdate)]
    public async Task<IActionResult> Quitar(int empleadoId, int anio, int mes, CancellationToken ct)
    {
        if (!await ExisteAsync(empleadoId, ct)) return NotFound(new { message = "Empleado no encontrado." });

        var registro = await _context.DevengadosMensuales
            .FirstOrDefaultAsync(d => d.EmpleadoId == empleadoId && d.Anio == anio && d.Mes == mes, ct);
        if (registro is null) return NotFound(new { message = "Ese mes no tiene dato cargado." });

        _context.DevengadosMensuales.Remove(registro);
        await _context.SaveChangesAsync(ct);
        return NoContent();
    }

    private Task<bool> ExisteAsync(int empleadoId, CancellationToken ct)
    {
        var tenantId = _tenantContext.TenantId;
        return _context.Empleados.AnyAsync(e => e.Id == empleadoId && e.TenantId == tenantId && !e.IsDeleted, ct);
    }

    private static DateTime? ParsearMes(string? s)
        => DateTime.TryParseExact(s, "yyyy-MM", System.Globalization.CultureInfo.InvariantCulture,
               System.Globalization.DateTimeStyles.None, out var d) ? new DateTime(d.Year, d.Month, 1) : null;
}
