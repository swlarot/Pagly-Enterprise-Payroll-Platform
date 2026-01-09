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
/// Controlador para gestionar solicitudes de vacaciones
/// </summary>
[Authorize] // ✅ SEGURIDAD: Todos los endpoints requieren autenticación
[ApiController]
[Route("api/[controller]")]
public class VacacionesController : ControllerBase
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ApplicationDbContext _context;
    private readonly ITenantContext _tenantContext;

    public VacacionesController(IUnitOfWork unitOfWork, ApplicationDbContext context, ITenantContext tenantContext)
    {
        _unitOfWork = unitOfWork;
        _context = context;
        _tenantContext = tenantContext;
    }

    /// <summary>
    /// Obtiene lista de solicitudes de vacaciones
    /// </summary>
    [HttpGet]
    [Authorize(Roles = "Owner,Admin,Manager,Accountant")]
    public async Task<IActionResult> GetAll([FromQuery] int? empleadoId = null, [FromQuery] EstadoVacaciones? estado = null)
    {
        var tenantId = _tenantContext.TenantId;
        var query = _context.SolicitudesVacaciones
            .Where(v => v.TenantId == tenantId)
            .Include(v => v.Empleado)
            .AsNoTracking()
            .AsQueryable();

        if (empleadoId.HasValue)
            query = query.Where(v => v.EmpleadoId == empleadoId.Value);

        if (estado.HasValue)
            query = query.Where(v => v.Estado == estado.Value);

        var solicitudes = await query.OrderByDescending(v => v.FechaSolicitud).ToListAsync();

        var dtos = solicitudes.Select(MapToDto);
        return Ok(dtos);
    }

    /// <summary>
    /// Obtiene una solicitud por ID
    /// </summary>
    [HttpGet("{id}")]
    [Authorize(Roles = "Owner,Admin,Manager,Accountant")]
    public async Task<IActionResult> GetById(int id)
    {
        var tenantId = _tenantContext.TenantId;
        var solicitud = await _context.SolicitudesVacaciones
            .Where(v => v.Id == id && v.TenantId == tenantId)
            .Include(v => v.Empleado)
            .AsNoTracking()
            .FirstOrDefaultAsync();

        if (solicitud == null)
            return NotFound(); // 404 previene info leak

        return Ok(MapToDto(solicitud));
    }

    /// <summary>
    /// Obtiene solicitudes de un empleado
    /// </summary>
    [HttpGet("empleado/{empleadoId}")]
    [Authorize(Roles = "Owner,Admin,Manager,Accountant")]
    public async Task<IActionResult> GetByEmpleado(int empleadoId)
    {
        var tenantId = _tenantContext.TenantId;
        var solicitudes = await _context.SolicitudesVacaciones
            .Where(v => v.EmpleadoId == empleadoId && v.TenantId == tenantId)
            .Include(v => v.Empleado)
            .AsNoTracking()
            .OrderByDescending(v => v.FechaSolicitud)
            .ToListAsync();

        var dtos = solicitudes.Select(MapToDto);
        return Ok(dtos);
    }

    /// <summary>
    /// Obtiene solicitudes pendientes de aprobar
    /// </summary>
    [HttpGet("pendientes")]
    [Authorize(Roles = "Owner,Admin,Manager")]
    public async Task<IActionResult> GetPendientes()
    {
        var tenantId = _tenantContext.TenantId;
        var pendientes = await _context.SolicitudesVacaciones
            .Where(v => v.TenantId == tenantId && v.Estado == EstadoVacaciones.Pendiente)
            .Include(v => v.Empleado)
            .AsNoTracking()
            .OrderBy(v => v.FechaSolicitud)
            .ToListAsync();

        var dtos = pendientes.Select(MapToDto);
        return Ok(dtos);
    }

    /// <summary>
    /// Obtiene saldo de vacaciones de un empleado
    /// </summary>
    [HttpGet("saldo/{empleadoId}")]
    [Authorize(Roles = "Owner,Admin,Manager,Accountant")]
    public async Task<IActionResult> GetSaldo(int empleadoId)
    {
        var tenantId = _tenantContext.TenantId;
        var empleado = await _context.Empleados
            .Where(e => e.Id == empleadoId && e.TenantId == tenantId)
            .AsNoTracking()
            .FirstOrDefaultAsync();

        if (empleado == null)
            return NotFound(); // 404 previene info leak

        var saldo = await _context.SaldosVacaciones
            .Where(s => s.EmpleadoId == empleadoId && s.TenantId == tenantId)
            .AsNoTracking()
            .FirstOrDefaultAsync();

        if (saldo == null)
        {
            // Crear saldo inicial si no existe
            saldo = await CrearSaldoInicial(empleadoId);
        }

        var dto = new SaldoVacacionesDto(
            saldo.EmpleadoId,
            $"{empleado.Nombre} {empleado.Apellido}",
            saldo.DiasAcumulados,
            saldo.DiasTomados,
            saldo.DiasDisponibles,
            saldo.UltimaActualizacion,
            saldo.PeriodoInicio,
            saldo.PeriodoFin
        );

        return Ok(dto);
    }

    /// <summary>
    /// Obtiene calendario de vacaciones aprobadas
    /// </summary>
    [HttpGet("calendario")]
    [Authorize(Roles = "Owner,Admin,Manager,Accountant")]
    public async Task<IActionResult> GetCalendario([FromQuery] DateTime? fecha = null)
    {
        var tenantId = _tenantContext.TenantId;
        var fechaConsulta = fecha ?? DateTime.Today;
        var inicioMes = new DateTime(fechaConsulta.Year, fechaConsulta.Month, 1);
        var finMes = inicioMes.AddMonths(1).AddDays(-1);

        var vacaciones = await _context.SolicitudesVacaciones
            .Where(v => v.TenantId == tenantId)
            .Where(v => v.Estado == EstadoVacaciones.Aprobada ||
                       v.Estado == EstadoVacaciones.EnCurso ||
                       v.Estado == EstadoVacaciones.Completada)
            .Where(v => v.FechaInicio <= finMes && v.FechaFin >= inicioMes)
            .Include(v => v.Empleado)
            .AsNoTracking()
            .ToListAsync();

        var dtos = vacaciones.Select(MapToDto);
        return Ok(dtos);
    }

    /// <summary>
    /// Crea una nueva solicitud de vacaciones
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "Owner,Admin,Manager")]
    public async Task<IActionResult> Create(CreateVacacionesRequest request)
    {
        var tenantId = _tenantContext.TenantId;
        var empleado = await _context.Empleados
            .Where(e => e.Id == request.EmpleadoId && e.TenantId == tenantId)
            .FirstOrDefaultAsync();

        if (empleado == null)
            return BadRequest(new { message = "El empleado especificado no existe." });

        // Calcular días
        var diasSolicitados = CalcularDiasHabiles(request.FechaInicio, request.FechaFin);

        // Obtener o crear saldo
        var saldo = await _context.SaldosVacaciones
            .Where(s => s.EmpleadoId == request.EmpleadoId && s.TenantId == tenantId)
            .FirstOrDefaultAsync();

        if (saldo == null)
        {
            saldo = await CrearSaldoInicial(request.EmpleadoId);
        }

        // Validar días disponibles
        if (diasSolicitados > saldo.DiasDisponibles)
        {
            return BadRequest(new { message = $"El empleado solo tiene {saldo.DiasDisponibles} días disponibles." });
        }

        var solicitud = new SolicitudVacaciones
        {
            EmpleadoId = request.EmpleadoId,
            FechaInicio = request.FechaInicio.Date,
            FechaFin = request.FechaFin.Date,
            DiasVacaciones = diasSolicitados,
            DiasProporcionales = saldo.DiasDisponibles,
            Estado = EstadoVacaciones.Pendiente,
            FechaSolicitud = DateTime.UtcNow,
            Observaciones = request.Observaciones,
            TenantId = _tenantContext.TenantId,
            CreatedAt = DateTime.UtcNow
        };

        await _unitOfWork.Repository<SolicitudVacaciones>().AddAsync(solicitud);
        await _unitOfWork.CompleteAsync();

        // Recargar con navegación
        solicitud = await _context.SolicitudesVacaciones
            .Where(v => v.Id == solicitud.Id && v.TenantId == tenantId)
            .Include(v => v.Empleado)
            .FirstOrDefaultAsync();

        return CreatedAtAction(nameof(GetById), new { id = solicitud!.Id }, MapToDto(solicitud));
    }

    /// <summary>
    /// Aprueba una solicitud de vacaciones
    /// </summary>
    [HttpPost("{id}/aprobar")]
    [Authorize(Roles = "Owner,Admin,Manager")]
    public async Task<IActionResult> Aprobar(int id)
    {
        var tenantId = _tenantContext.TenantId;
        var solicitud = await _context.SolicitudesVacaciones
            .FirstOrDefaultAsync(v => v.Id == id && v.TenantId == tenantId);

        if (solicitud == null)
            return NotFound(); // 404 previene info leak

        if (solicitud.Estado != EstadoVacaciones.Pendiente)
            return BadRequest(new { message = "Solo se pueden aprobar solicitudes pendientes." });

        solicitud.Estado = EstadoVacaciones.Aprobada;
        solicitud.FechaAprobacion = DateTime.UtcNow;
        solicitud.AprobadoPor = "Sistema"; // TODO: Usuario actual
        solicitud.UpdatedAt = DateTime.UtcNow;

        _unitOfWork.Repository<SolicitudVacaciones>().Update(solicitud);
        await _unitOfWork.CompleteAsync();

        return NoContent();
    }

    /// <summary>
    /// Rechaza una solicitud de vacaciones
    /// </summary>
    [HttpPost("{id}/rechazar")]
    [Authorize(Roles = "Owner,Admin,Manager")]
    public async Task<IActionResult> Rechazar(int id, [FromBody] RechazarVacacionesRequest request)
    {
        var tenantId = _tenantContext.TenantId;
        var solicitud = await _context.SolicitudesVacaciones
            .FirstOrDefaultAsync(v => v.Id == id && v.TenantId == tenantId);

        if (solicitud == null)
            return NotFound(); // 404 previene info leak

        if (solicitud.Estado != EstadoVacaciones.Pendiente)
            return BadRequest(new { message = "Solo se pueden rechazar solicitudes pendientes." });

        solicitud.Estado = EstadoVacaciones.Rechazada;
        solicitud.FechaRechazo = DateTime.UtcNow;
        solicitud.RechazadoPor = "Sistema"; // TODO: Usuario actual
        solicitud.MotivoRechazo = request.Motivo;
        solicitud.UpdatedAt = DateTime.UtcNow;

        _unitOfWork.Repository<SolicitudVacaciones>().Update(solicitud);
        await _unitOfWork.CompleteAsync();

        return NoContent();
    }

    /// <summary>
    /// Cancela una solicitud de vacaciones
    /// </summary>
    [HttpDelete("{id}/cancelar")]
    [Authorize(Roles = "Owner,Admin,Manager")]
    public async Task<IActionResult> Cancelar(int id)
    {
        var tenantId = _tenantContext.TenantId;
        var solicitud = await _context.SolicitudesVacaciones
            .FirstOrDefaultAsync(v => v.Id == id && v.TenantId == tenantId);

        if (solicitud == null)
            return NotFound(); // 404 previene info leak

        if (solicitud.Estado == EstadoVacaciones.Completada)
            return BadRequest(new { message = "No se puede cancelar una solicitud ya completada." });

        if (solicitud.Estado == EstadoVacaciones.EnCurso)
            return BadRequest(new { message = "No se puede cancelar vacaciones en curso." });

        solicitud.Estado = EstadoVacaciones.Cancelada;
        solicitud.UpdatedAt = DateTime.UtcNow;

        _unitOfWork.Repository<SolicitudVacaciones>().Update(solicitud);
        await _unitOfWork.CompleteAsync();

        return NoContent();
    }

    // Métodos privados
    private int CalcularDiasHabiles(DateTime inicio, DateTime fin)
    {
        // Simplificado: cuenta todos los días (incluye sábados y domingos)
        // TODO: Implementar cálculo excluyendo fines de semana y feriados
        var dias = (fin.Date - inicio.Date).Days + 1;
        return dias;
    }

    private async Task<SaldoVacaciones> CrearSaldoInicial(int empleadoId)
    {
        var tenantId = _tenantContext.TenantId;
        var empleado = await _context.Empleados
            .Where(e => e.Id == empleadoId && e.TenantId == tenantId)
            .FirstOrDefaultAsync();

        if (empleado == null)
            throw new Exception("Empleado no encontrado");

        // Calcular días proporcionales según antigüedad
        // Panamá: 30 días por año completo trabajado
        var añosTrabajados = (DateTime.Today - empleado.FechaContratacion).Days / 365.25;
        var diasAcumulados = Math.Round((decimal)(añosTrabajados * 30), 2);

        var saldo = new SaldoVacaciones
        {
            EmpleadoId = empleadoId,
            DiasAcumulados = diasAcumulados,
            DiasTomados = 0,
            DiasDisponibles = diasAcumulados,
            UltimaActualizacion = DateTime.UtcNow,
            PeriodoInicio = new DateTime(DateTime.Today.Year, 1, 1),
            PeriodoFin = new DateTime(DateTime.Today.Year, 12, 31),
            TenantId = _tenantContext.TenantId,
            CreatedAt = DateTime.UtcNow
        };

        await _unitOfWork.Repository<SaldoVacaciones>().AddAsync(saldo);
        await _unitOfWork.CompleteAsync();

        return saldo;
    }

    private string ObtenerNombreEstado(EstadoVacaciones estado)
    {
        return estado switch
        {
            EstadoVacaciones.Pendiente => "Pendiente",
            EstadoVacaciones.Aprobada => "Aprobada",
            EstadoVacaciones.EnCurso => "En Curso",
            EstadoVacaciones.Completada => "Completada",
            EstadoVacaciones.Cancelada => "Cancelada",
            EstadoVacaciones.Rechazada => "Rechazada",
            _ => "Desconocido"
        };
    }

    private VacacionesDto MapToDto(SolicitudVacaciones vacacion)
    {
        return new VacacionesDto(
            vacacion.Id,
            vacacion.EmpleadoId,
            $"{vacacion.Empleado.Nombre} {vacacion.Empleado.Apellido}",
            vacacion.FechaInicio,
            vacacion.FechaFin,
            vacacion.DiasVacaciones,
            vacacion.DiasProporcionales,
            vacacion.Estado,
            ObtenerNombreEstado(vacacion.Estado),
            vacacion.FechaSolicitud,
            vacacion.AprobadoPor,
            vacacion.FechaAprobacion,
            vacacion.MotivoRechazo
        );
    }
}

// DTO auxiliar para rechazar
public record RechazarVacacionesRequest(string Motivo);
