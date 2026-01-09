using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Vorluno.Planilla.Application.DTOs;
using Vorluno.Planilla.Application.Interfaces;
using Vorluno.Planilla.Domain.Entities;
using Vorluno.Planilla.Domain.Enums;
using Vorluno.Planilla.Infrastructure.Data;

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

    public HorasExtraController(IUnitOfWork unitOfWork, ApplicationDbContext context, ITenantContext tenantContext)
    {
        _unitOfWork = unitOfWork;
        _context = context;
        _tenantContext = tenantContext;
    }

    /// <summary>
    /// Obtiene lista de horas extra con filtros
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int? empleadoId = null,
        [FromQuery] DateTime? fecha = null,
        [FromQuery] TipoHoraExtra? tipo = null,
        [FromQuery] bool? aprobadas = null)
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

        var horasExtra = await query.AsNoTracking().OrderByDescending(h => h.Fecha).ToListAsync();

        var dtos = horasExtra.Select(MapToDto);
        return Ok(dtos);
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

        return Ok(MapToDto(horaExtra));
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

        var dtos = horasExtra.Select(MapToDto);
        return Ok(dtos);
    }

    /// <summary>
    /// Obtiene horas extra pendientes de aprobación
    /// </summary>
    [HttpGet("pendientes")]
    public async Task<IActionResult> GetPendientes()
    {
        var tenantId = _tenantContext.TenantId;
        var pendientes = await _context.HorasExtra
            .Where(h => h.TenantId == tenantId && !h.EstaAprobada && h.PlanillaDetailId == null)
            .Include(h => h.Empleado)
            .AsNoTracking()
            .OrderBy(h => h.Fecha)
            .ToListAsync();

        var dtos = pendientes.Select(MapToDto);
        return Ok(dtos);
    }

    /// <summary>
    /// Obtiene tipos de hora extra con factores
    /// </summary>
    [HttpGet("tipos")]
    public IActionResult GetTipos()
    {
        var tipos = new[]
        {
            new { Valor = (int)TipoHoraExtra.Diurna, Nombre = "Diurna (1.25x)", Factor = 1.25m },
            new { Valor = (int)TipoHoraExtra.Nocturna, Nombre = "Nocturna (1.50x)", Factor = 1.50m },
            new { Valor = (int)TipoHoraExtra.DomingoFeriado, Nombre = "Domingo/Feriado (1.50x)", Factor = 1.50m },
            new { Valor = (int)TipoHoraExtra.NocturnaDomingoFeriado, Nombre = "Nocturna Dom/Fer (1.75x)", Factor = 1.75m }
        };

        return Ok(tipos);
    }

    /// <summary>
    /// Crea una nueva hora extra
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "Owner,Admin,Manager")]
    public async Task<IActionResult> Create(CreateHoraExtraRequest request)
    {
        var tenantId = _tenantContext.TenantId;

        // Verificar que el empleado existe
        var empleado = await _context.Empleados
            .FirstOrDefaultAsync(e => e.Id == request.EmpleadoId && e.TenantId == tenantId);
        if (empleado == null)
            return BadRequest(new { message = "El empleado especificado no existe." });

        // Calcular horas y factor
        var cantidadHoras = CalcularHoras(request.HoraInicio, request.HoraFin);
        var factor = ObtenerFactor(request.TipoHoraExtra);

        var horaExtra = new HoraExtra
        {
            TenantId = tenantId,
            EmpleadoId = request.EmpleadoId,
            Fecha = request.Fecha.Date,
            TipoHoraExtra = request.TipoHoraExtra,
            HoraInicio = request.HoraInicio,
            HoraFin = request.HoraFin,
            CantidadHoras = cantidadHoras,
            FactorMultiplicador = factor,
            Motivo = request.Motivo,
            EstaAprobada = false,
            CreatedAt = DateTime.UtcNow
        };

        await _unitOfWork.Repository<HoraExtra>().AddAsync(horaExtra);
        await _unitOfWork.CompleteAsync();

        // Recargar con navegación
        horaExtra = await _context.HorasExtra
            .Include(h => h.Empleado)
            .FirstOrDefaultAsync(h => h.Id == horaExtra.Id);

        return CreatedAtAction(nameof(GetById), new { id = horaExtra!.Id }, MapToDto(horaExtra));
    }

    /// <summary>
    /// Crea múltiples horas extra (batch)
    /// </summary>
    [HttpPost("batch")]
    [Authorize(Roles = "Owner,Admin,Manager")]
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

            var cantidadHoras = CalcularHoras(request.HoraInicio, request.HoraFin);
            var factor = ObtenerFactor(request.TipoHoraExtra);

            var horaExtra = new HoraExtra
            {
                TenantId = tenantId,
                EmpleadoId = request.EmpleadoId,
                Fecha = request.Fecha.Date,
                TipoHoraExtra = request.TipoHoraExtra,
                HoraInicio = request.HoraInicio,
                HoraFin = request.HoraFin,
                CantidadHoras = cantidadHoras,
                FactorMultiplicador = factor,
                Motivo = request.Motivo,
                EstaAprobada = false,
                CreatedAt = DateTime.UtcNow
            };

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
    [Authorize(Roles = "Owner,Admin,Manager")]
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

        var cantidadHoras = CalcularHoras(request.HoraInicio, request.HoraFin);
        var factor = ObtenerFactor(request.TipoHoraExtra);

        horaExtra.Fecha = request.Fecha.Date;
        horaExtra.TipoHoraExtra = request.TipoHoraExtra;
        horaExtra.HoraInicio = request.HoraInicio;
        horaExtra.HoraFin = request.HoraFin;
        horaExtra.CantidadHoras = cantidadHoras;
        horaExtra.FactorMultiplicador = factor;
        horaExtra.Motivo = request.Motivo;
        horaExtra.UpdatedAt = DateTime.UtcNow;

        _unitOfWork.Repository<HoraExtra>().Update(horaExtra);
        await _unitOfWork.CompleteAsync();

        return NoContent();
    }

    /// <summary>
    /// Aprueba una hora extra
    /// </summary>
    [HttpPost("{id}/aprobar")]
    [Authorize(Roles = "Owner,Admin,Manager")]
    public async Task<IActionResult> Aprobar(int id)
    {
        var tenantId = _tenantContext.TenantId;
        var horaExtra = await _context.HorasExtra
            .FirstOrDefaultAsync(h => h.Id == id && h.TenantId == tenantId);

        if (horaExtra == null)
            return NotFound();

        if (horaExtra.EstaAprobada)
            return BadRequest(new { message = "La hora extra ya está aprobada." });

        horaExtra.EstaAprobada = true;
        horaExtra.FechaAprobacion = DateTime.UtcNow;
        horaExtra.AprobadoPor = "Sistema"; // TODO: Obtener usuario actual
        horaExtra.UpdatedAt = DateTime.UtcNow;

        _unitOfWork.Repository<HoraExtra>().Update(horaExtra);
        await _unitOfWork.CompleteAsync();

        return NoContent();
    }

    /// <summary>
    /// Rechaza una hora extra
    /// </summary>
    [HttpPost("{id}/rechazar")]
    [Authorize(Roles = "Owner,Admin,Manager")]
    public async Task<IActionResult> Rechazar(int id)
    {
        var tenantId = _tenantContext.TenantId;
        var horaExtra = await _context.HorasExtra
            .FirstOrDefaultAsync(h => h.Id == id && h.TenantId == tenantId);

        if (horaExtra == null)
            return NotFound();

        _context.HorasExtra.Remove(horaExtra);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    /// <summary>
    /// Elimina una hora extra
    /// </summary>
    [HttpDelete("{id}")]
    [Authorize(Roles = "Owner,Admin")]
    public async Task<IActionResult> Delete(int id)
    {
        var tenantId = _tenantContext.TenantId;
        var horaExtra = await _context.HorasExtra
            .FirstOrDefaultAsync(h => h.Id == id && h.TenantId == tenantId);

        if (horaExtra == null)
            return NotFound();

        if (horaExtra.PlanillaDetailId != null)
            return BadRequest(new { message = "No se puede eliminar una hora extra ya pagada." });

        _context.HorasExtra.Remove(horaExtra);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    // Métodos privados
    private decimal CalcularHoras(TimeSpan inicio, TimeSpan fin)
    {
        var diferencia = fin - inicio;
        if (diferencia.TotalHours < 0)
            diferencia = diferencia.Add(TimeSpan.FromHours(24));

        return (decimal)diferencia.TotalHours;
    }

    private decimal ObtenerFactor(TipoHoraExtra tipo)
    {
        return tipo switch
        {
            TipoHoraExtra.Diurna => 1.25m,
            TipoHoraExtra.Nocturna => 1.50m,
            TipoHoraExtra.DomingoFeriado => 1.50m,
            TipoHoraExtra.NocturnaDomingoFeriado => 1.75m,
            _ => 1.25m
        };
    }

    private string ObtenerNombreTipo(TipoHoraExtra tipo)
    {
        return tipo switch
        {
            TipoHoraExtra.Diurna => "Diurna (1.25x)",
            TipoHoraExtra.Nocturna => "Nocturna (1.50x)",
            TipoHoraExtra.DomingoFeriado => "Domingo/Feriado (1.50x)",
            TipoHoraExtra.NocturnaDomingoFeriado => "Nocturna Dom/Fer (1.75x)",
            _ => "Desconocido"
        };
    }

    private HoraExtraDto MapToDto(HoraExtra horaExtra)
    {
        return new HoraExtraDto(
            horaExtra.Id,
            horaExtra.EmpleadoId,
            $"{horaExtra.Empleado.Nombre} {horaExtra.Empleado.Apellido}",
            horaExtra.Fecha,
            horaExtra.TipoHoraExtra,
            ObtenerNombreTipo(horaExtra.TipoHoraExtra),
            horaExtra.HoraInicio,
            horaExtra.HoraFin,
            horaExtra.CantidadHoras,
            horaExtra.FactorMultiplicador,
            horaExtra.MontoCalculado,
            horaExtra.EstaAprobada,
            horaExtra.AprobadoPor,
            horaExtra.Motivo
        );
    }
}
