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
/// Controlador de API para gestionar préstamos a empleados.
/// </summary>
[Authorize] // ✅ SEGURIDAD: Todos los endpoints requieren autenticación
[ApiController]
[Route("api/[controller]")]
public class PrestamosController : ControllerBase
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ApplicationDbContext _context;
    private readonly ITenantContext _tenantContext;

    public PrestamosController(IUnitOfWork unitOfWork, ApplicationDbContext context, ITenantContext tenantContext)
    {
        _unitOfWork = unitOfWork;
        _context = context;
        _tenantContext = tenantContext;
    }

    /// <summary>
    /// Obtiene una lista de préstamos con filtros opcionales.
    /// </summary>
    /// <param name="empleadoId">Filtrar por empleado (opcional).</param>
    /// <param name="estado">Filtrar por estado (opcional).</param>
    /// <returns>Lista de préstamos.</returns>
    [HttpGet]
    [Authorize(Roles = "Owner,Admin,Manager,Accountant")]
    public async Task<IActionResult> GetAll([FromQuery] int? empleadoId, [FromQuery] EstadoPrestamo? estado)
    {
        var tenantId = _tenantContext.TenantId;
        var query = _context.Prestamos
            .Where(p => p.TenantId == tenantId)
            .Include(p => p.Empleado)
            .AsNoTracking()
            .AsQueryable();

        if (empleadoId.HasValue)
        {
            query = query.Where(p => p.EmpleadoId == empleadoId.Value);
        }

        if (estado.HasValue)
        {
            query = query.Where(p => p.Estado == estado.Value);
        }

        var prestamos = await query.OrderByDescending(p => p.FechaInicio).ToListAsync();
        var prestamosDto = prestamos.Select(MapToDto);

        return Ok(prestamosDto);
    }

    /// <summary>
    /// Obtiene un préstamo específico por su ID con historial de pagos.
    /// </summary>
    /// <param name="id">ID del préstamo.</param>
    /// <returns>Detalles del préstamo.</returns>
    [HttpGet("{id}")]
    [Authorize(Roles = "Owner,Admin,Manager,Accountant")]
    public async Task<IActionResult> GetById(int id)
    {
        var tenantId = _tenantContext.TenantId;
        var prestamo = await _context.Prestamos
            .Where(p => p.Id == id && p.TenantId == tenantId)
            .Include(p => p.Empleado)
            .Include(p => p.PagosPrestamo)
            .AsNoTracking()
            .FirstOrDefaultAsync();

        if (prestamo == null)
        {
            return NotFound(); // 404 previene info leak
        }

        var prestamoDto = MapToDto(prestamo);
        return Ok(prestamoDto);
    }

    /// <summary>
    /// Obtiene todos los préstamos de un empleado específico.
    /// </summary>
    /// <param name="empleadoId">ID del empleado.</param>
    /// <returns>Lista de préstamos del empleado.</returns>
    [HttpGet("empleado/{empleadoId}")]
    [Authorize(Roles = "Owner,Admin,Manager,Accountant")]
    public async Task<IActionResult> GetByEmpleado(int empleadoId)
    {
        var tenantId = _tenantContext.TenantId;
        var empleado = await _context.Empleados
            .Where(e => e.Id == empleadoId && e.TenantId == tenantId)
            .AsNoTracking()
            .FirstOrDefaultAsync();

        if (empleado == null)
        {
            return NotFound(new { message = "Empleado no encontrado" });
        }

        var prestamos = await _context.Prestamos
            .Where(p => p.EmpleadoId == empleadoId && p.TenantId == tenantId)
            .Include(p => p.Empleado)
            .AsNoTracking()
            .OrderByDescending(p => p.FechaInicio)
            .ToListAsync();

        var prestamosDto = prestamos.Select(MapToDto);
        return Ok(prestamosDto);
    }

    /// <summary>
    /// Crea un nuevo préstamo.
    /// </summary>
    /// <param name="request">Datos del préstamo a crear.</param>
    /// <returns>Préstamo creado.</returns>
    [HttpPost]
    [Authorize(Roles = "Owner,Admin,Manager")]
    public async Task<IActionResult> Create(CreatePrestamoRequest request)
    {
        var tenantId = _tenantContext.TenantId;
        // Validar empleado existe y pertenece al tenant
        var empleado = await _context.Empleados
            .Where(e => e.Id == request.EmpleadoId && e.TenantId == tenantId)
            .FirstOrDefaultAsync();

        if (empleado == null)
        {
            return BadRequest(new { message = "Empleado no encontrado" });
        }

        // Validaciones de negocio
        if (request.MontoOriginal <= 0)
        {
            return BadRequest(new { message = "El monto original debe ser mayor a cero" });
        }

        if (request.CuotaMensual <= 0)
        {
            return BadRequest(new { message = "La cuota mensual debe ser mayor a cero" });
        }

        if (request.NumeroCuotas <= 0)
        {
            return BadRequest(new { message = "El número de cuotas debe ser mayor a cero" });
        }

        if (request.TasaInteres < 0)
        {
            return BadRequest(new { message = "La tasa de interés no puede ser negativa" });
        }

        var prestamo = new Prestamo
        {
            EmpleadoId = request.EmpleadoId,
            TenantId = _tenantContext.TenantId,
            Descripcion = request.Descripcion,
            MontoOriginal = request.MontoOriginal,
            MontoPendiente = request.MontoOriginal,
            CuotaMensual = request.CuotaMensual,
            TasaInteres = request.TasaInteres,
            FechaInicio = request.FechaInicio,
            NumeroCuotas = request.NumeroCuotas,
            CuotasPagadas = 0,
            Estado = EstadoPrestamo.Activo,
            Referencia = request.Referencia,
            Observaciones = request.Observaciones,
            CreatedAt = DateTime.UtcNow
        };

        // Calcular fecha fin estimada
        if (request.NumeroCuotas > 0)
        {
            prestamo.FechaFin = request.FechaInicio.AddMonths(request.NumeroCuotas);
        }

        var repository = _unitOfWork.Repository<Prestamo>();
        await repository.AddAsync(prestamo);
        await _unitOfWork.CompleteAsync();

        // Recargar con navegación
        prestamo = await _context.Prestamos
            .Where(p => p.Id == prestamo.Id && p.TenantId == tenantId)
            .Include(p => p.Empleado)
            .FirstOrDefaultAsync();

        var prestamoDto = MapToDto(prestamo!);
        return CreatedAtAction(nameof(GetById), new { id = prestamo!.Id }, prestamoDto);
    }

    /// <summary>
    /// Actualiza un préstamo existente.
    /// </summary>
    /// <param name="id">ID del préstamo.</param>
    /// <param name="request">Datos actualizados.</param>
    /// <returns>NoContent si fue exitoso.</returns>
    [HttpPut("{id}")]
    [Authorize(Roles = "Owner,Admin,Manager")]
    public async Task<IActionResult> Update(int id, CreatePrestamoRequest request)
    {
        var tenantId = _tenantContext.TenantId;
        var prestamo = await _context.Prestamos
            .FirstOrDefaultAsync(p => p.Id == id && p.TenantId == tenantId);

        if (prestamo == null)
        {
            return NotFound(); // 404 previene info leak
        }

        // Validaciones
        if (request.MontoOriginal <= 0)
        {
            return BadRequest(new { message = "El monto original debe ser mayor a cero" });
        }

        if (request.CuotaMensual <= 0)
        {
            return BadRequest(new { message = "La cuota mensual debe ser mayor a cero" });
        }

        // Solo permitir actualización si el préstamo está activo o suspendido
        if (prestamo.Estado == EstadoPrestamo.Pagado || prestamo.Estado == EstadoPrestamo.Cancelado)
        {
            return BadRequest(new { message = "No se puede actualizar un préstamo pagado o cancelado" });
        }

        prestamo.Descripcion = request.Descripcion;
        prestamo.CuotaMensual = request.CuotaMensual;
        prestamo.TasaInteres = request.TasaInteres;
        prestamo.Referencia = request.Referencia;
        prestamo.Observaciones = request.Observaciones;
        prestamo.UpdatedAt = DateTime.UtcNow;

        await _unitOfWork.CompleteAsync();
        return NoContent();
    }

    /// <summary>
    /// Suspende un préstamo activo.
    /// </summary>
    /// <param name="id">ID del préstamo.</param>
    /// <returns>NoContent si fue exitoso.</returns>
    [HttpPost("{id}/suspender")]
    [Authorize(Roles = "Owner,Admin,Manager")]
    public async Task<IActionResult> Suspender(int id)
    {
        var tenantId = _tenantContext.TenantId;
        var prestamo = await _context.Prestamos
            .FirstOrDefaultAsync(p => p.Id == id && p.TenantId == tenantId);

        if (prestamo == null)
        {
            return NotFound(); // 404 previene info leak
        }

        if (prestamo.Estado != EstadoPrestamo.Activo)
        {
            return BadRequest(new { message = "Solo se pueden suspender préstamos activos" });
        }

        prestamo.Estado = EstadoPrestamo.Suspendido;
        prestamo.UpdatedAt = DateTime.UtcNow;

        await _unitOfWork.CompleteAsync();
        return NoContent();
    }

    /// <summary>
    /// Reactiva un préstamo suspendido.
    /// </summary>
    /// <param name="id">ID del préstamo.</param>
    /// <returns>NoContent si fue exitoso.</returns>
    [HttpPost("{id}/reactivar")]
    [Authorize(Roles = "Owner,Admin,Manager")]
    public async Task<IActionResult> Reactivar(int id)
    {
        var tenantId = _tenantContext.TenantId;
        var prestamo = await _context.Prestamos
            .FirstOrDefaultAsync(p => p.Id == id && p.TenantId == tenantId);

        if (prestamo == null)
        {
            return NotFound(); // 404 previene info leak
        }

        if (prestamo.Estado != EstadoPrestamo.Suspendido)
        {
            return BadRequest(new { message = "Solo se pueden reactivar préstamos suspendidos" });
        }

        prestamo.Estado = EstadoPrestamo.Activo;
        prestamo.UpdatedAt = DateTime.UtcNow;

        await _unitOfWork.CompleteAsync();
        return NoContent();
    }

    /// <summary>
    /// Cancela un préstamo.
    /// </summary>
    /// <param name="id">ID del préstamo.</param>
    /// <returns>NoContent si fue exitoso.</returns>
    [HttpDelete("{id}/cancelar")]
    [Authorize(Roles = "Owner,Admin")]
    public async Task<IActionResult> Cancelar(int id)
    {
        var tenantId = _tenantContext.TenantId;
        var prestamo = await _context.Prestamos
            .FirstOrDefaultAsync(p => p.Id == id && p.TenantId == tenantId);

        if (prestamo == null)
        {
            return NotFound(); // 404 previene info leak
        }

        if (prestamo.Estado == EstadoPrestamo.Pagado)
        {
            return BadRequest(new { message = "No se puede cancelar un préstamo ya pagado" });
        }

        if (prestamo.Estado == EstadoPrestamo.Cancelado)
        {
            return BadRequest(new { message = "El préstamo ya está cancelado" });
        }

        prestamo.Estado = EstadoPrestamo.Cancelado;
        prestamo.UpdatedAt = DateTime.UtcNow;

        await _unitOfWork.CompleteAsync();
        return NoContent();
    }

    /// <summary>
    /// Mapea una entidad Prestamo a PrestamoDto.
    /// </summary>
    private static PrestamoDto MapToDto(Prestamo prestamo)
    {
        var cuotasRestantes = prestamo.NumeroCuotas - prestamo.CuotasPagadas;
        var porcentajePagado = prestamo.NumeroCuotas > 0
            ? (decimal)prestamo.CuotasPagadas / prestamo.NumeroCuotas * 100
            : 0;

        var nombreCompleto = prestamo.Empleado != null
            ? $"{prestamo.Empleado.Nombre} {prestamo.Empleado.Apellido}".Trim()
            : string.Empty;

        return new PrestamoDto(
            Id: prestamo.Id,
            EmpleadoId: prestamo.EmpleadoId,
            EmpleadoNombre: nombreCompleto,
            Descripcion: prestamo.Descripcion,
            MontoOriginal: prestamo.MontoOriginal,
            MontoPendiente: prestamo.MontoPendiente,
            CuotaMensual: prestamo.CuotaMensual,
            TasaInteres: prestamo.TasaInteres,
            FechaInicio: prestamo.FechaInicio,
            FechaFin: prestamo.FechaFin,
            NumeroCuotas: prestamo.NumeroCuotas,
            CuotasPagadas: prestamo.CuotasPagadas,
            CuotasRestantes: cuotasRestantes,
            Estado: prestamo.Estado,
            EstadoNombre: GetEstadoNombre(prestamo.Estado),
            Referencia: prestamo.Referencia,
            PorcentajePagado: porcentajePagado
        );
    }

    /// <summary>
    /// Obtiene el nombre legible del estado del préstamo.
    /// </summary>
    private static string GetEstadoNombre(EstadoPrestamo estado)
    {
        return estado switch
        {
            EstadoPrestamo.Activo => "Activo",
            EstadoPrestamo.Pagado => "Pagado",
            EstadoPrestamo.Cancelado => "Cancelado",
            EstadoPrestamo.Suspendido => "Suspendido",
            _ => estado.ToString()
        };
    }
}
