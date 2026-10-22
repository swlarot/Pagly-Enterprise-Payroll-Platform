using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Vorluno.Planilla.Application.DTOs;
using Vorluno.Planilla.Application.Interfaces;
using Vorluno.Planilla.Domain.Entities;
using Vorluno.Planilla.Domain.Enums;
using Vorluno.Planilla.Infrastructure.Data;
using Vorluno.Planilla.Infrastructure.Services;
using Vorluno.Planilla.Web.Authorization;

namespace Vorluno.Planilla.Web.Controllers;

/// <summary>
/// Controlador para gestionar horas extra de empleados
/// </summary>
[Authorize]
[ApiController]
[Route("api/[controller]")]
public class HorasExtraController : ControllerBase
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ApplicationDbContext _context;
    private readonly ITenantContext _tenantContext;
    private readonly IOvertimeFactorService _overtimeFactorService;
    private readonly IAuditLogService _auditLog;
    private readonly RegistroHorasExtraService _registro;

    public HorasExtraController(
        IUnitOfWork unitOfWork,
        ApplicationDbContext context,
        ITenantContext tenantContext,
        IOvertimeFactorService overtimeFactorService,
        IAuditLogService auditLog,
        RegistroHorasExtraService registro)
    {
        _unitOfWork = unitOfWork;
        _context = context;
        _tenantContext = tenantContext;
        _overtimeFactorService = overtimeFactorService;
        _auditLog = auditLog;
        _registro = registro;
    }

    /// <summary>
    /// Obtiene lista de horas extra con filtros
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int? empleadoId = null,
        [FromQuery] DateTime? fecha = null,
        [FromQuery] TipoHoraExtra? tipo = null,
        [FromQuery] bool? aprobadas = null,
        [FromQuery] int? anio = null,
        [FromQuery] int? mes = null,
        [FromQuery] string? estado = null)
    {
        var tenantId = _tenantContext.TenantId;
        var query = _context.HorasExtra
            .Where(h => h.TenantId == tenantId)
            .Include(h => h.Empleado)
            .AsQueryable();

        if (empleadoId.HasValue)
            query = query.Where(h => h.EmpleadoId == empleadoId.Value);

        if (fecha.HasValue)
            query = query.Where(h => h.Fecha.Date == fecha.Value.Date);

        if (tipo.HasValue)
            query = query.Where(h => h.TipoHoraExtra == tipo.Value);

        if (aprobadas.HasValue)
            query = query.Where(h => h.EstaAprobada == aprobadas.Value);

        // Mes del día trabajado, igual que las planillas.
        if (anio.HasValue && mes is >= 1 and <= 12)
        {
            var desde = new DateTime(anio.Value, mes.Value, 1, 0, 0, 0, DateTimeKind.Utc);
            var hasta = desde.AddMonths(1);
            query = query.Where(h => h.Fecha >= desde && h.Fecha < hasta);
        }

        query = estado?.ToLowerInvariant() switch
        {
            "pendiente" => query.Where(h => !h.EstaAprobada && h.FechaRechazo == null),
            "aprobada" => query.Where(h => h.EstaAprobada && h.PlanillaDetailId == null),
            "rechazada" => query.Where(h => h.FechaRechazo != null),
            "pagada" => query.Where(h => h.PlanillaDetailId != null),
            _ => query,
        };

        var horasExtra = await query.AsNoTracking()
            .OrderByDescending(h => h.Fecha).ThenBy(h => h.HoraInicio)
            .ToListAsync();

        return Ok(await MapToDtosAsync(horasExtra));
    }

    /// <summary>
    /// Obtiene una hora extra por ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var tenantId = _tenantContext.TenantId;
        var horaExtra = await _context.HorasExtra
            .Where(h => h.Id == id && h.TenantId == tenantId)
            .Include(h => h.Empleado)
            .AsNoTracking()
            .FirstOrDefaultAsync();

        if (horaExtra == null)
            return NotFound();

        return Ok((await MapToDtosAsync(new[] { horaExtra }))[0]);
    }

    /// <summary>
    /// Obtiene horas extra de un empleado específico
    /// </summary>
    [HttpGet("empleado/{empleadoId}")]
    public async Task<IActionResult> GetByEmpleado(int empleadoId)
    {
        var tenantId = _tenantContext.TenantId;
        var horasExtra = await _context.HorasExtra
            .Where(h => h.EmpleadoId == empleadoId && h.TenantId == tenantId)
            .Include(h => h.Empleado)
            .AsNoTracking()
            .OrderByDescending(h => h.Fecha)
            .ToListAsync();

        return Ok(await MapToDtosAsync(horasExtra));
    }

    /// <summary>
    /// Obtiene horas extra pendientes de aprobación
    /// </summary>
    [HttpGet("pendientes")]
    public async Task<IActionResult> GetPendientes()
    {
        var tenantId = _tenantContext.TenantId;
        var pendientes = await _context.HorasExtra
            .Where(h => h.TenantId == tenantId && !h.EstaAprobada && h.FechaRechazo == null && h.PlanillaDetailId == null)
            .Include(h => h.Empleado)
            .AsNoTracking()
            .OrderBy(h => h.Fecha)
            .ToListAsync();

        return Ok(await MapToDtosAsync(pendientes));
    }

    /// <summary>
    /// Obtiene tipos de hora extra con factores
    /// </summary>
    [HttpGet("tipos")]
    public async Task<IActionResult> GetTipos()
    {
        var tipos = new List<object>();
        foreach (var t in Enum.GetValues<TipoHoraExtra>())
        {
            tipos.Add(new
            {
                Valor = (int)t,
                Nombre = ObtenerNombreTipo(t),
                Factor = await _overtimeFactorService.CalculateBaseFactorAsync(t)
            });
        }

        return Ok(tipos);
    }

    /// <summary>
    /// Crea una nueva hora extra
    /// </summary>
    [HttpPost]
    [RequirePermission(SystemPermission.OvertimeManage)]
    public async Task<IActionResult> Create(CreateHoraExtraRequest request)
    {
        var tenantId = _tenantContext.TenantId;

        // Verificar que el empleado existe
        var empleado = await _context.Empleados
            .FirstOrDefaultAsync(e => e.Id == request.EmpleadoId && e.TenantId == tenantId);
        if (empleado == null)
            return BadRequest(new { message = "El empleado especificado no existe." });

        // Horas, límites legales, factores y monto: las mismas cuentas que
        // usa el reloj de asistencia al proponer horas extra.
        var horaExtra = await _registro.ConstruirAsync(
            empleado, tenantId, request.Fecha, request.TipoHoraExtra, request.HoraInicio, request.HoraFin, request.Motivo);

        await _unitOfWork.Repository<HoraExtra>().AddAsync(horaExtra);
        await _unitOfWork.CompleteAsync();

        // Recargar con navegación
        horaExtra = await _context.HorasExtra
            .Include(h => h.Empleado)
            .FirstOrDefaultAsync(h => h.Id == horaExtra.Id);

        return CreatedAtAction(nameof(GetById), new { id = horaExtra!.Id }, (await MapToDtosAsync(new[] { horaExtra! }))[0]);
    }

    /// <summary>
    /// Crea múltiples horas extra (batch)
    /// </summary>
    [HttpPost("batch")]
    [RequirePermission(SystemPermission.OvertimeManage)]
    public async Task<IActionResult> CreateBatch([FromBody] List<CreateHoraExtraRequest> requests)
    {
        if (requests == null || requests.Count == 0)
            return BadRequest(new { message = "Debe proporcionar al menos una hora extra." });

        var tenantId = _tenantContext.TenantId;
        var horasExtra = new List<HoraExtra>();

        foreach (var request in requests)
        {
            var empleado = await _context.Empleados
                .FirstOrDefaultAsync(e => e.Id == request.EmpleadoId && e.TenantId == tenantId);
            if (empleado == null)
                continue;

            var horaExtra = await _registro.ConstruirAsync(
                empleado, tenantId, request.Fecha, request.TipoHoraExtra, request.HoraInicio, request.HoraFin, request.Motivo);

            horasExtra.Add(horaExtra);
        }

        foreach (var he in horasExtra)
        {
            await _unitOfWork.Repository<HoraExtra>().AddAsync(he);
        }

        await _unitOfWork.CompleteAsync();

        return Ok(new { message = $"{horasExtra.Count} horas extra creadas exitosamente.", count = horasExtra.Count });
    }

    /// <summary>
    /// Actualiza una hora extra
    /// </summary>
    [HttpPut("{id}")]
    [RequirePermission(SystemPermission.OvertimeManage)]
    public async Task<IActionResult> Update(int id, CreateHoraExtraRequest request)
    {
        var tenantId = _tenantContext.TenantId;
        var horaExtra = await _context.HorasExtra
            .FirstOrDefaultAsync(h => h.Id == id && h.TenantId == tenantId);

        if (horaExtra == null)
            return NotFound();

        if (horaExtra.EstaAprobada)
            return BadRequest(new { message = "No se puede modificar una hora extra ya aprobada." });

        if (horaExtra.PlanillaDetailId != null)
            return BadRequest(new { message = "No se puede modificar una hora extra ya pagada." });

        // Obtener empleado para calcular monto
        var empleado = await _context.Empleados
            .FirstOrDefaultAsync(e => e.Id == horaExtra.EmpleadoId && e.TenantId == tenantId);
        if (empleado == null)
            return BadRequest(new { message = "El empleado asociado no existe." });

        var cantidadHoras = CalcularHoras(request.HoraInicio, request.HoraFin);
        var tipo = request.TipoHoraExtra;

        // Validar límites legales (excluir la hora extra actual del cálculo)
        var horasExistentesDelDia = await _context.HorasExtra
            .Where(h => h.EmpleadoId == horaExtra.EmpleadoId 
                && h.Fecha.Date == request.Fecha.Date
                && h.Id != id // Excluir la hora extra que se está actualizando
                && h.FechaRechazo == null
                && h.PlanillaDetailId == null)
            .SumAsync(h => h.CantidadHoras);

        var horasExistentesDeLaSemana = await _context.HorasExtra
            .Where(h => h.EmpleadoId == horaExtra.EmpleadoId
                && h.Fecha >= request.Fecha.Date.AddDays(-((int)request.Fecha.DayOfWeek + 6) % 7)
                && h.Fecha <= request.Fecha.Date.AddDays(6 - ((int)request.Fecha.DayOfWeek + 6) % 7)
                && h.Id != id
                && h.FechaRechazo == null
                && h.PlanillaDetailId == null)
            .SumAsync(h => h.CantidadHoras);

        bool esExceso = (horasExistentesDelDia + cantidadHoras) > 3m || (horasExistentesDeLaSemana + cantidadHoras) > 9m;

        // Calcular factores
        var factorBase = await _overtimeFactorService.CalculateBaseFactorAsync(tipo);
        var factorTotal = await _overtimeFactorService.CalculateFactorAsync(tipo, esExceso);
        decimal? factorExceso = esExceso ? await _overtimeFactorService.GetFactorExcesoAsync() : null;

        // Calcular monto
        var montoCalculado = CalcularMonto(empleado, cantidadHoras, factorBase, factorExceso);

        horaExtra.Fecha = request.Fecha.Date;
        horaExtra.TipoHoraExtra = tipo;
        horaExtra.HoraInicio = request.HoraInicio;
        horaExtra.HoraFin = request.HoraFin;
        horaExtra.CantidadHoras = cantidadHoras;
        horaExtra.FactorMultiplicador = factorTotal;
        horaExtra.EsExceso = esExceso;
        horaExtra.FactorExceso = factorExceso;
        horaExtra.MontoCalculado = montoCalculado;
        horaExtra.Motivo = request.Motivo;
        horaExtra.UpdatedAt = DateTime.UtcNow;

        // Corregir una rechazada la vuelve a poner frente al director.
        horaExtra.FechaRechazo = null;
        horaExtra.RechazadoPor = null;
        horaExtra.MotivoRechazo = null;

        _unitOfWork.Repository<HoraExtra>().Update(horaExtra);
        await _unitOfWork.CompleteAsync();

        return NoContent();
    }

    /// <summary>
    /// Aprueba una hora extra. También sirve para cambiar de opinión sobre una
    /// rechazada, mientras no se haya pagado.
    /// </summary>
    [HttpPost("{id}/aprobar")]
    [RequirePermission(SystemPermission.OvertimeApprove)]
    public async Task<IActionResult> Aprobar(int id)
    {
        var r = await DecidirAsync(new[] { id }, "aprobar", null);
        return r ?? NoContent();
    }

    /// <summary>
    /// Rechaza una hora extra. No se borra: queda guardada con quién la
    /// rechazó, cuándo y por qué, y no entra a ninguna planilla.
    /// </summary>
    [HttpPost("{id}/rechazar")]
    [RequirePermission(SystemPermission.OvertimeApprove)]
    public async Task<IActionResult> Rechazar(int id, [FromBody] RechazarHoraExtraRequest? request = null)
    {
        var r = await DecidirAsync(new[] { id }, "rechazar", request?.Motivo);
        return r ?? NoContent();
    }

    /// <summary>
    /// Devuelve una hora extra a pendiente (deshace la aprobación o el rechazo).
    /// </summary>
    [HttpPost("{id}/pendiente")]
    [RequirePermission(SystemPermission.OvertimeApprove)]
    public async Task<IActionResult> DejarPendiente(int id)
    {
        var r = await DecidirAsync(new[] { id }, "pendiente", null);
        return r ?? NoContent();
    }

    /// <summary>
    /// Decisión del director sobre varias horas extra a la vez:
    /// aprobar, rechazar (con motivo) o dejar pendiente. Todo o nada.
    /// </summary>
    [HttpPost("decidir")]
    [RequirePermission(SystemPermission.OvertimeApprove)]
    public async Task<IActionResult> Decidir([FromBody] DecidirHorasExtraRequest request)
    {
        if (request?.Ids == null || request.Ids.Count == 0)
            return BadRequest(new { message = "Elige al menos una hora extra." });

        var r = await DecidirAsync(request.Ids.Distinct().ToList(), request.Decision, request.Motivo);
        return r ?? Ok(new { decididas = request.Ids.Distinct().Count() });
    }

    private async Task<IActionResult?> DecidirAsync(IReadOnlyCollection<int> ids, string? decision, string? motivo)
    {
        decision = decision?.Trim().ToLowerInvariant();
        if (decision is not ("aprobar" or "rechazar" or "pendiente"))
            return BadRequest(new { message = "La decisión debe ser aprobar, rechazar o pendiente." });

        var tenantId = _tenantContext.TenantId;
        var horas = await _context.HorasExtra
            .Where(h => h.TenantId == tenantId && ids.Contains(h.Id))
            .ToListAsync();

        if (horas.Count != ids.Count)
            return NotFound(new { message = "Alguna de las horas extra ya no existe." });

        if (horas.Any(h => h.PlanillaDetailId != null))
            return BadRequest(new { message = "Hay horas extra que ya se pagaron en una planilla; esas no se pueden cambiar." });

        var usuario = UsuarioActual();
        var ahora = DateTime.UtcNow;

        if (decision == "aprobar")
        {
            // El monto se recalcula al aprobar, por si cambió el salario.
            var empleadoIds = horas.Select(h => h.EmpleadoId).Distinct().ToList();
            var empleados = await _context.Empleados
                .Where(e => e.TenantId == tenantId && empleadoIds.Contains(e.Id))
                .ToDictionaryAsync(e => e.Id);

            foreach (var h in horas)
            {
                if (!empleados.TryGetValue(h.EmpleadoId, out var empleado))
                    return BadRequest(new { message = "El empleado asociado no existe." });

                var factorBase = await _overtimeFactorService.CalculateBaseFactorAsync(h.TipoHoraExtra);
                h.MontoCalculado = CalcularMonto(empleado, h.CantidadHoras, factorBase, h.FactorExceso);
                h.EstaAprobada = true;
                h.FechaAprobacion = ahora;
                h.AprobadoPor = usuario;
                h.FechaRechazo = null;
                h.RechazadoPor = null;
                h.MotivoRechazo = null;
                h.UpdatedAt = ahora;
            }
        }
        else if (decision == "rechazar")
        {
            var texto = string.IsNullOrWhiteSpace(motivo) ? null : motivo.Trim();
            if (texto?.Length > 500)
                return BadRequest(new { message = "El motivo no puede pasar de 500 caracteres." });

            foreach (var h in horas)
            {
                h.EstaAprobada = false;
                h.FechaAprobacion = null;
                h.AprobadoPor = null;
                h.FechaRechazo = ahora;
                h.RechazadoPor = usuario;
                h.MotivoRechazo = texto;
                h.UpdatedAt = ahora;
            }
        }
        else
        {
            foreach (var h in horas)
            {
                h.EstaAprobada = false;
                h.FechaAprobacion = null;
                h.AprobadoPor = null;
                h.FechaRechazo = null;
                h.RechazadoPor = null;
                h.MotivoRechazo = null;
                h.UpdatedAt = ahora;
            }
        }

        await _context.SaveChangesAsync();

        await _auditLog.LogAsync(
            decision switch { "aprobar" => "HorasExtraAprobadas", "rechazar" => "HorasExtraRechazadas", _ => "HorasExtraDevueltasAPendiente" },
            "HoraExtra",
            horas.Count == 1 ? horas[0].Id.ToString() : null,
            new Dictionary<string, string>
            {
                ["ids"] = string.Join(",", horas.Select(h => h.Id)),
                ["horas"] = horas.Sum(h => h.CantidadHoras).ToString("0.##"),
                ["motivo"] = motivo ?? string.Empty,
            });

        return null;
    }

    private string UsuarioActual() =>
        User.FindFirst("sub")?.Value
        ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
        ?? User.FindFirst("email")?.Value
        ?? "Sistema";

    /// <summary>
    /// Elimina una hora extra registrada por error. Solo mientras espera
    /// decisión: una aprobada o rechazada se conserva como constancia.
    /// </summary>
    [HttpDelete("{id}")]
    [RequirePermission(SystemPermission.OvertimeManage)]
    public async Task<IActionResult> Delete(int id)
    {
        var tenantId = _tenantContext.TenantId;
        var horaExtra = await _context.HorasExtra
            .FirstOrDefaultAsync(h => h.Id == id && h.TenantId == tenantId);

        if (horaExtra == null)
            return NotFound();

        if (horaExtra.PlanillaDetailId != null)
            return BadRequest(new { message = "No se puede eliminar una hora extra ya pagada." });

        if (!horaExtra.EstaPendiente)
            return BadRequest(new { message = "Esta hora extra ya tiene decisión del director; se conserva. Devuélvela a pendiente si fue un error." });

        _context.HorasExtra.Remove(horaExtra);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    // Métodos privados
    private static decimal CalcularHoras(TimeSpan inicio, TimeSpan fin) => RegistroHorasExtraService.CalcularHoras(inicio, fin);

    private static decimal CalcularMonto(Empleado empleado, decimal cantidadHoras, decimal factorMultiplicador, decimal? factorExceso = null) =>
        RegistroHorasExtraService.CalcularMonto(empleado, cantidadHoras, factorMultiplicador, factorExceso);

    private string ObtenerNombreTipo(TipoHoraExtra tipo)
    {
        return tipo switch
        {
            TipoHoraExtra.Diurna => "Diurna (1.25x)",
            TipoHoraExtra.Nocturna => "Nocturna (1.50x)",
            TipoHoraExtra.DomingoFeriado => "Domingo/Feriado (1.50x)",
            TipoHoraExtra.NocturnaDomingoFeriado => "Nocturna Dom/Fer (2.25x)",
            TipoHoraExtra.FiestaNacionalDiurna => "Fiesta Nacional Diurna (3.125x)",
            TipoHoraExtra.FiestaNacionalNocturna => "Fiesta Nacional Nocturna (3.75x)",
            TipoHoraExtra.MixtaDiurnaNocturna => "Mixta Diurna-Nocturna (1.50x)",
            TipoHoraExtra.MixtaNocturnaDiurna => "Mixta Nocturna-Diurna (1.75x)",
            TipoHoraExtra.DominicalHEDiurna => "Dominical HE Diurna (1.875x)",
            TipoHoraExtra.FeriadoOrdinario => "Feriado Ordinario (2.50x)",
            TipoHoraExtra.DiaSustituto => "Día Sustituto (1.50x)",
            _ => "Desconocido"
        };
    }

    private static string EstadoDe(HoraExtra h) =>
        h.PlanillaDetailId != null ? "Pagada"
        : h.EstaAprobada ? "Aprobada"
        : h.FechaRechazo != null ? "Rechazada"
        : "Pendiente";

    /// <summary>
    /// Convierte a DTO y cambia el id de quien decidió por su nombre: en pantalla
    /// se ve «Aprobado por Ana Pérez», no un identificador.
    /// </summary>
    private async Task<List<HoraExtraDto>> MapToDtosAsync(IReadOnlyList<HoraExtra> horas)
    {
        var ids = horas.SelectMany(h => new[] { h.AprobadoPor, h.RechazadoPor })
            .Where(x => !string.IsNullOrEmpty(x)).Select(x => x!).Distinct().ToList();
        var nombres = ids.Count == 0
            ? new Dictionary<string, string>()
            : await _context.Users.AsNoTracking()
                .Where(u => ids.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => !string.IsNullOrWhiteSpace(u.NombreCompleto) ? u.NombreCompleto! : u.Email ?? "");
        string? Nombre(string? id) => id == null ? null : nombres.TryGetValue(id, out var n) && n != "" ? n : id;

        return horas.Select(h => new HoraExtraDto(
            h.Id,
            h.EmpleadoId,
            $"{h.Empleado.Nombre} {h.Empleado.Apellido}",
            h.Fecha,
            h.TipoHoraExtra,
            ObtenerNombreTipo(h.TipoHoraExtra),
            h.HoraInicio,
            h.HoraFin,
            h.CantidadHoras,
            h.FactorMultiplicador,
            h.EsExceso,
            h.FactorExceso,
            h.MontoCalculado,
            h.EstaAprobada,
            Nombre(h.AprobadoPor),
            h.Motivo,
            h.Observaciones,
            h.PlanillaDetailId,
            EstadoDe(h),
            h.FechaAprobacion,
            h.FechaRechazo,
            Nombre(h.RechazadoPor),
            h.MotivoRechazo
        )).ToList();
    }

    /// <summary>
    /// Obtiene estadísticas agregadas de horas extra
    /// GET /api/horasextra/estadisticas?empleadoId=&fechaInicio=&fechaFin=
    /// </summary>
    [HttpGet("estadisticas")]
    [RequirePermission(SystemPermission.OvertimeManage, SystemPermission.OvertimeViewSelf)]
    public async Task<ActionResult<HorasExtraEstadisticasDto>> GetEstadisticas(
        [FromQuery] int? empleadoId = null,
        [FromQuery] DateTime? fechaInicio = null,
        [FromQuery] DateTime? fechaFin = null)
    {
        var tenantId = _tenantContext.TenantId;
        var query = _context.HorasExtra
            .Where(h => h.TenantId == tenantId && h.EstaAprobada);

        if (empleadoId.HasValue)
        {
            query = query.Where(h => h.EmpleadoId == empleadoId.Value);
        }

        if (fechaInicio.HasValue)
        {
            query = query.Where(h => h.Fecha >= fechaInicio.Value.Date);
        }

        if (fechaFin.HasValue)
        {
            query = query.Where(h => h.Fecha <= fechaFin.Value.Date);
        }

        var horasExtra = await query
            .Include(h => h.Empleado)
            .ToListAsync();

        if (!horasExtra.Any())
        {
            return Ok(new HorasExtraEstadisticasDto
            {
                TotalHoras = 0,
                MontoTotal = 0,
                PromedioHorasPorSemana = 0,
                PromedioHorasPorMes = 0,
                DiasFestivosTrabajados = new List<DateTime>(),
                TotalHorasConExceso = 0,
                PorcentajeHorasExceso = 0
            });
        }

        // Calcular totales por tipo
        decimal totalHorasDiurnas = horasExtra
            .Where(h => h.TipoHoraExtra == TipoHoraExtra.Diurna)
            .Sum(h => h.CantidadHoras);

        decimal totalHorasNocturnas = horasExtra
            .Where(h => h.TipoHoraExtra == TipoHoraExtra.Nocturna)
            .Sum(h => h.CantidadHoras);

        decimal totalHorasDomingoFeriado = horasExtra
            .Where(h => h.TipoHoraExtra == TipoHoraExtra.DomingoFeriado || h.TipoHoraExtra == TipoHoraExtra.NocturnaDomingoFeriado)
            .Sum(h => h.CantidadHoras);

        decimal totalHorasFestivos = horasExtra
            .Where(h => h.TipoHoraExtra == TipoHoraExtra.FiestaNacionalDiurna || h.TipoHoraExtra == TipoHoraExtra.FiestaNacionalNocturna)
            .Sum(h => h.CantidadHoras);

        decimal totalHorasMixtas = horasExtra
            .Where(h => h.TipoHoraExtra == TipoHoraExtra.MixtaDiurnaNocturna || h.TipoHoraExtra == TipoHoraExtra.MixtaNocturnaDiurna)
            .Sum(h => h.CantidadHoras);

        decimal totalHorasExceso = horasExtra
            .Where(h => h.EsExceso)
            .Sum(h => h.CantidadHoras);

        decimal totalHoras = horasExtra.Sum(h => h.CantidadHoras);

        // Calcular montos por tipo
        decimal montoDiurnas = horasExtra
            .Where(h => h.TipoHoraExtra == TipoHoraExtra.Diurna)
            .Sum(h => h.MontoCalculado ?? 0);

        decimal montoNocturnas = horasExtra
            .Where(h => h.TipoHoraExtra == TipoHoraExtra.Nocturna)
            .Sum(h => h.MontoCalculado ?? 0);

        decimal montoDomingoFeriado = horasExtra
            .Where(h => h.TipoHoraExtra == TipoHoraExtra.DomingoFeriado || h.TipoHoraExtra == TipoHoraExtra.NocturnaDomingoFeriado)
            .Sum(h => h.MontoCalculado ?? 0);

        decimal montoFestivos = horasExtra
            .Where(h => h.TipoHoraExtra == TipoHoraExtra.FiestaNacionalDiurna || h.TipoHoraExtra == TipoHoraExtra.FiestaNacionalNocturna)
            .Sum(h => h.MontoCalculado ?? 0);

        decimal montoMixtas = horasExtra
            .Where(h => h.TipoHoraExtra == TipoHoraExtra.MixtaDiurnaNocturna || h.TipoHoraExtra == TipoHoraExtra.MixtaNocturnaDiurna)
            .Sum(h => h.MontoCalculado ?? 0);

        decimal montoExceso = horasExtra
            .Where(h => h.EsExceso)
            .Sum(h => h.MontoCalculado ?? 0);

        decimal montoTotal = horasExtra.Sum(h => h.MontoCalculado ?? 0);

        // Calcular promedios
        var fechaMin = fechaInicio ?? horasExtra.Min(h => h.Fecha);
        var fechaMax = fechaFin ?? horasExtra.Max(h => h.Fecha);
        var diasTotales = (fechaMax - fechaMin).TotalDays + 1;
        var semanas = diasTotales / 7.0;
        var meses = diasTotales / 30.0;

        decimal promedioHorasPorSemana = semanas > 0 ? totalHoras / (decimal)semanas : 0;
        decimal promedioHorasPorMes = meses > 0 ? totalHoras / (decimal)meses : 0;

        // Días festivos trabajados
        var diasFestivosTrabajados = horasExtra
            .Where(h => h.TipoHoraExtra == TipoHoraExtra.FiestaNacionalDiurna || h.TipoHoraExtra == TipoHoraExtra.FiestaNacionalNocturna)
            .Select(h => h.Fecha.Date)
            .Distinct()
            .OrderBy(d => d)
            .ToList();

        decimal porcentajeHorasExceso = totalHoras > 0 ? (totalHorasExceso / totalHoras) * 100 : 0;

        // Comparación con período anterior (si aplica)
        ComparacionPeriodoDto? comparacion = null;
        if (fechaInicio.HasValue && fechaFin.HasValue)
        {
            var duracionPeriodo = (fechaFin.Value - fechaInicio.Value).TotalDays;
            var fechaInicioAnterior = fechaInicio.Value.AddDays(-duracionPeriodo - 1);
            var fechaFinAnterior = fechaInicio.Value.AddDays(-1);

            var horasAnteriores = await _context.HorasExtra
                .Where(h => h.TenantId == tenantId 
                    && h.EstaAprobada
                    && h.Fecha >= fechaInicioAnterior.Date
                    && h.Fecha <= fechaFinAnterior.Date
                    && (!empleadoId.HasValue || h.EmpleadoId == empleadoId.Value))
                .ToListAsync();

            var totalHorasAnterior = horasAnteriores.Sum(h => h.CantidadHoras);
            var montoTotalAnterior = horasAnteriores.Sum(h => h.MontoCalculado ?? 0);

            if (totalHorasAnterior > 0 || montoTotalAnterior > 0)
            {
                var diferenciaHoras = totalHoras - totalHorasAnterior;
                var diferenciaMonto = montoTotal - montoTotalAnterior;
                var porcentajeCambioHoras = totalHorasAnterior > 0 ? (diferenciaHoras / totalHorasAnterior) * 100 : 0;
                var porcentajeCambioMonto = montoTotalAnterior > 0 ? (diferenciaMonto / montoTotalAnterior) * 100 : 0;

                comparacion = new ComparacionPeriodoDto
                {
                    TotalHorasAnterior = totalHorasAnterior,
                    MontoTotalAnterior = montoTotalAnterior,
                    DiferenciaHoras = diferenciaHoras,
                    DiferenciaMonto = diferenciaMonto,
                    PorcentajeCambioHoras = porcentajeCambioHoras,
                    PorcentajeCambioMonto = porcentajeCambioMonto
                };
            }
        }

        var estadisticas = new HorasExtraEstadisticasDto
        {
            TotalHorasDiurnas = totalHorasDiurnas,
            TotalHorasNocturnas = totalHorasNocturnas,
            TotalHorasDomingoFeriado = totalHorasDomingoFeriado,
            TotalHorasFestivos = totalHorasFestivos,
            TotalHorasMixtas = totalHorasMixtas,
            TotalHorasExceso = totalHorasExceso,
            TotalHoras = totalHoras,
            MontoDiurnas = montoDiurnas,
            MontoNocturnas = montoNocturnas,
            MontoDomingoFeriado = montoDomingoFeriado,
            MontoFestivos = montoFestivos,
            MontoMixtas = montoMixtas,
            MontoExceso = montoExceso,
            MontoTotal = montoTotal,
            PromedioHorasPorSemana = promedioHorasPorSemana,
            PromedioHorasPorMes = promedioHorasPorMes,
            DiasFestivosTrabajados = diasFestivosTrabajados,
            TotalHorasConExceso = totalHorasExceso,
            PorcentajeHorasExceso = porcentajeHorasExceso,
            ComparacionPeriodoAnterior = comparacion
        };

        return Ok(estadisticas);
    }

    /// <summary>
    /// Valida límites legales de horas extra para un empleado en una fecha específica
    /// GET /api/horasextra/validar-limites?empleadoId=&fecha=&horasNuevas=
    /// </summary>
    [HttpGet("validar-limites")]
    [RequirePermission(SystemPermission.OvertimeManage)]
    public async Task<ActionResult> ValidarLimites(
        [FromQuery] int empleadoId,
        [FromQuery] DateTime fecha,
        [FromQuery] decimal horasNuevas = 0)
    {
        var tenantId = _tenantContext.TenantId;

        var (esExceso, mensaje) = await _overtimeFactorService.ValidateOvertimeLimits(
            empleadoId, fecha, horasNuevas);

        // Obtener horas extra del día y de la semana
        var inicioSemana = fecha.Date.AddDays(-(int)fecha.DayOfWeek);
        var finSemana = inicioSemana.AddDays(6);

        var horasDelDia = await _context.HorasExtra
            .Where(h => h.TenantId == tenantId
                && h.EmpleadoId == empleadoId
                && h.Fecha.Date == fecha.Date
                && h.EstaAprobada)
            .SumAsync(h => h.CantidadHoras);

        var horasDeLaSemana = await _context.HorasExtra
            .Where(h => h.TenantId == tenantId
                && h.EmpleadoId == empleadoId
                && h.Fecha >= inicioSemana
                && h.Fecha <= finSemana
                && h.EstaAprobada)
            .SumAsync(h => h.CantidadHoras);

        var horasDisponiblesDia = Math.Max(0, 3m - horasDelDia);
        var horasDisponiblesSemana = Math.Max(0, 9m - horasDeLaSemana);

        return Ok(new
        {
            esExceso,
            mensaje,
            horasDelDia,
            horasDeLaSemana,
            horasDisponiblesDia,
            horasDisponiblesSemana,
            limiteDiario = 3m,
            limiteSemanal = 9m,
            porcentajeDia = (horasDelDia / 3m) * 100,
            porcentajeSemana = (horasDeLaSemana / 9m) * 100
        });
    }

    /// <summary>
    /// Obtiene información sobre si una fecha es día festivo nacional
    /// GET /api/horasextra/es-festivo?fecha=
    /// </summary>
    [HttpGet("es-festivo")]
    [Authorize]
    public ActionResult EsFestivo([FromQuery] DateTime fecha)
    {
        var holidayService = HttpContext.RequestServices.GetRequiredService<PanamaHolidayService>();
        var esFestivo = holidayService.IsNationalHoliday(fecha);
        var nombreFestivo = esFestivo ? holidayService.GetHolidayName(fecha) : null;

        return Ok(new
        {
            esFestivo,
            nombreFestivo,
            fechaStr = fecha.ToString("yyyy-MM-dd")
        });
    }

    /// <summary>
    /// Sugiere el tipo de hora extra basado en fecha y horario
    /// GET /api/horasextra/sugerir-tipo?fecha=&horaInicio=&horaFin=
    /// </summary>
    [HttpGet("sugerir-tipo")]
    [Authorize]
    public async Task<ActionResult> SugerirTipo(
        [FromQuery] DateTime fecha,
        [FromQuery] string horaInicio,
        [FromQuery] string horaFin)
    {
        var holidayService = HttpContext.RequestServices.GetRequiredService<PanamaHolidayService>();
        var overtimeFactorService = HttpContext.RequestServices.GetRequiredService<IOvertimeFactorService>();

        if (!TimeSpan.TryParse(horaInicio, out var inicio) || !TimeSpan.TryParse(horaFin, out var fin))
        {
            return BadRequest(new { message = "Formato de hora inválido. Use formato HH:mm" });
        }

        var tipoSugerido = overtimeFactorService.DetermineOvertimeType(fecha, inicio, fin);
        var esFestivo = holidayService.IsNationalHoliday(fecha);
        var nombreFestivo = esFestivo ? holidayService.GetHolidayName(fecha) : null;
        var esDomingo = fecha.DayOfWeek == DayOfWeek.Sunday;

        return Ok(new
        {
            tipoSugerido = (int)tipoSugerido,
            nombreTipo = ObtenerNombreTipo(tipoSugerido),
            esFestivo,
            nombreFestivo,
            esDomingo,
            factor = await overtimeFactorService.CalculateBaseFactorAsync(tipoSugerido)
        });
    }
}
