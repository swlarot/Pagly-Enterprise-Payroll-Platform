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
/// Controlador CRUD para el catalogo maestro de acreedores.
/// </summary>
[Authorize]
[ApiController]
[Route("api/[controller]")]
public class AcreedoresController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ITenantContext _tenantContext;

    public AcreedoresController(ApplicationDbContext context, ITenantContext tenantContext)
    {
        _context = context;
        _tenantContext = tenantContext;
    }

    /// <summary>
    /// Lista acreedores con filtros opcionales.
    /// GET /api/acreedores?tipo=2&activos=true&search=banco
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] TipoAcreedor? tipo,
        [FromQuery] bool? activos,
        [FromQuery] string? search)
    {
        var query = _context.Acreedores
            .Include(a => a.Deducciones.Where(d => d.EstaActivo))
            .Include(a => a.Prestamos.Where(p => p.Estado == EstadoPrestamo.Activo))
            .AsQueryable();

        if (tipo.HasValue)
            query = query.Where(a => a.TipoAcreedor == tipo.Value);

        if (activos.HasValue)
            query = query.Where(a => a.EstaActivo == activos.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.ToLower();
            query = query.Where(a =>
                a.Nombre.ToLower().Contains(term) ||
                (a.Identificacion != null && a.Identificacion.ToLower().Contains(term)));
        }

        var acreedores = await query
            .OrderBy(a => a.Nombre)
            .Select(a => MapToDto(a))
            .ToListAsync();

        return Ok(acreedores);
    }

    /// <summary>
    /// Obtiene un acreedor por ID.
    /// GET /api/acreedores/{id}
    /// </summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var acreedor = await _context.Acreedores
            .Include(a => a.Deducciones.Where(d => d.EstaActivo))
            .Include(a => a.Prestamos.Where(p => p.Estado == EstadoPrestamo.Activo))
            .FirstOrDefaultAsync(a => a.Id == id);

        if (acreedor == null)
            return NotFound(new { message = "Acreedor no encontrado" });

        return Ok(MapToDto(acreedor));
    }

    /// <summary>
    /// Lista deducciones vinculadas a un acreedor.
    /// GET /api/acreedores/{id}/deducciones
    /// </summary>
    [HttpGet("{id}/deducciones")]
    public async Task<IActionResult> GetDeducciones(int id)
    {
        var acreedor = await _context.Acreedores.FindAsync(id);
        if (acreedor == null)
            return NotFound(new { message = "Acreedor no encontrado" });

        var deducciones = await _context.DeduccionesFijas
            .Include(d => d.Empleado)
            .Where(d => d.AcreedorId == id)
            .OrderByDescending(d => d.EstaActivo)
            .ThenBy(d => d.Empleado.Nombre)
            .Select(d => new
            {
                d.Id,
                d.EmpleadoId,
                EmpleadoNombre = d.Empleado.Nombre + " " + d.Empleado.Apellido,
                d.TipoDeduccion,
                d.Descripcion,
                d.Monto,
                d.Porcentaje,
                d.EsPorcentaje,
                d.EstaActivo,
                d.Categoria
            })
            .ToListAsync();

        return Ok(deducciones);
    }

    /// <summary>
    /// Retorna los tipos de acreedor disponibles.
    /// GET /api/acreedores/tipos
    /// </summary>
    [HttpGet("tipos")]
    public IActionResult GetTipos()
    {
        var tipos = Enum.GetValues<TipoAcreedor>()
            .Select(t => new { value = (int)t, label = GetTipoNombre(t) })
            .ToList();

        return Ok(tipos);
    }

    /// <summary>
    /// Busqueda rapida para combobox (max 10 resultados).
    /// GET /api/acreedores/buscar?q=banco
    /// </summary>
    [HttpGet("buscar")]
    public async Task<IActionResult> Buscar([FromQuery] string q)
    {
        if (string.IsNullOrWhiteSpace(q))
            return Ok(Array.Empty<object>());

        var term = q.ToLower();
        var resultados = await _context.Acreedores
            .Where(a => a.EstaActivo &&
                (a.Nombre.ToLower().Contains(term) ||
                 (a.Identificacion != null && a.Identificacion.ToLower().Contains(term))))
            .OrderBy(a => a.Nombre)
            .Take(10)
            .Select(a => new
            {
                a.Id,
                a.Nombre,
                a.TipoAcreedor,
                TipoAcreedorNombre = GetTipoNombre(a.TipoAcreedor),
                a.Identificacion,
                a.Banco,
                a.NumeroCuenta
            })
            .ToListAsync();

        return Ok(resultados);
    }

    /// <summary>
    /// Crea un nuevo acreedor.
    /// POST /api/acreedores
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "Owner,Admin,Manager")]
    public async Task<IActionResult> Create([FromBody] CreateAcreedorRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Nombre))
            return BadRequest(new { message = "El nombre del acreedor es requerido" });

        var tenantId = _tenantContext.TenantId;

        // Validar identificacion unica dentro del tenant
        if (!string.IsNullOrWhiteSpace(request.Identificacion))
        {
            var existe = await _context.Acreedores
                .AnyAsync(a => a.Identificacion == request.Identificacion);

            if (existe)
                return BadRequest(new { message = $"Ya existe un acreedor con identificacion {request.Identificacion}" });
        }

        var acreedor = new Acreedor
        {
            TenantId = tenantId,
            Nombre = request.Nombre.Trim(),
            TipoAcreedor = request.TipoAcreedor,
            Identificacion = request.Identificacion?.Trim(),
            TipoIdentificacion = request.TipoIdentificacion?.Trim(),
            Banco = request.Banco?.Trim(),
            TipoCuenta = request.TipoCuenta?.Trim(),
            NumeroCuenta = request.NumeroCuenta?.Trim(),
            IBAN = request.IBAN?.Trim(),
            Telefono = request.Telefono?.Trim(),
            Email = request.Email?.Trim(),
            Direccion = request.Direccion?.Trim(),
            ContactoNombre = request.ContactoNombre?.Trim(),
            Observaciones = request.Observaciones?.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        _context.Acreedores.Add(acreedor);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = acreedor.Id }, new
        {
            message = "Acreedor creado exitosamente",
            id = acreedor.Id
        });
    }

    /// <summary>
    /// Actualiza un acreedor existente.
    /// PUT /api/acreedores/{id}
    /// </summary>
    [HttpPut("{id}")]
    [Authorize(Roles = "Owner,Admin,Manager")]
    public async Task<IActionResult> Update(int id, [FromBody] CreateAcreedorRequest request)
    {
        var acreedor = await _context.Acreedores
            .Include(a => a.Deducciones.Where(d => d.EstaActivo))
            .FirstOrDefaultAsync(a => a.Id == id);

        if (acreedor == null)
            return NotFound(new { message = "Acreedor no encontrado" });

        if (string.IsNullOrWhiteSpace(request.Nombre))
            return BadRequest(new { message = "El nombre del acreedor es requerido" });

        // Validar identificacion unica (excluir el actual)
        if (!string.IsNullOrWhiteSpace(request.Identificacion))
        {
            var existe = await _context.Acreedores
                .AnyAsync(a => a.Identificacion == request.Identificacion && a.Id != id);

            if (existe)
                return BadRequest(new { message = $"Ya existe un acreedor con identificacion {request.Identificacion}" });
        }

        acreedor.Nombre = request.Nombre.Trim();
        acreedor.TipoAcreedor = request.TipoAcreedor;
        acreedor.Identificacion = request.Identificacion?.Trim();
        acreedor.TipoIdentificacion = request.TipoIdentificacion?.Trim();
        acreedor.Banco = request.Banco?.Trim();
        acreedor.TipoCuenta = request.TipoCuenta?.Trim();
        acreedor.NumeroCuenta = request.NumeroCuenta?.Trim();
        acreedor.IBAN = request.IBAN?.Trim();
        acreedor.Telefono = request.Telefono?.Trim();
        acreedor.Email = request.Email?.Trim();
        acreedor.Direccion = request.Direccion?.Trim();
        acreedor.ContactoNombre = request.ContactoNombre?.Trim();
        acreedor.Observaciones = request.Observaciones?.Trim();
        acreedor.UpdatedAt = DateTime.UtcNow;

        // Sync automatico: actualizar campos embebidos en deducciones activas vinculadas
        foreach (var deduccion in acreedor.Deducciones)
        {
            deduccion.NombreAcreedor = acreedor.Nombre;
            deduccion.IdentificacionAcreedor = acreedor.Identificacion;
            deduccion.BancoAcreedor = acreedor.Banco;
            deduccion.CuentaBancariaAcreedor = acreedor.NumeroCuenta;
            deduccion.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();

        return Ok(new { message = "Acreedor actualizado exitosamente" });
    }

    /// <summary>
    /// Desactiva un acreedor (soft delete).
    /// DELETE /api/acreedores/{id}
    /// </summary>
    [HttpDelete("{id}")]
    [Authorize(Roles = "Owner,Admin")]
    public async Task<IActionResult> Desactivar(int id)
    {
        var acreedor = await _context.Acreedores
            .Include(a => a.Deducciones.Where(d => d.EstaActivo))
            .FirstOrDefaultAsync(a => a.Id == id);

        if (acreedor == null)
            return NotFound(new { message = "Acreedor no encontrado" });

        // No desactivar si tiene deducciones con orden judicial vigente
        var deduccionesJudiciales = acreedor.Deducciones
            .Where(d => d.EstadoOrdenJudicial == Domain.Enums.EstadoOrdenJudicial.Vigente)
            .ToList();

        if (deduccionesJudiciales.Any())
            return BadRequest(new
            {
                message = "No se puede desactivar un acreedor con deducciones judiciales vigentes",
                deducciones = deduccionesJudiciales.Select(d => new { d.Id, d.Descripcion, d.NumeroExpediente })
            });

        // Validar si tiene deducciones activas (warning)
        if (acreedor.Deducciones.Any())
            return BadRequest(new
            {
                message = "Este acreedor tiene deducciones activas vinculadas. Desvinculelas primero.",
                deducciones = acreedor.Deducciones.Select(d => new { d.Id, d.Descripcion })
            });

        acreedor.EstaActivo = false;
        acreedor.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return Ok(new { message = "Acreedor desactivado exitosamente" });
    }

    // ====================================================================
    // Helpers
    // ====================================================================

    private static AcreedorDto MapToDto(Acreedor a) => new(
        a.Id,
        a.Nombre,
        a.TipoAcreedor,
        GetTipoNombre(a.TipoAcreedor),
        a.Identificacion,
        a.TipoIdentificacion,
        a.Banco,
        a.TipoCuenta,
        a.NumeroCuenta,
        a.IBAN,
        a.Telefono,
        a.Email,
        a.Direccion,
        a.ContactoNombre,
        a.Observaciones,
        a.EstaActivo,
        a.Deducciones?.Count(d => d.EstaActivo) ?? 0,
        a.Prestamos?.Count(p => p.Estado == EstadoPrestamo.Activo) ?? 0,
        a.CreatedAt
    );

    private static string GetTipoNombre(TipoAcreedor tipo) => tipo switch
    {
        TipoAcreedor.PersonaNatural => "Persona Natural",
        TipoAcreedor.Banco => "Banco",
        TipoAcreedor.Cooperativa => "Cooperativa",
        TipoAcreedor.Sindicato => "Sindicato",
        TipoAcreedor.Aseguradora => "Aseguradora",
        TipoAcreedor.EntidadGubernamental => "Entidad Gubernamental",
        TipoAcreedor.Otro => "Otro",
        _ => "Desconocido"
    };
}
