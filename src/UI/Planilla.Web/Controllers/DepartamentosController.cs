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
/// Controlador de API para gestionar las operaciones CRUD de departamentos.
/// </summary>
[Authorize]
[ApiController]
[Route("api/[controller]")]
public class DepartamentosController : ControllerBase
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;
    private readonly ApplicationDbContext _context;
    private readonly ITenantContext _tenantContext;

    public DepartamentosController(IUnitOfWork unitOfWork, IMapper mapper, ApplicationDbContext context, ITenantContext tenantContext)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _context = context;
        _tenantContext = tenantContext;
    }

    /// <summary>
    /// Obtiene una lista de todos los departamentos con conteos de empleados y posiciones.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var tenantId = _tenantContext.TenantId;
        var departamentos = await _context.Departamentos
            .Where(d => d.TenantId == tenantId)
            .Include(d => d.Manager)
            .Include(d => d.Empleados)
            .Include(d => d.Posiciones)
            .AsNoTracking()
            .ToListAsync();

        var departamentosDto = departamentos.Select(d => new DepartamentoVerDto(
            d.Id,
            d.Nombre,
            d.Codigo,
            d.Descripcion,
            d.EstaActivo,
            d.ManagerId,
            d.Manager != null ? $"{d.Manager.Nombre} {d.Manager.Apellido}" : null,
            d.Empleados.Count,
            d.Posiciones.Count
        ));

        return Ok(departamentosDto);
    }

    /// <summary>
    /// Obtiene un departamento específico por su ID.
    /// </summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var tenantId = _tenantContext.TenantId;
        var departamento = await _context.Departamentos
            .Where(d => d.Id == id && d.TenantId == tenantId)
            .Include(d => d.Manager)
            .Include(d => d.Empleados)
            .Include(d => d.Posiciones)
            .AsNoTracking()
            .FirstOrDefaultAsync();

        if (departamento == null)
        {
            return NotFound();
        }

        var departamentoDto = new DepartamentoVerDto(
            departamento.Id,
            departamento.Nombre,
            departamento.Codigo,
            departamento.Descripcion,
            departamento.EstaActivo,
            departamento.ManagerId,
            departamento.Manager != null ? $"{departamento.Manager.Nombre} {departamento.Manager.Apellido}" : null,
            departamento.Empleados.Count,
            departamento.Posiciones.Count
        );

        return Ok(departamentoDto);
    }

    /// <summary>
    /// Crea un nuevo departamento.
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "Owner,Admin,Manager")]
    public async Task<IActionResult> Create(DepartamentoCrearDto departamentoDto)
    {
        var tenantId = _tenantContext.TenantId;

        // Verificar que el código sea único para la compañía
        var existe = await _context.Departamentos
            .AnyAsync(d => d.Codigo == departamentoDto.Codigo && d.TenantId == tenantId);

        if (existe)
        {
            return BadRequest(new { message = $"Ya existe un departamento con el código '{departamentoDto.Codigo}'." });
        }

        // Verificar que el manager existe si se proporciona
        if (departamentoDto.ManagerId.HasValue)
        {
            var managerExiste = await _unitOfWork.Empleados.GetByIdAsync(departamentoDto.ManagerId.Value);
            if (managerExiste == null)
            {
                return BadRequest(new { message = "El empleado seleccionado como jefe no existe." });
            }
        }

        var departamento = new Departamento
        {
            TenantId = tenantId,
            Nombre = departamentoDto.Nombre,
            Codigo = departamentoDto.Codigo,
            Descripcion = departamentoDto.Descripcion,
            ManagerId = departamentoDto.ManagerId,
            CreatedAt = DateTime.UtcNow,
            EstaActivo = true
        };

        await _unitOfWork.Repository<Departamento>().AddAsync(departamento);
        await _unitOfWork.CompleteAsync();

        // Recargar con navegación para el DTO
        departamento = await _context.Departamentos
            .Include(d => d.Manager)
            .FirstOrDefaultAsync(d => d.Id == departamento.Id);

        var departamentoCreadoDto = new DepartamentoVerDto(
            departamento!.Id,
            departamento.Nombre,
            departamento.Codigo,
            departamento.Descripcion,
            departamento.EstaActivo,
            departamento.ManagerId,
            departamento.Manager != null ? $"{departamento.Manager.Nombre} {departamento.Manager.Apellido}" : null,
            0,
            0
        );

        return CreatedAtAction(nameof(GetById), new { id = departamento.Id }, departamentoCreadoDto);
    }

    /// <summary>
    /// Actualiza un departamento existente.
    /// </summary>
    [HttpPut("{id}")]
    [Authorize(Roles = "Owner,Admin,Manager")]
    public async Task<IActionResult> Update(int id, DepartamentoActualizarDto departamentoDto)
    {
        var tenantId = _tenantContext.TenantId;
        var departamento = await _context.Departamentos
            .FirstOrDefaultAsync(d => d.Id == id && d.TenantId == tenantId);

        if (departamento == null)
        {
            return NotFound();
        }

        // Verificar que el código sea único (excepto para este departamento)
        var existe = await _context.Departamentos
            .AnyAsync(d => d.Codigo == departamentoDto.Codigo && d.Id != id && d.TenantId == tenantId);

        if (existe)
        {
            return BadRequest(new { message = $"Ya existe otro departamento con el código '{departamentoDto.Codigo}'." });
        }

        // Verificar que el manager existe si se proporciona
        if (departamentoDto.ManagerId.HasValue)
        {
            var managerExiste = await _unitOfWork.Empleados.GetByIdAsync(departamentoDto.ManagerId.Value);
            if (managerExiste == null)
            {
                return BadRequest(new { message = "El empleado seleccionado como jefe no existe." });
            }
        }

        departamento.Nombre = departamentoDto.Nombre;
        departamento.Codigo = departamentoDto.Codigo;
        departamento.Descripcion = departamentoDto.Descripcion;
        departamento.ManagerId = departamentoDto.ManagerId;
        departamento.EstaActivo = departamentoDto.EstaActivo;
        departamento.UpdatedAt = DateTime.UtcNow;

        _unitOfWork.Repository<Departamento>().Update(departamento);
        await _unitOfWork.CompleteAsync();

        return NoContent();
    }

    /// <summary>
    /// Elimina (desactiva) un departamento.
    /// </summary>
    [HttpDelete("{id}")]
    [Authorize(Roles = "Owner,Admin")]
    public async Task<IActionResult> Delete(int id)
    {
        var tenantId = _tenantContext.TenantId;
        var departamento = await _context.Departamentos
            .Where(d => d.Id == id && d.TenantId == tenantId)
            .Include(d => d.Posiciones)
            .FirstOrDefaultAsync();

        if (departamento == null)
        {
            return NotFound();
        }

        // Verificar si tiene posiciones activas
        if (departamento.Posiciones.Any(p => p.EstaActivo))
        {
            return BadRequest(new { message = "No se puede eliminar el departamento porque tiene posiciones activas. Desactive primero las posiciones." });
        }

        // Soft delete
        departamento.EstaActivo = false;
        departamento.UpdatedAt = DateTime.UtcNow;

        _unitOfWork.Repository<Departamento>().Update(departamento);
        await _unitOfWork.CompleteAsync();

        return NoContent();
    }
}
