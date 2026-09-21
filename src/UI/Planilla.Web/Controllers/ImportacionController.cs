using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vorluno.Planilla.Application.DTOs.Importacion;
using Vorluno.Planilla.Application.Helpers;
using Vorluno.Planilla.Domain.Enums;
using Vorluno.Planilla.Infrastructure.Services;
using Vorluno.Planilla.Web.Authorization;

namespace Vorluno.Planilla.Web.Controllers;

/// <summary>
/// Onboarding de una empresa: plantilla, validación y confirmación de la
/// importación masiva de empleados con su historial de salarios y saldos.
/// </summary>
[ApiController]
[Route("api/importacion")]
[Authorize]
public class ImportacionController : ControllerBase
{
    private const long TamanoMaximo = 10 * 1024 * 1024; // 10 MB: 2,000 empleados x 60 meses caben de sobra

    private readonly IImportacionEmpleadosService _importacion;

    public ImportacionController(IImportacionEmpleadosService importacion)
    {
        _importacion = importacion;
    }

    // ====================================================================
    // GET /api/importacion/plantilla
    // ====================================================================
    [HttpGet("plantilla")]
    [RequirePermission(SystemPermission.EmployeesCreate)]
    public IActionResult DescargarPlantilla()
    {
        var hoy = DateTimeHelper.NowPanama();
        var bytes = PlantillaImportacion.Generar(hoy);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"Plantilla empleados Pagly {hoy:yyyy-MM}.xlsx");
    }

    // ====================================================================
    // POST /api/importacion/empleados/validar   (multipart, campo "archivo")
    // No escribe nada: devuelve cada fila con sus problemas para revisarla.
    // ====================================================================
    [HttpPost("empleados/validar")]
    [RequirePermission(SystemPermission.EmployeesCreate)]
    [RequestSizeLimit(TamanoMaximo)]
    public async Task<ActionResult<ResultadoValidacionDto>> Validar(IFormFile? archivo, CancellationToken ct)
    {
        if (archivo is null || archivo.Length == 0)
            return BadRequest(new { message = "Sube la plantilla llena (.xlsx)." });
        if (archivo.Length > TamanoMaximo)
            return BadRequest(new { message = "El archivo pasa de 10 MB." });
        if (!archivo.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { message = "El archivo debe ser un Excel .xlsx (el .xls antiguo no sirve; guárdalo como .xlsx)." });

        await using var stream = archivo.OpenReadStream();
        var resultado = await _importacion.ValidarAsync(stream, DateTimeHelper.NowPanama(), ct);
        return Ok(resultado);
    }

    // ====================================================================
    // POST /api/importacion/empleados/revalidar
    // Vuelve a validar filas ya corregidas en pantalla (sin archivo).
    // ====================================================================
    [HttpPost("empleados/revalidar")]
    [RequirePermission(SystemPermission.EmployeesCreate)]
    public async Task<ActionResult<ResultadoValidacionDto>> Revalidar([FromBody] List<FilaImportacionDto> filas, CancellationToken ct)
    {
        if (filas.Count > 5000) return BadRequest(new { message = "Demasiadas filas." });
        return Ok(await _importacion.RevalidarAsync(filas, DateTimeHelper.NowPanama(), ct));
    }

    // ====================================================================
    // POST /api/importacion/empleados/confirmar
    // ====================================================================
    [HttpPost("empleados/confirmar")]
    [RequirePermission(SystemPermission.EmployeesCreate)]
    public async Task<ActionResult<ResumenImportacionDto>> Confirmar([FromBody] ConfirmarImportacionRequest request, CancellationToken ct)
    {
        if (request.Filas.Count == 0)
            return BadRequest(new { message = "No hay empleados para importar." });

        try
        {
            var resumen = await _importacion.ConfirmarAsync(request, DateTimeHelper.NowPanama(), ct);
            return Ok(resumen);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
