using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Vorluno.Planilla.Application.DTOs;
using Vorluno.Planilla.Application.Interfaces;
using Vorluno.Planilla.Domain.Entities;
using Vorluno.Planilla.Infrastructure.Data;

namespace Vorluno.Planilla.Web.Controllers;

/// <summary>
/// Controlador de API para gestionar las operaciones CRUD de posiciones.
/// </summary>
[Authorize]
[ApiController]
[Route("api/[controller]")]
public class PosicionesController : ControllerBase
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly ApplicationDbContext _context;
    private readonly ITenantContext _tenantContext;

    public PosicionesController(IUnitOfWork unitOfWork, IMapper mapper, ApplicationDbContext context, ITenantContext tenantContext)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _context = context;
        _tenantContext = tenantContext;
    }

    /// <summary>
    /// Obtiene una lista de todas las posiciones, opcionalmente filtradas por departamento.
    /// </summary>
    /// <param name="departamentoId">ID del departamento para filtrar (opcional).</param>
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] int? departamentoId = null)
    {
        var tenantId = _tenantContext.TenantId;
        var query = _context.Posiciones
            .Where(p => p.TenantId == tenantId)
            .Include(p => p.Departamento)
            .Include(p => p.Empleados)
            .AsQueryable();

        if (departamentoId.HasValue)
        {
            query = query.Where(p => p.DepartamentoId == departamentoId.Value);
        }

        var posiciones = await query.AsNoTracking().ToListAsync();

        var posicionesDto = posiciones.Select(p => new PosicionVerDto(
            p.Id,
            p.Nombre,
            p.Codigo,
            p.Descripcion,
            p.EstaActivo,
            p.DepartamentoId,
            p.Departamento.Nombre,
            p.SalarioMinimo,
            p.SalarioMaximo,
            p.NivelRiesgo,
            p.Empleados.Count
        ));

        return Ok(posicionesDto);
    }

    /// <summary>
    /// Obtiene una posición específica por su ID.
    /// </summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var tenantId = _tenantContext.TenantId;
        var posicion = await _context.Posiciones
            .Where(p => p.Id == id && p.TenantId == tenantId)
            .Include(p => p.Departamento)
            .Include(p => p.Empleados)
            .AsNoTracking()
            .FirstOrDefaultAsync();

        if (posicion == null)
        {
            return NotFound();
        }

        var posicionDto = new PosicionVerDto(
            posicion.Id,
            posicion.Nombre,
            posicion.Codigo,
            posicion.Descripcion,
            posicion.EstaActivo,
            posicion.DepartamentoId,
            posicion.Departamento.Nombre,
            posicion.SalarioMinimo,
            posicion.SalarioMaximo,
            posicion.NivelRiesgo,
            posicion.Empleados.Count
        );

        return Ok(posicionDto);
    }

    /// <summary>
    /// Crea una nueva posición.
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "Owner,Admin,Manager")]
    public async Task<IActionResult> Create(PosicionCrearDto posicionDto)
    {
        var tenantId = _tenantContext.TenantId;

        // Verificar que el departamento existe
        var departamento = await _context.Departamentos
            .FirstOrDefaultAsync(d => d.Id == posicionDto.DepartamentoId && d.TenantId == tenantId);
        if (departamento == null)
        {
            return BadRequest(new { message = "El departamento especificado no existe." });
        }

        // Verificar que el código sea único para la compañía
        var existe = await _context.Posiciones
            .AnyAsync(p => p.Codigo == posicionDto.Codigo && p.TenantId == tenantId);

        if (existe)
        {
            return BadRequest(new { message = $"Ya existe una posición con el código '{posicionDto.Codigo}'." });
        }

        // Validar rango salarial
        if (posicionDto.SalarioMinimo > posicionDto.SalarioMaximo)
        {
            return BadRequest(new { message = "El salario mínimo no puede ser mayor que el salario máximo." });
        }

        var posicion = new Posicion
        {
            TenantId = tenantId,
            Nombre = posicionDto.Nombre,
            Codigo = posicionDto.Codigo,
            Descripcion = posicionDto.Descripcion,
            DepartamentoId = posicionDto.DepartamentoId,
            SalarioMinimo = posicionDto.SalarioMinimo,
            SalarioMaximo = posicionDto.SalarioMaximo,
            NivelRiesgo = posicionDto.NivelRiesgo,
            CreatedAt = DateTime.UtcNow,
            EstaActivo = true
        };

        await _unitOfWork.Repository<Posicion>().AddAsync(posicion);
        await _unitOfWork.CompleteAsync();

        // Recargar con navegación para el DTO
        posicion = await _context.Posiciones
            .Include(p => p.Departamento)
            .FirstOrDefaultAsync(p => p.Id == posicion.Id);

        var posicionCreadaDto = new PosicionVerDto(
            posicion!.Id,
            posicion.Nombre,
            posicion.Codigo,
            posicion.Descripcion,
            posicion.EstaActivo,
            posicion.DepartamentoId,
            posicion.Departamento.Nombre,
            posicion.SalarioMinimo,
            posicion.SalarioMaximo,
            posicion.NivelRiesgo,
            0
        );

        return CreatedAtAction(nameof(GetById), new { id = posicion.Id }, posicionCreadaDto);
    }

    /// <summary>
    /// Actualiza una posición existente.
    /// </summary>
    [HttpPut("{id}")]
    [Authorize(Roles = "Owner,Admin,Manager")]
    public async Task<IActionResult> Update(int id, PosicionActualizarDto posicionDto)
    {
        var tenantId = _tenantContext.TenantId;
        var posicion = await _context.Posiciones
            .FirstOrDefaultAsync(p => p.Id == id && p.TenantId == tenantId);

        if (posicion == null)
        {
            return NotFound();
        }

        // Verificar que el departamento existe
        var departamento = await _context.Departamentos
            .FirstOrDefaultAsync(d => d.Id == posicionDto.DepartamentoId && d.TenantId == tenantId);
        if (departamento == null)
        {
            return BadRequest(new { message = "El departamento especificado no existe." });
        }

        // Verificar que el código sea único (excepto para esta posición)
        var existe = await _context.Posiciones
            .AnyAsync(p => p.Codigo == posicionDto.Codigo && p.Id != id && p.TenantId == tenantId);

        if (existe)
        {
            return BadRequest(new { message = $"Ya existe otra posición con el código '{posicionDto.Codigo}'." });
        }

        // Validar rango salarial
        if (posicionDto.SalarioMinimo > posicionDto.SalarioMaximo)
        {
            return BadRequest(new { message = "El salario mínimo no puede ser mayor que el salario máximo." });
        }

        posicion.Nombre = posicionDto.Nombre;
        posicion.Codigo = posicionDto.Codigo;
        posicion.Descripcion = posicionDto.Descripcion;
        posicion.DepartamentoId = posicionDto.DepartamentoId;
        posicion.SalarioMinimo = posicionDto.SalarioMinimo;
        posicion.SalarioMaximo = posicionDto.SalarioMaximo;
        posicion.NivelRiesgo = posicionDto.NivelRiesgo;
        posicion.EstaActivo = posicionDto.EstaActivo;
        posicion.UpdatedAt = DateTime.UtcNow;

        _unitOfWork.Repository<Posicion>().Update(posicion);
        await _unitOfWork.CompleteAsync();

        return NoContent();
    }

    /// <summary>
    /// Elimina (desactiva) una posición.
    /// </summary>
    [HttpDelete("{id}")]
    [Authorize(Roles = "Owner,Admin")]
    public async Task<IActionResult> Delete(int id)
    {
        var tenantId = _tenantContext.TenantId;
        var posicion = await _context.Posiciones
            .Where(p => p.Id == id && p.TenantId == tenantId)
            .Include(p => p.Empleados)
            .FirstOrDefaultAsync();

        if (posicion == null)
        {
            return NotFound();
        }

        // Verificar si tiene empleados asignados
        if (posicion.Empleados.Any(e => e.EstaActivo))
        {
            return BadRequest(new { message = "No se puede eliminar la posición porque tiene empleados asignados. Reasigne primero los empleados." });
        }

        // Soft delete
        posicion.EstaActivo = false;
        posicion.UpdatedAt = DateTime.UtcNow;

        _unitOfWork.Repository<Posicion>().Update(posicion);
        await _unitOfWork.CompleteAsync();

        return NoContent();
    }
}
