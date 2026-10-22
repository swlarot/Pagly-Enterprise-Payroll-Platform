using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Vorluno.Planilla.Application.Interfaces;
using Vorluno.Planilla.Infrastructure.Data;

namespace Vorluno.Planilla.Web.Controllers;

/// <summary>
/// La campana: los avisos de la persona que entra. Solo los suyos; se marcan
/// como leídos y no se borran.
/// </summary>
[Authorize]
[ApiController]
[Route("api/[controller]")]
public class AvisosController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ITenantContext _tenant;

    public AvisosController(ApplicationDbContext context, ITenantContext tenant)
    {
        _context = context;
        _tenant = tenant;
    }

    private string Usuario => _tenant.UserId ?? User.FindFirst("sub")?.Value ?? string.Empty;

    /// <summary>Los últimos avisos y cuántos faltan por leer. GET api/avisos?limite=20</summary>
    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] int limite = 20)
    {
        var tenantId = _tenant.TenantId;
        var usuario = Usuario;
        var mios = _context.Avisos.AsNoTracking().Where(a => a.TenantId == tenantId && a.UsuarioId == usuario);

        var items = await mios
            .OrderByDescending(a => a.CreatedAt).ThenByDescending(a => a.Id)
            .Take(Math.Clamp(limite, 1, 100))
            .Select(a => new { a.Id, a.Titulo, a.Mensaje, a.Enlace, a.Leido, a.CreatedAt, a.SolicitudRevisionId })
            .ToListAsync();
        var noLeidos = await mios.CountAsync(a => !a.Leido);

        return Ok(new { items, noLeidos });
    }

    [HttpPost("{id:int}/leido")]
    public async Task<IActionResult> MarcarLeido(int id)
    {
        var tenantId = _tenant.TenantId;
        var usuario = Usuario;
        var aviso = await _context.Avisos.FirstOrDefaultAsync(a => a.Id == id && a.TenantId == tenantId && a.UsuarioId == usuario);
        if (aviso == null) return NotFound(new { message = "Ese aviso no existe." });
        if (!aviso.Leido)
        {
            aviso.Leido = true;
            aviso.LeidoEn = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
        return NoContent();
    }

    [HttpPost("leer-todos")]
    public async Task<IActionResult> LeerTodos()
    {
        var tenantId = _tenant.TenantId;
        var usuario = Usuario;
        var ahora = DateTime.UtcNow;
        var pendientes = await _context.Avisos
            .Where(a => a.TenantId == tenantId && a.UsuarioId == usuario && !a.Leido)
            .ToListAsync();
        foreach (var a in pendientes) { a.Leido = true; a.LeidoEn = ahora; }
        await _context.SaveChangesAsync();
        return Ok(new { marcados = pendientes.Count });
    }
}
