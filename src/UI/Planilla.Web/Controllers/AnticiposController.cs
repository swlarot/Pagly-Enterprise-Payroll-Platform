using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Vorluno.Planilla.Application.DTOs;
using Vorluno.Planilla.Application.Interfaces;
using Vorluno.Planilla.Domain.Entities;
using Vorluno.Planilla.Infrastructure.Data;

namespace Vorluno.Planilla.Web.Controllers;

/// <summary>
/// Controlador de API para gestionar anticipos de salario.
/// </summary>
[Authorize]
[ApiController]
[Route("api/[controller]")]
public class AnticiposController : ControllerBase
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ApplicationDbContext _context;
    private readonly ITenantContext _tenantContext;

    public AnticiposController(IUnitOfWork unitOfWork, ApplicationDbContext context, ITenantContext tenantContext)
    {
        _unitOfWork = unitOfWork;
        _context = context;
        _tenantContext = tenantContext;
    }

    /// <summary>
    /// Obtiene una lista de anticipos con filtros opcionales.
    /// </summary>
    /// <param name="empleadoId">Filtrar por empleado (opcional).</param>
    /// <param name="estado">Filtrar por estado (opcional).</param>
    /// <returns>Lista de anticipos.</returns>
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] int? empleadoId, [FromQuery] EstadoAnticipo? estado)
    {
        var tenantId = _tenantContext.TenantId;
        var query = _context.Anticipos
            .Where(a => a.TenantId == tenantId)
            .Include(a => a.Empleado)
            .AsQueryable();

        if (empleadoId.HasValue)
        {
            query = query.Where(a => a.EmpleadoId == empleadoId.Value);
        }

        if (estado.HasValue)
        {
            query = query.Where(a => a.Estado == estado.Value);
        }

        var anticipos = await query.AsNoTracking().OrderByDescending(a => a.FechaSolicitud).ToListAsync();
        var anticiposDto = anticipos.Select(MapToDto);

        return Ok(anticiposDto);
    }

    /// <summary>
    /// Obtiene un anticipo específico por su ID.
    /// </summary>
    /// <param name="id">ID del anticipo.</param>
    /// <returns>Detalles del anticipo.</returns>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var tenantId = _tenantContext.TenantId;
        var anticipo = await _context.Anticipos
            .Where(a => a.Id == id && a.TenantId == tenantId)
            .Include(a => a.Empleado)
            .AsNoTracking()
            .FirstOrDefaultAsync();

        if (anticipo == null)
        {
            return NotFound();
        }

        var anticipoDto = MapToDto(anticipo);
        return Ok(anticipoDto);
    }

    /// <summary>
    /// Obtiene todos los anticipos pendientes de aprobación.
    /// </summary>
    /// <returns>Lista de anticipos pendientes.</returns>
    [HttpGet("pendientes")]
    public async Task<IActionResult> GetPendientes()
    {
        var tenantId = _tenantContext.TenantId;
        var anticipos = await _context.Anticipos
            .Where(a => a.TenantId == tenantId && a.Estado == EstadoAnticipo.Pendiente)
            .Include(a => a.Empleado)
            .AsNoTracking()
            .OrderBy(a => a.FechaSolicitud)
            .ToListAsync();

        var anticiposDto = anticipos.Select(MapToDto);
        return Ok(anticiposDto);
    }

    /// <summary>
    /// Crea una nueva solicitud de anticipo.
    /// </summary>
    /// <param name="request">Datos del anticipo a crear.</param>
    /// <returns>Anticipo creado.</returns>
    [HttpPost]
    [Authorize(Roles = "Owner,Admin,Manager")]
    public async Task<IActionResult> Create(CreateAnticipoRequest request)
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
        if (request.Monto <= 0)
        {
            return BadRequest(new { message = "El monto debe ser mayor a cero" });
        }

        if (string.IsNullOrWhiteSpace(request.Motivo))
        {
            return BadRequest(new { message = "El motivo es requerido" });
        }

        if (request.FechaDescuento < DateTime.Today)
        {
            return BadRequest(new { message = "La fecha de descuento no puede ser anterior a hoy" });
        }

        // Verificar que no tenga anticipos pendientes o aprobados sin descontar
        var anticiposPendientes = await _context.Anticipos
            .Where(a => a.EmpleadoId == request.EmpleadoId &&
                       a.TenantId == tenantId &&
                       (a.Estado == EstadoAnticipo.Pendiente || a.Estado == EstadoAnticipo.Aprobado))
            .AnyAsync();

        if (anticiposPendientes)
        {
            return BadRequest(new { message = "El empleado ya tiene un anticipo pendiente o aprobado" });
        }

        var anticipo = new Anticipo
        {
            TenantId = tenantId,
            EmpleadoId = request.EmpleadoId,
            Monto = request.Monto,
            FechaSolicitud = DateTime.UtcNow,
            FechaDescuento = request.FechaDescuento,
            Estado = EstadoAnticipo.Pendiente,
            Motivo = request.Motivo,
            CreatedAt = DateTime.UtcNow
        };

        var repository = _unitOfWork.Repository<Anticipo>();
        await repository.AddAsync(anticipo);
        await _unitOfWork.CompleteAsync();

        // Recargar con navegación
        anticipo = await _context.Anticipos
            .Include(a => a.Empleado)
            .FirstOrDefaultAsync(a => a.Id == anticipo.Id);

        var anticipoDto = MapToDto(anticipo!);
        return CreatedAtAction(nameof(GetById), new { id = anticipo!.Id }, anticipoDto);
    }

    /// <summary>
    /// Aprueba un anticipo pendiente.
    /// </summary>
    /// <param name="id">ID del anticipo.</param>
    /// <param name="request">Datos de aprobación (aprobador).</param>
    /// <returns>NoContent si fue exitoso.</returns>
    [HttpPost("{id}/aprobar")]
    [Authorize(Roles = "Owner,Admin,Manager")]
    public async Task<IActionResult> Aprobar(int id, [FromBody] AprobarAnticipoRequest request)
    {
        var tenantId = _tenantContext.TenantId;
        var anticipo = await _context.Anticipos
            .FirstOrDefaultAsync(a => a.Id == id && a.TenantId == tenantId);

        if (anticipo == null)
        {
            return NotFound();
        }

        if (anticipo.Estado != EstadoAnticipo.Pendiente)
        {
            return BadRequest(new { message = "Solo se pueden aprobar anticipos pendientes" });
        }

        anticipo.Estado = EstadoAnticipo.Aprobado;
        anticipo.FechaAprobacion = DateTime.UtcNow;
        anticipo.AprobadoPor = request.AprobadoPor ?? "Sistema";
        anticipo.UpdatedAt = DateTime.UtcNow;

        await _unitOfWork.CompleteAsync();
        return NoContent();
    }

    /// <summary>
    /// Rechaza un anticipo pendiente.
    /// </summary>
    /// <param name="id">ID del anticipo.</param>
    /// <param name="request">Datos de rechazo (motivo).</param>
    /// <returns>NoContent si fue exitoso.</returns>
    [HttpPost("{id}/rechazar")]
    [Authorize(Roles = "Owner,Admin,Manager")]
    public async Task<IActionResult> Rechazar(int id, [FromBody] RechazarAnticipoRequest request)
    {
        var tenantId = _tenantContext.TenantId;
        var anticipo = await _context.Anticipos
            .FirstOrDefaultAsync(a => a.Id == id && a.TenantId == tenantId);

        if (anticipo == null)
        {
            return NotFound();
        }

        if (anticipo.Estado != EstadoAnticipo.Pendiente)
        {
            return BadRequest(new { message = "Solo se pueden rechazar anticipos pendientes" });
        }

        anticipo.Estado = EstadoAnticipo.Rechazado;
        anticipo.Observaciones = request.MotivoRechazo;
        anticipo.UpdatedAt = DateTime.UtcNow;

        await _unitOfWork.CompleteAsync();
        return NoContent();
    }

    /// <summary>
    /// Cancela un anticipo (solo si está pendiente).
    /// </summary>
    /// <param name="id">ID del anticipo.</param>
    /// <returns>NoContent si fue exitoso.</returns>
    [HttpDelete("{id}/cancelar")]
    [Authorize(Roles = "Owner,Admin")]
    public async Task<IActionResult> Cancelar(int id)
    {
        var tenantId = _tenantContext.TenantId;
        var anticipo = await _context.Anticipos
            .FirstOrDefaultAsync(a => a.Id == id && a.TenantId == tenantId);

        if (anticipo == null)
        {
            return NotFound();
        }

        if (anticipo.Estado != EstadoAnticipo.Pendiente)
        {
            return BadRequest(new { message = "Solo se pueden cancelar anticipos pendientes" });
        }

        anticipo.Estado = EstadoAnticipo.Cancelado;
        anticipo.UpdatedAt = DateTime.UtcNow;

        await _unitOfWork.CompleteAsync();
        return NoContent();
    }

    /// <summary>
    /// Mapea una entidad Anticipo a AnticipoDto.
    /// </summary>
    private static AnticipoDto MapToDto(Anticipo anticipo)
    {
        var nombreCompleto = anticipo.Empleado != null
            ? $"{anticipo.Empleado.Nombre} {anticipo.Empleado.Apellido}".Trim()
            : string.Empty;

        return new AnticipoDto(
            Id: anticipo.Id,
            EmpleadoId: anticipo.EmpleadoId,
            EmpleadoNombre: nombreCompleto,
            Monto: anticipo.Monto,
            FechaSolicitud: anticipo.FechaSolicitud,
            FechaAprobacion: anticipo.FechaAprobacion,
            FechaDescuento: anticipo.FechaDescuento,
            Estado: anticipo.Estado,
            EstadoNombre: GetEstadoNombre(anticipo.Estado),
            Motivo: anticipo.Motivo,
            AprobadoPor: anticipo.AprobadoPor
        );
    }

    /// <summary>
    /// Obtiene el nombre legible del estado del anticipo.
    /// </summary>
    private static string GetEstadoNombre(EstadoAnticipo estado)
    {
        return estado switch
        {
            EstadoAnticipo.Pendiente => "Pendiente",
            EstadoAnticipo.Aprobado => "Aprobado",
            EstadoAnticipo.Descontado => "Descontado",
            EstadoAnticipo.Rechazado => "Rechazado",
            EstadoAnticipo.Cancelado => "Cancelado",
            _ => estado.ToString()
        };
    }
}

/// <summary>
/// DTO para aprobación de anticipo.
/// </summary>
public record AprobarAnticipoRequest(string? AprobadoPor);

/// <summary>
/// DTO para rechazo de anticipo.
/// </summary>
public record RechazarAnticipoRequest(string MotivoRechazo);
