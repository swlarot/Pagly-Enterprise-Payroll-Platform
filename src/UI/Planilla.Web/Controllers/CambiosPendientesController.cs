using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vorluno.Planilla.Application.Interfaces;
using Vorluno.Planilla.Domain.Enums;
using Vorluno.Planilla.Infrastructure.Services;
using Vorluno.Planilla.Web.Authorization;

namespace Vorluno.Planilla.Web.Controllers;

public record RechazarCambioRequest(string? Comentario);
public record ActivarCambiosSensiblesRequest(bool Activo);

/// <summary>
/// Cambios de salario y bajas que esperan la confirmación de otra persona
/// (si la empresa activó esa opción en Configuración).
/// </summary>
[Authorize]
[ApiController]
[Route("api/cambios-pendientes")]
[RequirePermission(SystemPermission.EmployeesUpdate, SystemPermission.EmployeesDelete)]
public class CambiosPendientesController : ControllerBase
{
    private readonly CambiosSensiblesService _cambios;
    private readonly ITenantContext _tenant;

    public CambiosPendientesController(CambiosSensiblesService cambios, ITenantContext tenant)
    {
        _cambios = cambios;
        _tenant = tenant;
    }

    private string Usuario => _tenant.UserId ?? User.FindFirst("sub")?.Value ?? string.Empty;

    private IActionResult Resultado(string? error) =>
        error == null ? NoContent()
        : error.Contains("no existe") ? NotFound(new { message = error })
        : BadRequest(new { message = error });

    [HttpGet]
    public async Task<IActionResult> Listar() => Ok(await _cambios.ListarAsync(Usuario));

    [HttpPost("{id:int}/confirmar")]
    public async Task<IActionResult> Confirmar(int id) => Resultado(await _cambios.ConfirmarAsync(id, Usuario));

    [HttpPost("{id:int}/rechazar")]
    public async Task<IActionResult> Rechazar(int id, [FromBody] RechazarCambioRequest request) =>
        Resultado(await _cambios.RechazarAsync(id, Usuario, request?.Comentario));

    [HttpPost("{id:int}/retirar")]
    public async Task<IActionResult> Retirar(int id) => Resultado(await _cambios.RetirarAsync(id, Usuario));

    /// <summary>¿Está activa la confirmación de otra persona? Lo ve quien gestiona empleados.</summary>
    [HttpGet("opcion")]
    public async Task<IActionResult> Opcion() => Ok(new { activo = await _cambios.ActivoAsync() });

    /// <summary>Activa o desactiva la confirmación. Solo el dueño (gestión de roles).</summary>
    [HttpPut("opcion")]
    [RequirePermission(SystemPermission.SettingsRoles)]
    public async Task<IActionResult> CambiarOpcion([FromBody] ActivarCambiosSensiblesRequest request)
    {
        await _cambios.ActivarAsync(request.Activo, Usuario);
        return Ok(new { activo = request.Activo });
    }
}
