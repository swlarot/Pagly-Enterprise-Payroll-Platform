using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vorluno.Planilla.Infrastructure.Data;
using Vorluno.Planilla.Infrastructure.Services;

namespace Vorluno.Planilla.Web.Controllers;

/// <summary>
/// Protocolo «servidor en la nube» (ADMS / PUSH) de los relojes ZKTeco: el
/// reloj saluda, Pagly le dice qué enviar (solo marcaciones: nada de fotos,
/// huellas ni rostros) y el reloj envía cada marcación por su cuenta.
///
/// El reloj no inicia sesión: se acepta solo si su número de serie está
/// registrado y activo en una empresa. A uno desconocido se le responde 401
/// para que conserve sus marcas y reintente cuando lo registren.
/// </summary>
[AllowAnonymous]
[ApiController]
[Route("iclock")]
public class IclockController : ControllerBase
{
    private const long TamanoMaximo = 1024 * 1024; // un envío del reloj es texto; 1 MB sobra

    private readonly RelojAsistenciaService _reloj;
    private readonly ApplicationDbContext _context;
    private readonly ILogger<IclockController> _logger;

    public IclockController(RelojAsistenciaService reloj, ApplicationDbContext context, ILogger<IclockController> logger)
    {
        _reloj = reloj;
        _context = context;
        _logger = logger;
    }

    private ContentResult Texto(string cuerpo, int estado = 200) =>
        new() { Content = cuerpo, ContentType = "text/plain; charset=utf-8", StatusCode = estado };

    /// <summary>Saludo del reloj: GET /iclock/cdata?SN=XXXX&amp;options=all</summary>
    [HttpGet("cdata")]
    [HttpGet("cdata.aspx")]
    public async Task<IActionResult> Saludo([FromQuery(Name = "SN")] string? sn)
    {
        var dispositivo = await _reloj.DispositivoAsync(sn);
        if (dispositivo == null)
        {
            _logger.LogWarning("Reloj sin registrar intentó conectarse: {SN}", sn);
            return Texto("Unauthorized", 401);
        }

        dispositivo.UltimaConexion = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        // Solo marcaciones (TransFlag=TransData AttLog): ni fotos ni datos biométricos.
        // OPERLOGStamp=9999 evita que envíe su bitácora de operaciones.
        return Texto(string.Join("\n", new[]
        {
            $"GET OPTION FROM: {dispositivo.NumeroSerie}",
            "ATTLOGStamp=None",
            "OPERLOGStamp=9999",
            "ATTPHOTOStamp=None",
            "ErrorDelay=30",
            "Delay=10",
            "TransTimes=00:00;14:05",
            "TransInterval=1",
            "TransFlag=TransData AttLog",
            "TimeZone=-5",
            "Realtime=1",
            "Encrypt=None",
        }) + "\n");
    }

    /// <summary>Envío del reloj: POST /iclock/cdata?SN=XXXX&amp;table=ATTLOG (una marcación por línea).</summary>
    [HttpPost("cdata")]
    [HttpPost("cdata.aspx")]
    [RequestSizeLimit(TamanoMaximo)]
    public async Task<IActionResult> Envio([FromQuery(Name = "SN")] string? sn, [FromQuery] string? table)
    {
        var dispositivo = await _reloj.DispositivoAsync(sn);
        if (dispositivo == null)
        {
            _logger.LogWarning("Reloj sin registrar intentó enviar datos: {SN}", sn);
            return Texto("Unauthorized", 401);
        }

        using var lector = new StreamReader(Request.Body);
        var cuerpo = await lector.ReadToEndAsync();

        // Otras tablas (bitácora, fotos, usuarios) no se guardan.
        if (!string.Equals(table, "ATTLOG", StringComparison.OrdinalIgnoreCase))
            return Texto("OK");

        var recibidas = await _reloj.RecibirDelRelojAsync(dispositivo, cuerpo);
        return Texto($"OK: {recibidas}");
    }

    /// <summary>El reloj pregunta si hay órdenes para él: no hay ninguna.</summary>
    [HttpGet("getrequest")]
    [HttpGet("getrequest.aspx")]
    public async Task<IActionResult> Ordenes([FromQuery(Name = "SN")] string? sn)
    {
        var dispositivo = await _reloj.DispositivoAsync(sn);
        if (dispositivo == null) return Texto("Unauthorized", 401);
        dispositivo.UltimaConexion = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return Texto("OK");
    }

    /// <summary>Respuesta del reloj a una orden (Pagly no envía órdenes).</summary>
    [HttpPost("devicecmd")]
    [HttpPost("devicecmd.aspx")]
    public async Task<IActionResult> RespuestaOrden([FromQuery(Name = "SN")] string? sn)
    {
        var dispositivo = await _reloj.DispositivoAsync(sn);
        return dispositivo == null ? Texto("Unauthorized", 401) : Texto("OK");
    }
}
