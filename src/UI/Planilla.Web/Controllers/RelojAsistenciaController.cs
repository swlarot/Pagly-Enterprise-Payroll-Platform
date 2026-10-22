using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Vorluno.Planilla.Application.Interfaces;
using Vorluno.Planilla.Domain.Entities;
using Vorluno.Planilla.Domain.Enums;
using Vorluno.Planilla.Infrastructure.Data;
using Vorluno.Planilla.Infrastructure.Services;
using Vorluno.Planilla.Web.Authorization;

namespace Vorluno.Planilla.Web.Controllers;

public record AsignarCodigoRequest(string Codigo, int? EmpleadoId);
public record RangoFechasRequest(DateTime Desde, DateTime Hasta);
public record GuardarHorarioRequest(string Nombre, string HoraEntrada, string HoraSalida, int DiasLaborables,
    int ToleranciaMinutos, int MinutosParaHoraExtra, bool EsPredeterminado);
public record AsignarHorarioRequest(int? HorarioId, List<int> EmpleadoIds);
public record RegistrarDispositivoRequest(string? NumeroSerie, string? Nombre);

/// <summary>
/// Reloj de asistencia: importar sus marcaciones, asociar cada número a una
/// persona, compararlas con su horario y proponer horas extra al director.
/// </summary>
[Authorize]
[ApiController]
[Route("api/reloj")]
[RequirePermission(SystemPermission.OvertimeManage, SystemPermission.AbsencesManage)]
public class RelojAsistenciaController : ControllerBase
{
    private const long TamanoMaximo = 10 * 1024 * 1024;
    private static readonly string[] Extensiones = { ".dat", ".txt", ".csv", ".xlsx", ".xls" };

    private readonly RelojAsistenciaService _reloj;
    private readonly ApplicationDbContext _context;
    private readonly ITenantContext _tenant;
    private readonly IAuditLogService _audit;

    public RelojAsistenciaController(RelojAsistenciaService reloj, ApplicationDbContext context, ITenantContext tenant, IAuditLogService audit)
    {
        _reloj = reloj;
        _context = context;
        _tenant = tenant;
        _audit = audit;
    }

    private IActionResult? ArchivoInvalido(IFormFile? archivo)
    {
        if (archivo is null || archivo.Length == 0)
            return BadRequest(new { message = "Sube el archivo que descargas del reloj." });
        if (archivo.Length > TamanoMaximo)
            return BadRequest(new { message = "El archivo pasa de 10 MB." });
        if (!Extensiones.Contains(Path.GetExtension(archivo.FileName).ToLowerInvariant()))
            return BadRequest(new { message = "Sube el archivo del reloj (.dat o .txt) o el que exporta su programa (.csv o .xlsx)." });
        return null;
    }

    /// <summary>Lee el archivo y enseña qué trae, sin guardar. POST api/reloj/importar/previsualizar (multipart, campo "archivo")</summary>
    [HttpPost("importar/previsualizar")]
    [RequestSizeLimit(TamanoMaximo)]
    public async Task<IActionResult> Previsualizar(IFormFile? archivo)
    {
        if (ArchivoInvalido(archivo) is { } error) return error;
        await using var stream = archivo!.OpenReadStream();
        var lectura = RelojAsistenciaService.LeerArchivo(stream, archivo.FileName);
        return Ok(await _reloj.PrevisualizarAsync(lectura));
    }

    /// <summary>
    /// Importa las marcaciones (sin duplicar) y guarda las asociaciones hechas
    /// en la vista previa. Campo "asignaciones": JSON {"12": 345, ...}.
    /// </summary>
    [HttpPost("importar")]
    [RequestSizeLimit(TamanoMaximo)]
    public async Task<IActionResult> Importar(IFormFile? archivo, [FromForm] string? asignaciones)
    {
        if (ArchivoInvalido(archivo) is { } error) return error;

        Dictionary<string, int>? mapa = null;
        if (!string.IsNullOrWhiteSpace(asignaciones))
        {
            try { mapa = JsonSerializer.Deserialize<Dictionary<string, int>>(asignaciones); }
            catch (JsonException) { return BadRequest(new { message = "Las asociaciones no tienen el formato esperado." }); }
        }

        await using var stream = archivo!.OpenReadStream();
        var lectura = RelojAsistenciaService.LeerArchivo(stream, archivo.FileName);
        if (lectura.Marcas.Count == 0)
            return BadRequest(new { message = lectura.Problemas.FirstOrDefault()?.Mensaje ?? "El archivo no trae marcaciones." });

        var (resultado, fallo) = await _reloj.ImportarAsync(lectura, archivo.FileName, mapa);
        if (fallo != null) return BadRequest(new { message = fallo });

        await _audit.LogAsync("MarcacionesImportadas", "Marcacion", null, new Dictionary<string, string>
        {
            ["archivo"] = archivo.FileName,
            ["nuevas"] = resultado!.Nuevas.ToString(),
            ["repetidas"] = resultado.Repetidas.ToString(),
        });
        return Ok(resultado);
    }

    /// <summary>Personas activas con su número del reloj y su horario.</summary>
    [HttpGet("personas")]
    public async Task<IActionResult> Personas()
    {
        var tenantId = _tenant.TenantId;
        var personas = await _context.Empleados.AsNoTracking()
            .Where(e => e.TenantId == tenantId && e.EstaActivo)
            .OrderBy(e => e.Apellido).ThenBy(e => e.Nombre)
            .Select(e => new { e.Id, nombre = e.Nombre + " " + e.Apellido, e.CodigoReloj, horarioId = e.HorarioLaboralId })
            .ToListAsync();
        return Ok(personas);
    }

    /// <summary>Asocia o quita el número del reloj de una persona. PUT api/reloj/codigos</summary>
    [HttpPut("codigos")]
    public async Task<IActionResult> AsignarCodigo([FromBody] AsignarCodigoRequest request)
    {
        var error = await _reloj.AsignarCodigoAsync(request.Codigo, request.EmpleadoId);
        return error == null ? NoContent() : BadRequest(new { message = error });
    }

    /// <summary>Asistencia por persona y día contra su horario. GET api/reloj/resumen?desde=2026-10-01&amp;hasta=2026-10-31</summary>
    [HttpGet("resumen")]
    public async Task<IActionResult> Resumen([FromQuery] DateTime desde, [FromQuery] DateTime hasta)
    {
        if (hasta < desde) return BadRequest(new { message = "El final del rango tiene que ser después del inicio." });
        if ((hasta - desde).TotalDays > 62) return BadRequest(new { message = "Elige como máximo dos meses." });
        return Ok(await _reloj.ResumenAsync(desde, hasta));
    }

    /// <summary>Crea como pendientes del director las horas extra que muestra el reloj.</summary>
    [HttpPost("proponer-horas-extra")]
    [RequirePermission(SystemPermission.OvertimeManage)]
    public async Task<IActionResult> ProponerHorasExtra([FromBody] RangoFechasRequest request)
    {
        if (request.Hasta < request.Desde || (request.Hasta - request.Desde).TotalDays > 62)
            return BadRequest(new { message = "Elige un rango de como máximo dos meses." });
        var creadas = await _reloj.ProponerHorasExtraAsync(request.Desde, request.Hasta);
        await _audit.LogAsync("HorasExtraPropuestasPorReloj", "HoraExtra", null,
            new Dictionary<string, string> { ["creadas"] = creadas.ToString(), ["desde"] = request.Desde.ToString("yyyy-MM-dd"), ["hasta"] = request.Hasta.ToString("yyyy-MM-dd") });
        return Ok(new { creadas });
    }

    // ----------------------------------------------------------------
    // Relojes conectados por internet (ADMS)
    // ----------------------------------------------------------------

    /// <summary>Qué escribir en el reloj para que envíe las marcaciones solo.</summary>
    [HttpGet("conexion")]
    public IActionResult Conexion([FromServices] IConfiguration config)
    {
        var baseUrl = config["App:BaseUrl"];
        if (string.IsNullOrWhiteSpace(baseUrl) || !Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri))
            return Ok(new { configurado = false });
        return Ok(new { configurado = true, servidor = uri.Host, puerto = uri.Port, https = uri.Scheme == Uri.UriSchemeHttps });
    }

    [HttpGet("dispositivos")]
    public async Task<IActionResult> Dispositivos()
    {
        var tenantId = _tenant.TenantId;
        var lista = await _context.DispositivosReloj.AsNoTracking()
            .Where(d => d.TenantId == tenantId)
            .OrderByDescending(d => d.Activo).ThenBy(d => d.Nombre)
            .Select(d => new { d.Id, d.NumeroSerie, d.Nombre, d.Activo, d.UltimaConexion, d.UltimaMarcacion, d.MarcasRecibidas })
            .ToListAsync();
        return Ok(lista);
    }

    [HttpPost("dispositivos")]
    public async Task<IActionResult> RegistrarDispositivo([FromBody] RegistrarDispositivoRequest request)
    {
        var tenantId = _tenant.TenantId;
        var sn = request.NumeroSerie?.Trim().ToUpperInvariant() ?? "";
        if (!System.Text.RegularExpressions.Regex.IsMatch(sn, "^[A-Z0-9-]{5,50}$"))
            return BadRequest(new { message = "El número de serie lleva solo letras y números (está en «Información del sistema» del reloj)." });
        var nombre = string.IsNullOrWhiteSpace(request.Nombre) ? "Reloj" : request.Nombre.Trim();
        if (nombre.Length > 100) return BadRequest(new { message = "El nombre puede tener hasta 100 caracteres." });

        // El número de serie identifica al reloj en todo Pagly.
        if (await _context.DispositivosReloj.IgnoreQueryFilters().AnyAsync(d => d.NumeroSerie == sn))
            return BadRequest(new { message = "Ese reloj ya está registrado. Si aparece en tu lista desactivado, actívalo; si no, escríbenos." });

        var d = new DispositivoReloj { TenantId = tenantId, NumeroSerie = sn, Nombre = nombre, Activo = true, CreatedAt = DateTime.UtcNow };
        _context.DispositivosReloj.Add(d);
        await _context.SaveChangesAsync();
        await _audit.LogAsync("RelojRegistrado", "DispositivoReloj", d.Id.ToString(), new Dictionary<string, string> { ["numeroSerie"] = sn });
        return StatusCode(201, new { d.Id, d.NumeroSerie, d.Nombre, d.Activo });
    }

    /// <summary>Desactivar deja de aceptar sus envíos (no se borra; se puede volver a activar).</summary>
    [HttpPost("dispositivos/{id:int}/{accion:regex(^(activar|desactivar)$)}")]
    public async Task<IActionResult> CambiarDispositivo(int id, string accion)
    {
        var tenantId = _tenant.TenantId;
        var d = await _context.DispositivosReloj.FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId);
        if (d == null) return NotFound(new { message = "Ese reloj no existe." });
        d.Activo = accion == "activar";
        await _context.SaveChangesAsync();
        await _audit.LogAsync(d.Activo ? "RelojActivado" : "RelojDesactivado", "DispositivoReloj", id.ToString(),
            new Dictionary<string, string> { ["numeroSerie"] = d.NumeroSerie });
        return NoContent();
    }

    // ----------------------------------------------------------------
    // Horarios
    // ----------------------------------------------------------------

    [HttpGet("horarios")]
    public async Task<IActionResult> Horarios()
    {
        var tenantId = _tenant.TenantId;
        var horarios = await _context.HorariosLaborales.AsNoTracking()
            .Where(h => h.TenantId == tenantId && h.Activo)
            .OrderByDescending(h => h.EsPredeterminado).ThenBy(h => h.Nombre)
            .ToListAsync();
        var conteo = await _context.Empleados.AsNoTracking()
            .Where(e => e.TenantId == tenantId && e.EstaActivo && e.HorarioLaboralId != null)
            .GroupBy(e => e.HorarioLaboralId!.Value)
            .Select(g => new { g.Key, n = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.n);
        return Ok(horarios.Select(h => ADto(h, conteo.GetValueOrDefault(h.Id))));
    }

    [HttpPost("horarios")]
    public async Task<IActionResult> CrearHorario([FromBody] GuardarHorarioRequest request)
    {
        var tenantId = _tenant.TenantId;
        var (horario, error) = Validar(request, new HorarioLaboral { TenantId = tenantId, CreatedAt = DateTime.UtcNow });
        if (error != null) return BadRequest(new { message = error });

        var hayOtros = await _context.HorariosLaborales.AnyAsync(h => h.TenantId == tenantId && h.Activo);
        if (!hayOtros) horario!.EsPredeterminado = true; // el primero es el de todos
        if (horario!.EsPredeterminado) await QuitarPredeterminadoAsync(null);

        _context.HorariosLaborales.Add(horario);
        await _context.SaveChangesAsync();
        return StatusCode(201, ADto(horario, 0));
    }

    [HttpPut("horarios/{id:int}")]
    public async Task<IActionResult> ActualizarHorario(int id, [FromBody] GuardarHorarioRequest request)
    {
        var tenantId = _tenant.TenantId;
        var actual = await _context.HorariosLaborales.FirstOrDefaultAsync(h => h.Id == id && h.TenantId == tenantId && h.Activo);
        if (actual == null) return NotFound(new { message = "Ese horario no existe." });

        var eraPredeterminado = actual.EsPredeterminado;
        var (horario, error) = Validar(request, actual);
        if (error != null) return BadRequest(new { message = error });
        if (eraPredeterminado && !horario!.EsPredeterminado)
            return BadRequest(new { message = "Siempre tiene que haber un horario predeterminado: marca otro como predeterminado." });
        if (horario!.EsPredeterminado) await QuitarPredeterminadoAsync(id);

        horario.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return Ok(ADto(horario, 0));
    }

    /// <summary>Asigna un horario (o el predeterminado, con null) a varias personas.</summary>
    [HttpPut("horarios/asignar")]
    public async Task<IActionResult> AsignarHorario([FromBody] AsignarHorarioRequest request)
    {
        var tenantId = _tenant.TenantId;
        if (request.HorarioId.HasValue && !await _context.HorariosLaborales.AnyAsync(h => h.Id == request.HorarioId && h.TenantId == tenantId && h.Activo))
            return BadRequest(new { message = "Ese horario no existe." });
        var ids = request.EmpleadoIds?.Distinct().ToList() ?? new List<int>();
        var empleados = await _context.Empleados.Where(e => e.TenantId == tenantId && ids.Contains(e.Id)).ToListAsync();
        foreach (var e in empleados) e.HorarioLaboralId = request.HorarioId;
        await _context.SaveChangesAsync();
        return Ok(new { asignados = empleados.Count });
    }

    private async Task QuitarPredeterminadoAsync(int? exceptoId)
    {
        var tenantId = _tenant.TenantId;
        var otros = await _context.HorariosLaborales
            .Where(h => h.TenantId == tenantId && h.EsPredeterminado && (exceptoId == null || h.Id != exceptoId))
            .ToListAsync();
        foreach (var o in otros) o.EsPredeterminado = false;
    }

    private static (HorarioLaboral? Horario, string? Error) Validar(GuardarHorarioRequest r, HorarioLaboral h)
    {
        if (string.IsNullOrWhiteSpace(r.Nombre) || r.Nombre.Length > 100) return (null, "Ponle un nombre al horario (hasta 100 caracteres).");
        if (!TimeSpan.TryParse(r.HoraEntrada, out var entrada) || !TimeSpan.TryParse(r.HoraSalida, out var salida))
            return (null, "Las horas tienen que ser como 07:00 o 14:30.");
        if (salida <= entrada) return (null, "La salida tiene que ser después de la entrada.");
        if (r.DiasLaborables is <= 0 or > 127) return (null, "Elige al menos un día de trabajo.");
        if (r.ToleranciaMinutos is < 0 or > 120) return (null, "La tolerancia va de 0 a 120 minutos.");
        if (r.MinutosParaHoraExtra is < 1 or > 240) return (null, "Los minutos para hora extra van de 1 a 240.");

        h.Nombre = r.Nombre.Trim();
        h.HoraEntrada = entrada;
        h.HoraSalida = salida;
        h.DiasLaborables = r.DiasLaborables;
        h.ToleranciaMinutos = r.ToleranciaMinutos;
        h.MinutosParaHoraExtra = r.MinutosParaHoraExtra;
        h.EsPredeterminado = r.EsPredeterminado;
        return (h, null);
    }

    private static object ADto(HorarioLaboral h, int personas) => new
    {
        h.Id,
        h.Nombre,
        horaEntrada = h.HoraEntrada.ToString(@"hh\:mm"),
        horaSalida = h.HoraSalida.ToString(@"hh\:mm"),
        h.DiasLaborables,
        h.ToleranciaMinutos,
        h.MinutosParaHoraExtra,
        h.EsPredeterminado,
        personas,
    };
}
