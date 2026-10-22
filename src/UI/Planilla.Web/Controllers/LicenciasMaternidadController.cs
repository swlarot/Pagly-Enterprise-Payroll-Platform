using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Vorluno.Planilla.Application.Helpers;
using Vorluno.Planilla.Application.Interfaces;
using Vorluno.Planilla.Domain.Entities;
using Vorluno.Planilla.Domain.Enums;
using Vorluno.Planilla.Infrastructure.Data;
using Vorluno.Planilla.Web.Authorization;

namespace Vorluno.Planilla.Web.Controllers;

public record GuardarLicenciaRequest(int EmpleadoId, DateTime FechaInicio, DateTime? FechaFin, string? Observaciones);
public record AnularLicenciaRequest(string? Motivo);

/// <summary>
/// Licencias de maternidad: mientras duran, la colaboradora no cobra salario
/// en la planilla (la CSS paga el subsidio) y vuelve sola al terminar. Se
/// anulan, no se borran.
/// </summary>
[Authorize]
[ApiController]
[Route("api/licencias-maternidad")]
public class LicenciasMaternidadController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ITenantContext _tenant;
    private readonly IAuditLogService _audit;

    public LicenciasMaternidadController(ApplicationDbContext context, ITenantContext tenant, IAuditLogService audit)
    {
        _context = context;
        _tenant = tenant;
        _audit = audit;
    }

    private string Usuario => _tenant.UserId ?? User.FindFirst("sub")?.Value ?? "Sistema";

    private static string Estado(LicenciaMaternidad l, DateTime hoy) =>
        l.Anulada ? "Anulada"
        : hoy < l.FechaInicio.Date ? "Programada"
        : hoy <= l.FechaFin.Date ? "En curso"
        : "Terminada";

    private static object ADto(LicenciaMaternidad l, DateTime hoy) => new
    {
        l.Id,
        l.EmpleadoId,
        empleado = $"{l.Empleado.Nombre} {l.Empleado.Apellido}",
        cedula = l.Empleado.NumeroIdentificacion,
        l.FechaInicio,
        l.FechaFin,
        regreso = l.FechaFin.Date.AddDays(1),
        dias = (int)(l.FechaFin.Date - l.FechaInicio.Date).TotalDays + 1,
        estado = Estado(l, hoy),
        fueroHasta = l.FueroHasta,
        l.Observaciones,
        l.Anulada,
        l.AnuladaEn,
        l.MotivoAnulacion,
    };

    /// <summary>Todas las licencias, la más reciente primero. GET api/licencias-maternidad?incluirAnuladas=true</summary>
    [HttpGet]
    [RequirePermission(SystemPermission.AbsencesManage, SystemPermission.EmployeesRead)]
    public async Task<IActionResult> Listar([FromQuery] bool incluirAnuladas = false)
    {
        var tenantId = _tenant.TenantId;
        var hoy = DateTimeHelper.NowPanama().Date;
        var lista = await _context.LicenciasMaternidad.AsNoTracking()
            .Include(l => l.Empleado)
            .Where(l => l.TenantId == tenantId && (incluirAnuladas || !l.Anulada))
            .OrderByDescending(l => l.FechaInicio)
            .ToListAsync();
        return Ok(lista.Select(l => ADto(l, hoy)));
    }

    [HttpPost]
    [RequirePermission(SystemPermission.AbsencesManage)]
    public async Task<IActionResult> Crear([FromBody] GuardarLicenciaRequest request)
    {
        var tenantId = _tenant.TenantId;
        var empleado = await _context.Empleados
            .FirstOrDefaultAsync(e => e.Id == request.EmpleadoId && e.TenantId == tenantId && e.EstaActivo);
        if (empleado == null)
            return BadRequest(new { message = "Esa colaboradora no existe o no está activa." });

        var (inicio, fin, error) = Validar(request);
        if (error != null) return BadRequest(new { message = error });

        if (await SeCruzaAsync(empleado.Id, inicio, fin, excluirId: null))
            return BadRequest(new { message = "Ya tiene una licencia de maternidad en esas fechas." });

        var licencia = new LicenciaMaternidad
        {
            TenantId = tenantId,
            EmpleadoId = empleado.Id,
            FechaInicio = inicio,
            FechaFin = fin,
            Observaciones = string.IsNullOrWhiteSpace(request.Observaciones) ? null : request.Observaciones.Trim(),
            CreadaPor = Usuario,
            CreatedAt = DateTime.UtcNow,
        };
        _context.LicenciasMaternidad.Add(licencia);
        await _context.SaveChangesAsync();

        await _audit.LogAsync("LicenciaMaternidadRegistrada", "LicenciaMaternidad", licencia.Id.ToString(), new Dictionary<string, string>
        {
            ["empleadoId"] = empleado.Id.ToString(),
            ["inicio"] = inicio.ToString("yyyy-MM-dd"),
            ["fin"] = fin.ToString("yyyy-MM-dd"),
        });

        licencia.Empleado = empleado;
        return StatusCode(201, new { licencia = ADto(licencia, DateTimeHelper.NowPanama().Date), avisos = await AvisosPlanillasAsync(empleado.Id, inicio, fin) });
    }

    /// <summary>Ajusta las fechas (por ejemplo, el regreso real) o las observaciones.</summary>
    [HttpPut("{id:int}")]
    [RequirePermission(SystemPermission.AbsencesManage)]
    public async Task<IActionResult> Actualizar(int id, [FromBody] GuardarLicenciaRequest request)
    {
        var tenantId = _tenant.TenantId;
        var licencia = await _context.LicenciasMaternidad.Include(l => l.Empleado)
            .FirstOrDefaultAsync(l => l.Id == id && l.TenantId == tenantId);
        if (licencia == null) return NotFound(new { message = "Esa licencia no existe." });
        if (licencia.Anulada) return BadRequest(new { message = "Una licencia anulada no se modifica." });

        var (inicio, fin, error) = Validar(request);
        if (error != null) return BadRequest(new { message = error });
        if (await SeCruzaAsync(licencia.EmpleadoId, inicio, fin, excluirId: id))
            return BadRequest(new { message = "Esas fechas se cruzan con otra licencia suya." });

        var antes = $"{licencia.FechaInicio:yyyy-MM-dd} → {licencia.FechaFin:yyyy-MM-dd}";
        licencia.FechaInicio = inicio;
        licencia.FechaFin = fin;
        licencia.Observaciones = string.IsNullOrWhiteSpace(request.Observaciones) ? null : request.Observaciones.Trim();
        licencia.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        await _audit.LogAsync("LicenciaMaternidadAjustada", "LicenciaMaternidad", id.ToString(), new Dictionary<string, string>
        {
            ["antes"] = antes,
            ["despues"] = $"{inicio:yyyy-MM-dd} → {fin:yyyy-MM-dd}",
        });

        return Ok(new { licencia = ADto(licencia, DateTimeHelper.NowPanama().Date), avisos = await AvisosPlanillasAsync(licencia.EmpleadoId, inicio, fin) });
    }

    /// <summary>Anula una licencia registrada por error. Queda guardada con el motivo.</summary>
    [HttpPost("{id:int}/anular")]
    [RequirePermission(SystemPermission.AbsencesManage)]
    public async Task<IActionResult> Anular(int id, [FromBody] AnularLicenciaRequest? request)
    {
        var tenantId = _tenant.TenantId;
        var licencia = await _context.LicenciasMaternidad.Include(l => l.Empleado)
            .FirstOrDefaultAsync(l => l.Id == id && l.TenantId == tenantId);
        if (licencia == null) return NotFound(new { message = "Esa licencia no existe." });
        if (licencia.Anulada) return BadRequest(new { message = "Ya estaba anulada." });

        licencia.Anulada = true;
        licencia.AnuladaPor = Usuario;
        licencia.AnuladaEn = DateTime.UtcNow;
        licencia.MotivoAnulacion = string.IsNullOrWhiteSpace(request?.Motivo) ? null : request!.Motivo!.Trim();
        await _context.SaveChangesAsync();

        await _audit.LogAsync("LicenciaMaternidadAnulada", "LicenciaMaternidad", id.ToString(),
            new Dictionary<string, string> { ["motivo"] = licencia.MotivoAnulacion ?? string.Empty });

        return Ok(ADto(licencia, DateTimeHelper.NowPanama().Date));
    }

    private static (DateTime Inicio, DateTime Fin, string? Error) Validar(GuardarLicenciaRequest r)
    {
        var inicio = DateTime.SpecifyKind(r.FechaInicio.Date, DateTimeKind.Utc);
        var fin = DateTime.SpecifyKind((r.FechaFin ?? LicenciaMaternidad.FinSugerido(inicio)).Date, DateTimeKind.Utc);
        if (fin < inicio) return (inicio, fin, "El regreso tiene que ser después del inicio.");
        if ((fin - inicio).TotalDays > 365) return (inicio, fin, "Una licencia de maternidad no puede pasar de un año.");
        if (r.Observaciones?.Length > 500) return (inicio, fin, "Las observaciones no pueden pasar de 500 caracteres.");
        return (inicio, fin, null);
    }

    private Task<bool> SeCruzaAsync(int empleadoId, DateTime inicio, DateTime fin, int? excluirId)
    {
        var tenantId = _tenant.TenantId;
        return _context.LicenciasMaternidad.AnyAsync(l =>
            l.TenantId == tenantId && l.EmpleadoId == empleadoId && !l.Anulada
            && (excluirId == null || l.Id != excluirId)
            && l.FechaInicio <= fin && l.FechaFin >= inicio);
    }

    /// <summary>
    /// Planillas ya aprobadas o pagadas que cubren días de la licencia: esas no
    /// se recalculan solas, así que se avisa para que se revisen.
    /// </summary>
    private async Task<List<string>> AvisosPlanillasAsync(int empleadoId, DateTime inicio, DateTime fin)
    {
        var tenantId = _tenant.TenantId;
        var planillas = await _context.PayrollDetails.AsNoTracking()
            .Where(d => d.TenantId == tenantId && d.EmpleadoId == empleadoId
                     && (d.PayrollHeader!.Status == PayrollStatus.Approved || d.PayrollHeader.Status == PayrollStatus.Paid)
                     && d.PayrollHeader.PeriodStartDate <= fin && d.PayrollHeader.PeriodEndDate >= inicio)
            .Select(d => new { d.PayrollHeader!.PayrollNumber, d.PayrollHeader.PeriodStartDate, d.PayrollHeader.PeriodEndDate })
            .ToListAsync();
        return planillas
            .Select(p => $"La planilla {p.PayrollNumber} ({p.PeriodStartDate:dd/MM}–{p.PeriodEndDate:dd/MM}) ya está aprobada y le pagó salario en días de la licencia. Revísala con su contador.")
            .ToList();
    }
}
