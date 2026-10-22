using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vorluno.Planilla.Application.Interfaces;
using Vorluno.Planilla.Domain.Enums;
using Vorluno.Planilla.Infrastructure.Services;

namespace Vorluno.Planilla.Web.Controllers;

public record PedirRevisionRequest(TipoRevision Tipo, int EntidadId, string RevisorId, string? Comentario);
public record ResponderRevisionRequest(string? Comentario);

/// <summary>
/// Pedir que otra persona revise una planilla, un décimo, una liquidación o
/// las horas extra de un mes, y responder (aprobar o devolver).
/// </summary>
[Authorize]
[ApiController]
[Route("api/[controller]")]
public class RevisionesController : ControllerBase
{
    private readonly RevisionesService _revisiones;
    private readonly ITenantContext _tenant;
    private readonly IConfiguration _config;

    public RevisionesController(RevisionesService revisiones, ITenantContext tenant, IConfiguration config)
    {
        _revisiones = revisiones;
        _tenant = tenant;
        _config = config;
    }

    private string Usuario => _tenant.UserId ?? User.FindFirst("sub")?.Value ?? string.Empty;

    /// <summary>
    /// Dirección de la app para los enlaces del correo. Solo de la
    /// configuración: nunca del Host de la petición, que se puede falsificar.
    /// </summary>
    private string? BaseUrl => string.IsNullOrWhiteSpace(_config["App:BaseUrl"]) ? null : _config["App:BaseUrl"];

    private IActionResult Responder(ResultadoRevision r, bool creada = false)
    {
        if (r.NoEncontrada) return NotFound(new { message = r.Error });
        if (r.Error != null) return BadRequest(new { message = r.Error });
        return creada ? StatusCode(201, r.Solicitud) : Ok(r.Solicitud);
    }

    /// <summary>A quiénes se les puede pedir revisión de un tipo de cosa. GET api/revisiones/revisores?tipo=1</summary>
    [HttpGet("revisores")]
    public async Task<IActionResult> Revisores([FromQuery] TipoRevision tipo = TipoRevision.Planilla)
    {
        if (!await _revisiones.PuedeVerAsync(Usuario, tipo)) return Forbid();
        return Ok(await _revisiones.RevisoresAsync(Usuario, tipo));
    }

    /// <summary>Historial de revisiones de una cosa. GET api/revisiones?tipo=1&amp;entidadId=5</summary>
    [HttpGet]
    public async Task<IActionResult> Historial([FromQuery] TipoRevision tipo, [FromQuery] int entidadId)
    {
        if (!await _revisiones.PuedeVerAsync(Usuario, tipo)) return Forbid();
        return Ok(await _revisiones.HistorialAsync(tipo, entidadId));
    }

    /// <summary>Lo que el usuario tiene pendiente de revisar.</summary>
    [HttpGet("para-mi")]
    public async Task<IActionResult> ParaMi() => Ok(await _revisiones.PendientesParaAsync(Usuario));

    [HttpPost]
    public async Task<IActionResult> Pedir([FromBody] PedirRevisionRequest request)
    {
        if (!await _revisiones.PuedeVerAsync(Usuario, request.Tipo)) return Forbid();
        return Responder(await _revisiones.PedirAsync(request.Tipo, request.EntidadId, request.RevisorId, request.Comentario, Usuario, BaseUrl), creada: true);
    }

    [HttpPost("{id:int}/aprobar")]
    public async Task<IActionResult> Aprobar(int id, [FromBody] ResponderRevisionRequest? request = null) =>
        Responder(await _revisiones.ResponderAsync(id, aprobar: true, request?.Comentario, Usuario, BaseUrl));

    [HttpPost("{id:int}/devolver")]
    public async Task<IActionResult> Devolver(int id, [FromBody] ResponderRevisionRequest request) =>
        Responder(await _revisiones.ResponderAsync(id, aprobar: false, request?.Comentario, Usuario, BaseUrl));

    /// <summary>Quien pidió la revisión la retira (queda en el historial como retirada).</summary>
    [HttpPost("{id:int}/retirar")]
    public async Task<IActionResult> Retirar(int id) => Responder(await _revisiones.RetirarAsync(id, Usuario));
}
