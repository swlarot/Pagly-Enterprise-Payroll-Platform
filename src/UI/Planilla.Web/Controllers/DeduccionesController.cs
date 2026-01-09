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
/// Controlador de API para gestionar deducciones fijas de empleados.
/// </summary>
[Authorize]
[ApiController]
[Route("api/[controller]")]
public class DeduccionesController : ControllerBase
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ApplicationDbContext _context;
    private readonly ITenantContext _tenantContext;

    public DeduccionesController(IUnitOfWork unitOfWork, ApplicationDbContext context, ITenantContext tenantContext)
    {
        _unitOfWork = unitOfWork;
        _context = context;
        _tenantContext = tenantContext;
    }

    /// <summary>
    /// Obtiene una lista de deducciones con filtros opcionales.
    /// </summary>
    /// <param name="empleadoId">Filtrar por empleado (opcional).</param>
    /// <param name="tipo">Filtrar por tipo de deducción (opcional).</param>
    /// <param name="activas">Filtrar solo deducciones activas (opcional).</param>
    /// <returns>Lista de deducciones.</returns>
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int? empleadoId,
        [FromQuery] TipoDeduccion? tipo,
        [FromQuery] bool? activas)
    {
        var tenantId = _tenantContext.TenantId;
        var query = _context.DeduccionesFijas
            .Where(d => d.TenantId == tenantId)
            .Include(d => d.Empleado)
            .AsQueryable();

        if (empleadoId.HasValue)
        {
            query = query.Where(d => d.EmpleadoId == empleadoId.Value);
        }

        if (tipo.HasValue)
        {
            query = query.Where(d => d.TipoDeduccion == tipo.Value);
        }

        if (activas.HasValue && activas.Value)
        {
            query = query.Where(d => d.EstaActivo);
        }

        var deducciones = await query
            .AsNoTracking()
            .OrderBy(d => d.Prioridad)
            .ThenByDescending(d => d.CreatedAt)
            .ToListAsync();

        var deduccionesDto = deducciones.Select(MapToDto);
        return Ok(deduccionesDto);
    }

    /// <summary>
    /// Obtiene una deducción específica por su ID.
    /// </summary>
    /// <param name="id">ID de la deducción.</param>
    /// <returns>Detalles de la deducción.</returns>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var tenantId = _tenantContext.TenantId;
        var deduccion = await _context.DeduccionesFijas
            .Where(d => d.Id == id && d.TenantId == tenantId)
            .Include(d => d.Empleado)
            .AsNoTracking()
            .FirstOrDefaultAsync();

        if (deduccion == null)
        {
            return NotFound();
        }

        var deduccionDto = MapToDto(deduccion);
        return Ok(deduccionDto);
    }

    /// <summary>
    /// Obtiene todas las deducciones de un empleado específico.
    /// </summary>
    /// <param name="empleadoId">ID del empleado.</param>
    /// <returns>Lista de deducciones del empleado.</returns>
    [HttpGet("empleado/{empleadoId}")]
    public async Task<IActionResult> GetByEmpleado(int empleadoId)
    {
        var tenantId = _tenantContext.TenantId;
        var empleado = await _context.Empleados
            .FirstOrDefaultAsync(e => e.Id == empleadoId && e.TenantId == tenantId);
        if (empleado == null)
        {
            return NotFound(new { message = "Empleado no encontrado" });
        }

        var deducciones = await _context.DeduccionesFijas
            .Where(d => d.EmpleadoId == empleadoId && d.TenantId == tenantId)
            .Include(d => d.Empleado)
            .AsNoTracking()
            .OrderBy(d => d.Prioridad)
            .ToListAsync();

        var deduccionesDto = deducciones.Select(MapToDto);
        return Ok(deduccionesDto);
    }

    /// <summary>
    /// Obtiene la lista de tipos de deducción con sus nombres.
    /// </summary>
    /// <returns>Lista de tipos de deducción.</returns>
    [HttpGet("tipos")]
    public IActionResult GetTipos()
    {
        var tipos = Enum.GetValues<TipoDeduccion>()
            .Select(t => new
            {
                id = (int)t,
                nombre = GetTipoDeduccionNombre(t),
                valor = t.ToString()
            });

        return Ok(tipos);
    }

    /// <summary>
    /// Crea una nueva deducción.
    /// </summary>
    /// <param name="request">Datos de la deducción a crear.</param>
    /// <returns>Deducción creada.</returns>
    [HttpPost]
    [Authorize(Roles = "Owner,Admin,Manager")]
    public async Task<IActionResult> Create(CreateDeduccionRequest request)
    {
        var tenantId = _tenantContext.TenantId;

        // Validar empleado existe
        var empleado = await _context.Empleados
            .FirstOrDefaultAsync(e => e.Id == request.EmpleadoId && e.TenantId == tenantId);
        if (empleado == null)
        {
            return BadRequest(new { message = "Empleado no encontrado" });
        }

        // Validaciones de negocio
        if (request.EsPorcentaje)
        {
            if (!request.Porcentaje.HasValue || request.Porcentaje.Value <= 0)
            {
                return BadRequest(new { message = "El porcentaje debe ser mayor a cero cuando es deducción por porcentaje" });
            }
            if (request.Porcentaje.Value > 100)
            {
                return BadRequest(new { message = "El porcentaje no puede ser mayor a 100" });
            }
        }
        else
        {
            if (request.Monto <= 0)
            {
                return BadRequest(new { message = "El monto debe ser mayor a cero cuando es deducción fija" });
            }
        }

        if (request.FechaFin.HasValue && request.FechaFin.Value < request.FechaInicio)
        {
            return BadRequest(new { message = "La fecha fin no puede ser anterior a la fecha inicio" });
        }

        var deduccion = new DeduccionFija
        {
            TenantId = tenantId,
            EmpleadoId = request.EmpleadoId,
            TipoDeduccion = request.TipoDeduccion,
            Descripcion = request.Descripcion,
            Monto = request.Monto,
            Porcentaje = request.Porcentaje,
            EsPorcentaje = request.EsPorcentaje,
            FechaInicio = request.FechaInicio,
            FechaFin = request.FechaFin,
            EstaActivo = true,
            Referencia = request.Referencia,
            Prioridad = request.Prioridad,
            Observaciones = request.Observaciones,
            CreatedAt = DateTime.UtcNow
        };

        var repository = _unitOfWork.Repository<DeduccionFija>();
        await repository.AddAsync(deduccion);
        await _unitOfWork.CompleteAsync();

        // Recargar con navegación
        deduccion = await _context.DeduccionesFijas
            .Include(d => d.Empleado)
            .FirstOrDefaultAsync(d => d.Id == deduccion.Id);

        var deduccionDto = MapToDto(deduccion!);
        return CreatedAtAction(nameof(GetById), new { id = deduccion!.Id }, deduccionDto);
    }

    /// <summary>
    /// Actualiza una deducción existente.
    /// </summary>
    /// <param name="id">ID de la deducción.</param>
    /// <param name="request">Datos actualizados.</param>
    /// <returns>NoContent si fue exitoso.</returns>
    [HttpPut("{id}")]
    [Authorize(Roles = "Owner,Admin,Manager")]
    public async Task<IActionResult> Update(int id, CreateDeduccionRequest request)
    {
        var tenantId = _tenantContext.TenantId;
        var deduccion = await _context.DeduccionesFijas
            .FirstOrDefaultAsync(d => d.Id == id && d.TenantId == tenantId);

        if (deduccion == null)
        {
            return NotFound();
        }

        // Validaciones
        if (request.EsPorcentaje)
        {
            if (!request.Porcentaje.HasValue || request.Porcentaje.Value <= 0)
            {
                return BadRequest(new { message = "El porcentaje debe ser mayor a cero cuando es deducción por porcentaje" });
            }
            if (request.Porcentaje.Value > 100)
            {
                return BadRequest(new { message = "El porcentaje no puede ser mayor a 100" });
            }
        }
        else
        {
            if (request.Monto <= 0)
            {
                return BadRequest(new { message = "El monto debe ser mayor a cero cuando es deducción fija" });
            }
        }

        if (request.FechaFin.HasValue && request.FechaFin.Value < request.FechaInicio)
        {
            return BadRequest(new { message = "La fecha fin no puede ser anterior a la fecha inicio" });
        }

        deduccion.TipoDeduccion = request.TipoDeduccion;
        deduccion.Descripcion = request.Descripcion;
        deduccion.Monto = request.Monto;
        deduccion.Porcentaje = request.Porcentaje;
        deduccion.EsPorcentaje = request.EsPorcentaje;
        deduccion.FechaInicio = request.FechaInicio;
        deduccion.FechaFin = request.FechaFin;
        deduccion.Referencia = request.Referencia;
        deduccion.Prioridad = request.Prioridad;
        deduccion.Observaciones = request.Observaciones;
        deduccion.UpdatedAt = DateTime.UtcNow;

        await _unitOfWork.CompleteAsync();
        return NoContent();
    }

    /// <summary>
    /// Desactiva una deducción (soft delete).
    /// </summary>
    /// <param name="id">ID de la deducción.</param>
    /// <returns>NoContent si fue exitoso.</returns>
    [HttpDelete("{id}")]
    [Authorize(Roles = "Owner,Admin")]
    public async Task<IActionResult> Desactivar(int id)
    {
        var tenantId = _tenantContext.TenantId;
        var deduccion = await _context.DeduccionesFijas
            .FirstOrDefaultAsync(d => d.Id == id && d.TenantId == tenantId);

        if (deduccion == null)
        {
            return NotFound();
        }

        deduccion.EstaActivo = false;
        deduccion.UpdatedAt = DateTime.UtcNow;

        await _unitOfWork.CompleteAsync();
        return NoContent();
    }

    /// <summary>
    /// Mapea una entidad DeduccionFija a DeduccionFijaDto.
    /// </summary>
    private static DeduccionFijaDto MapToDto(DeduccionFija deduccion)
    {
        var nombreCompleto = deduccion.Empleado != null
            ? $"{deduccion.Empleado.Nombre} {deduccion.Empleado.Apellido}".Trim()
            : string.Empty;

        return new DeduccionFijaDto(
            Id: deduccion.Id,
            EmpleadoId: deduccion.EmpleadoId,
            EmpleadoNombre: nombreCompleto,
            TipoDeduccion: deduccion.TipoDeduccion,
            TipoDeduccionNombre: GetTipoDeduccionNombre(deduccion.TipoDeduccion),
            Descripcion: deduccion.Descripcion,
            Monto: deduccion.Monto,
            Porcentaje: deduccion.Porcentaje,
            EsPorcentaje: deduccion.EsPorcentaje,
            FechaInicio: deduccion.FechaInicio,
            FechaFin: deduccion.FechaFin,
            EstaActivo: deduccion.EstaActivo,
            Referencia: deduccion.Referencia,
            Prioridad: deduccion.Prioridad
        );
    }

    /// <summary>
    /// Obtiene el nombre legible del tipo de deducción.
    /// </summary>
    private static string GetTipoDeduccionNombre(TipoDeduccion tipo)
    {
        return tipo switch
        {
            TipoDeduccion.PrestamoInterno => "Préstamo Interno",
            TipoDeduccion.PrestamoBancario => "Préstamo Bancario",
            TipoDeduccion.PensionAlimenticia => "Pensión Alimenticia",
            TipoDeduccion.Embargo => "Embargo",
            TipoDeduccion.SeguroMedico => "Seguro Médico",
            TipoDeduccion.AhorroVoluntario => "Ahorro Voluntario",
            TipoDeduccion.Sindicato => "Sindicato",
            TipoDeduccion.Otro => "Otro",
            _ => tipo.ToString()
        };
    }
}
