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
            .Include(d => d.Acreedor)
            .AsQueryable();

        if (empleadoId.HasValue)
            query = query.Where(d => d.EmpleadoId == empleadoId.Value);

        if (tipo.HasValue)
            query = query.Where(d => d.TipoDeduccion == tipo.Value);

        if (activas.HasValue && activas.Value)
            query = query.Where(d => d.EstaActivo);

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
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var tenantId = _tenantContext.TenantId;
        var deduccion = await _context.DeduccionesFijas
            .Where(d => d.Id == id && d.TenantId == tenantId)
            .Include(d => d.Empleado)
            .Include(d => d.Acreedor)
            .AsNoTracking()
            .FirstOrDefaultAsync();

        if (deduccion == null)
            return NotFound();

        return Ok(MapToDto(deduccion));
    }

    /// <summary>
    /// Obtiene todas las deducciones de un empleado específico.
    /// </summary>
    [HttpGet("empleado/{empleadoId}")]
    public async Task<IActionResult> GetByEmpleado(int empleadoId)
    {
        var tenantId = _tenantContext.TenantId;
        var empleado = await _context.Empleados
            .FirstOrDefaultAsync(e => e.Id == empleadoId && e.TenantId == tenantId);
        if (empleado == null)
            return NotFound(new { message = "Empleado no encontrado" });

        var deducciones = await _context.DeduccionesFijas
            .Where(d => d.EmpleadoId == empleadoId && d.TenantId == tenantId)
            .Include(d => d.Empleado)
            .Include(d => d.Acreedor)
            .AsNoTracking()
            .OrderBy(d => d.Prioridad)
            .ToListAsync();

        return Ok(deducciones.Select(MapToDto));
    }

    /// <summary>
    /// Obtiene la lista de tipos de deducción con sus nombres.
    /// </summary>
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
    [HttpPost]
    [Authorize(Roles = "Owner,Admin,Manager")]
    public async Task<IActionResult> Create(CreateDeduccionRequest request)
    {
        var tenantId = _tenantContext.TenantId;

        var empleado = await _context.Empleados
            .FirstOrDefaultAsync(e => e.Id == request.EmpleadoId && e.TenantId == tenantId);
        if (empleado == null)
            return BadRequest(new { message = "Empleado no encontrado" });

        // Validaciones basicas
        if (request.EsPorcentaje)
        {
            if (!request.Porcentaje.HasValue || request.Porcentaje.Value <= 0)
                return BadRequest(new { message = "El porcentaje debe ser mayor a cero cuando es deduccion por porcentaje" });
            if (request.Porcentaje.Value > 100)
                return BadRequest(new { message = "El porcentaje no puede ser mayor a 100" });
        }
        else
        {
            if (request.Monto <= 0)
                return BadRequest(new { message = "El monto debe ser mayor a cero cuando es deduccion fija" });
        }

        if (request.FechaFin.HasValue && request.FechaFin.Value < request.FechaInicio)
            return BadRequest(new { message = "La fecha fin no puede ser anterior a la fecha inicio" });

        // Validaciones para pension alimenticia y embargo: requieren expediente y juzgado
        if (request.TipoDeduccion == TipoDeduccion.PensionAlimenticia || request.TipoDeduccion == TipoDeduccion.Embargo)
        {
            if (string.IsNullOrWhiteSpace(request.NumeroExpediente))
                return BadRequest(new { message = "El numero de expediente es obligatorio para pension alimenticia y embargos" });
            if (string.IsNullOrWhiteSpace(request.Juzgado))
                return BadRequest(new { message = "El juzgado es obligatorio para pension alimenticia y embargos" });
        }

        // Auto-asignar categoria segun tipo si no se envia explicitamente
        var categoria = request.Categoria ?? InferirCategoria(request.TipoDeduccion, request.NumeroExpediente);

        // Auto-llenado desde catálogo de acreedores si se proporciona AcreedorId
        Acreedor? acreedor = null;
        if (request.AcreedorId.HasValue)
        {
            acreedor = await _context.Acreedores
                .FirstOrDefaultAsync(a => a.Id == request.AcreedorId.Value && a.TenantId == tenantId);
            if (acreedor == null)
                return BadRequest(new { message = "Acreedor no encontrado" });
            if (!acreedor.EstaActivo)
                return BadRequest(new { message = "El acreedor seleccionado esta inactivo" });
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
            // Acreedor - auto-llenar desde catálogo o usar campos manuales
            AcreedorId = request.AcreedorId,
            NombreAcreedor = acreedor?.Nombre ?? request.NombreAcreedor,
            IdentificacionAcreedor = acreedor?.Identificacion ?? request.IdentificacionAcreedor,
            CuentaBancariaAcreedor = acreedor?.NumeroCuenta ?? request.CuentaBancariaAcreedor,
            BancoAcreedor = acreedor?.Banco ?? request.BancoAcreedor,
            // Orden Judicial
            NumeroExpediente = request.NumeroExpediente,
            Juzgado = request.Juzgado,
            FechaOrdenJudicial = request.FechaOrdenJudicial,
            NombreJuez = request.NombreJuez,
            EstadoOrdenJudicial = request.EstadoOrdenJudicial,
            // Control de calculo
            BaseCalculo = request.BaseCalculo,
            Categoria = categoria,
            MontoTotalACobrar = request.MontoTotalACobrar,
            // Autorizacion
            TieneAutorizacionEscrita = request.TieneAutorizacionEscrita,
            FechaAutorizacion = request.FechaAutorizacion,
            DocumentoAutorizacionRef = request.DocumentoAutorizacionRef,
            // Aplicacion especial
            AplicaADecimoTercerMes = request.AplicaADecimoTercerMes,
            AplicaAPrestaciones = request.AplicaAPrestaciones,
            CreatedAt = DateTime.UtcNow
        };

        var repository = _unitOfWork.Repository<DeduccionFija>();
        await repository.AddAsync(deduccion);
        await _unitOfWork.CompleteAsync();

        deduccion = await _context.DeduccionesFijas
            .Include(d => d.Empleado)
            .Include(d => d.Acreedor)
            .FirstOrDefaultAsync(d => d.Id == deduccion.Id);

        return CreatedAtAction(nameof(GetById), new { id = deduccion!.Id }, MapToDto(deduccion));
    }

    /// <summary>
    /// Actualiza una deducción existente.
    /// </summary>
    [HttpPut("{id}")]
    [Authorize(Roles = "Owner,Admin,Manager")]
    public async Task<IActionResult> Update(int id, CreateDeduccionRequest request)
    {
        var tenantId = _tenantContext.TenantId;
        var deduccion = await _context.DeduccionesFijas
            .FirstOrDefaultAsync(d => d.Id == id && d.TenantId == tenantId);

        if (deduccion == null)
            return NotFound();

        // Validaciones
        if (request.EsPorcentaje)
        {
            if (!request.Porcentaje.HasValue || request.Porcentaje.Value <= 0)
                return BadRequest(new { message = "El porcentaje debe ser mayor a cero cuando es deduccion por porcentaje" });
            if (request.Porcentaje.Value > 100)
                return BadRequest(new { message = "El porcentaje no puede ser mayor a 100" });
        }
        else
        {
            if (request.Monto <= 0)
                return BadRequest(new { message = "El monto debe ser mayor a cero cuando es deduccion fija" });
        }

        if (request.FechaFin.HasValue && request.FechaFin.Value < request.FechaInicio)
            return BadRequest(new { message = "La fecha fin no puede ser anterior a la fecha inicio" });

        // Validaciones para pension alimenticia y embargo
        if (request.TipoDeduccion == TipoDeduccion.PensionAlimenticia || request.TipoDeduccion == TipoDeduccion.Embargo)
        {
            if (string.IsNullOrWhiteSpace(request.NumeroExpediente))
                return BadRequest(new { message = "El numero de expediente es obligatorio para pension alimenticia y embargos" });
            if (string.IsNullOrWhiteSpace(request.Juzgado))
                return BadRequest(new { message = "El juzgado es obligatorio para pension alimenticia y embargos" });
        }

        var categoria = request.Categoria ?? InferirCategoria(request.TipoDeduccion, request.NumeroExpediente);

        // Auto-llenado desde catálogo de acreedores si se proporciona AcreedorId
        Acreedor? acreedor = null;
        if (request.AcreedorId.HasValue)
        {
            acreedor = await _context.Acreedores
                .FirstOrDefaultAsync(a => a.Id == request.AcreedorId.Value && a.TenantId == tenantId);
            if (acreedor == null)
                return BadRequest(new { message = "Acreedor no encontrado" });
            if (!acreedor.EstaActivo)
                return BadRequest(new { message = "El acreedor seleccionado esta inactivo" });
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
        // Acreedor - auto-llenar desde catálogo o usar campos manuales
        deduccion.AcreedorId = request.AcreedorId;
        deduccion.NombreAcreedor = acreedor?.Nombre ?? request.NombreAcreedor;
        deduccion.IdentificacionAcreedor = acreedor?.Identificacion ?? request.IdentificacionAcreedor;
        deduccion.CuentaBancariaAcreedor = acreedor?.NumeroCuenta ?? request.CuentaBancariaAcreedor;
        deduccion.BancoAcreedor = acreedor?.Banco ?? request.BancoAcreedor;
        // Orden Judicial
        deduccion.NumeroExpediente = request.NumeroExpediente;
        deduccion.Juzgado = request.Juzgado;
        deduccion.FechaOrdenJudicial = request.FechaOrdenJudicial;
        deduccion.NombreJuez = request.NombreJuez;
        deduccion.EstadoOrdenJudicial = request.EstadoOrdenJudicial;
        // Control de calculo
        deduccion.BaseCalculo = request.BaseCalculo;
        deduccion.Categoria = categoria;
        deduccion.MontoTotalACobrar = request.MontoTotalACobrar;
        // Autorizacion
        deduccion.TieneAutorizacionEscrita = request.TieneAutorizacionEscrita;
        deduccion.FechaAutorizacion = request.FechaAutorizacion;
        deduccion.DocumentoAutorizacionRef = request.DocumentoAutorizacionRef;
        // Aplicacion especial
        deduccion.AplicaADecimoTercerMes = request.AplicaADecimoTercerMes;
        deduccion.AplicaAPrestaciones = request.AplicaAPrestaciones;
        deduccion.UpdatedAt = DateTime.UtcNow;

        await _unitOfWork.CompleteAsync();
        return NoContent();
    }

    /// <summary>
    /// Elimina una deducción permanentemente (hard delete). Si tiene orden judicial vigente, rechaza sin motivo.
    /// El historial en DeduccionAplicada se preserva (DeduccionFijaId queda en null por cascade SetNull).
    /// </summary>
    [HttpDelete("{id}")]
    [Authorize(Roles = "Owner,Admin")]
    public async Task<IActionResult> Eliminar(int id, [FromQuery] string? motivo = null)
    {
        var tenantId = _tenantContext.TenantId;
        var deduccion = await _context.DeduccionesFijas
            .FirstOrDefaultAsync(d => d.Id == id && d.TenantId == tenantId);

        if (deduccion == null)
            return NotFound();

        // Si tiene orden judicial vigente, requerir motivo
        if (deduccion.EstadoOrdenJudicial == EstadoOrdenJudicial.Vigente && string.IsNullOrWhiteSpace(motivo))
        {
            return BadRequest(new { message = "No se puede eliminar una deduccion con orden judicial vigente sin proporcionar un motivo. Use ?motivo=..." });
        }

        _context.DeduccionesFijas.Remove(deduccion);
        await _context.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>
    /// Infiere la categoria de prelacion segun el tipo de deduccion.
    /// </summary>
    private static CategoriaDeduccion InferirCategoria(TipoDeduccion tipo, string? numeroExpediente)
    {
        return tipo switch
        {
            TipoDeduccion.PensionAlimenticia => CategoriaDeduccion.PensionAlimenticia,
            TipoDeduccion.Embargo => CategoriaDeduccion.EmbargoJudicial,
            TipoDeduccion.PrestamoBancario when !string.IsNullOrEmpty(numeroExpediente) => CategoriaDeduccion.EmbargoJudicial,
            _ => CategoriaDeduccion.Voluntaria
        };
    }

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
            Prioridad: deduccion.Prioridad,
            NombreAcreedor: deduccion.NombreAcreedor,
            IdentificacionAcreedor: deduccion.IdentificacionAcreedor,
            CuentaBancariaAcreedor: deduccion.CuentaBancariaAcreedor,
            BancoAcreedor: deduccion.BancoAcreedor,
            NumeroExpediente: deduccion.NumeroExpediente,
            Juzgado: deduccion.Juzgado,
            FechaOrdenJudicial: deduccion.FechaOrdenJudicial,
            NombreJuez: deduccion.NombreJuez,
            EstadoOrdenJudicial: deduccion.EstadoOrdenJudicial,
            FechaLevantamiento: deduccion.FechaLevantamiento,
            MotivoLevantamiento: deduccion.MotivoLevantamiento,
            BaseCalculo: deduccion.BaseCalculo,
            Categoria: deduccion.Categoria,
            MontoTotalACobrar: deduccion.MontoTotalACobrar,
            MontoCobradoAcumulado: deduccion.MontoCobradoAcumulado,
            TieneAutorizacionEscrita: deduccion.TieneAutorizacionEscrita,
            FechaAutorizacion: deduccion.FechaAutorizacion,
            DocumentoAutorizacionRef: deduccion.DocumentoAutorizacionRef,
            AplicaADecimoTercerMes: deduccion.AplicaADecimoTercerMes,
            AplicaAPrestaciones: deduccion.AplicaAPrestaciones,
            AcreedorId: deduccion.AcreedorId,
            AcreedorNombre: deduccion.Acreedor?.Nombre ?? deduccion.NombreAcreedor
        );
    }

    private static string GetTipoDeduccionNombre(TipoDeduccion tipo)
    {
        return tipo switch
        {
            TipoDeduccion.PrestamoInterno => "Prestamo Interno",
            TipoDeduccion.PrestamoBancario => "Prestamo Bancario",
            TipoDeduccion.PensionAlimenticia => "Pension Alimenticia",
            TipoDeduccion.Embargo => "Embargo",
            TipoDeduccion.SeguroMedico => "Seguro Medico",
            TipoDeduccion.AhorroVoluntario => "Ahorro Voluntario",
            TipoDeduccion.Sindicato => "Sindicato",
            TipoDeduccion.Cooperativa => "Cooperativa",
            TipoDeduccion.Otro => "Otro",
            _ => tipo.ToString()
        };
    }
}
